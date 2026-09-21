using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BookRead.Models;

namespace BookRead.Services;

/// <summary>
/// 表示 Kavita 网页搜索接口不可用，例如版本过旧、接口未开放或书源密钥失效。
/// </summary>
internal sealed class KavitaSearchUnavailableException : Exception
{
    /// <summary>
    /// 使用用户可读信息初始化异常。
    /// </summary>
    /// <param name="message">可直接展示的错误信息。</param>
    public KavitaSearchUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 使用用户可读信息和内部异常初始化异常。
    /// </summary>
    /// <param name="message">可直接展示的错误信息。</param>
    /// <param name="innerException">导致失败的基础异常。</param>
    public KavitaSearchUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// 使用 Kavita 网页搜索接口（/api/search/search）为 OPDS 书源提供增强搜索，
/// 使搜索关键词可以匹配作者名，并列出该作者的作品系列。
/// </summary>
internal sealed class KavitaSearchService : IDisposable
{
    /// <summary>内部搜索地址前缀，用于在浏览页导航历史中标记一次作者可检索的搜索请求。</summary>
    private const string SearchUrlPrefix = "kavita-search:";

    /// <summary>每次搜索最多展开的作者数量，避免为一个关键词发出过多服务端请求。</summary>
    private const int MaxPersonsToExpand = 3;

    /// <summary>搜索结果条目上限，避免作者作品过多时列表过长。</summary>
    private const int MaxEntries = 60;

    /// <summary>匹配 Kavita OPDS 地址并提取服务根地址与密钥。</summary>
    private static readonly Regex OpdsUrlPattern = new(
        @"^(?<base>https?://[^/]+)/api/opds/(?<key>[^/?#]+)/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<Guid, HttpClient> _clients = new();
    private readonly Dictionary<Guid, string> _tokens = new();

    /// <summary>
    /// 判断书源是否为 Kavita OPDS 书源；是则生成可放入导航历史的内部搜索地址。
    /// </summary>
    /// <param name="source">要检查的书源。</param>
    /// <param name="query">用户输入的关键词。</param>
    /// <param name="searchUrl">生成的内部搜索地址。</param>
    /// <returns>书源支持 Kavita 增强搜索时返回 <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/> 时抛出。</exception>
    internal static bool TryCreateSearchUrl(OpdsSource source, string query, out string searchUrl)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!TryParseSource(source, out _, out _, out _))
        {
            searchUrl = string.Empty;
            return false;
        }

        searchUrl = SearchUrlPrefix + Uri.EscapeDataString(query);
        return true;
    }

    /// <summary>
    /// 尝试从内部搜索地址解析搜索关键词。
    /// </summary>
    /// <param name="url">要解析的地址。</param>
    /// <param name="query">解析出的关键词。</param>
    /// <returns>地址是 Kavita 增强搜索地址时返回 <see langword="true"/>。</returns>
    internal static bool TryGetQuery(string? url, out string query)
    {
        query = string.Empty;
        if (url is null || !url.StartsWith(SearchUrlPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        query = Uri.UnescapeDataString(url[SearchUrlPrefix.Length..]);
        return !string.IsNullOrWhiteSpace(query);
    }

    /// <summary>
    /// 使用 Kavita 网页搜索接口执行一次支持作者匹配的搜索。
    /// </summary>
    /// <param name="source">目标 Kavita 书源。</param>
    /// <param name="query">用户输入的关键词。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>包含直接命中的系列与作者作品系列的搜索结果页。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="KavitaSearchUnavailableException">书源不是 Kavita、接口不可用或密钥无效时抛出。</exception>
    internal async Task<OpdsPage> SearchAsync(
        OpdsSource source,
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!TryParseSource(source, out Uri? serverBase, out string? apiKey, out string? opdsBaseUrl))
        {
            throw new KavitaSearchUnavailableException("该书源不是 Kavita OPDS 书源，无法使用作者检索。");
        }

        HttpClient client = GetClient(source);
        (KavitaSearchResponse response, string token) = await SendSearchAsync(source, serverBase, apiKey, query, client, cancellationToken);

        List<OpdsEntry> entries = [];
        HashSet<int> addedSeriesIds = [];

        // 直接命中的系列排在前面，与 Kavita 网页版搜索结果的展示顺序保持一致。
        foreach (KavitaSeriesDto series in response.Series)
        {
            if (entries.Count >= MaxEntries)
            {
                break;
            }

            if (addedSeriesIds.Add(series.EffectiveId))
            {
                entries.Add(CreateSeriesEntry(serverBase, apiKey, opdsBaseUrl, series));
            }
        }

        // 作者命中时展开作者的代表作品，让搜索作者名也能直接找到书。
        foreach (KavitaPersonDto person in response.Persons.Take(MaxPersonsToExpand))
        {
            if (entries.Count >= MaxEntries)
            {
                break;
            }

            IReadOnlyList<KavitaSeriesDto> personSeries = await GetPersonSeriesAsync(serverBase, person, token, client, cancellationToken);
            foreach (KavitaSeriesDto series in personSeries)
            {
                if (entries.Count >= MaxEntries)
                {
                    break;
                }

                if (addedSeriesIds.Add(series.EffectiveId))
                {
                    entries.Add(CreateSeriesEntry(serverBase, apiKey, opdsBaseUrl, series));
                }
            }
        }

        return new OpdsPage($"搜索：{query}", entries, null, null, null);
    }

    /// <summary>
    /// 解析 Kavita OPDS 地址，提取服务根地址、密钥和 OPDS 前缀。
    /// </summary>
    /// <param name="source">要解析的书源。</param>
    /// <param name="serverBase">服务根地址，例如 https://host。</param>
    /// <param name="apiKey">OPDS 地址中的密钥。</param>
    /// <param name="opdsBaseUrl">书源 OPDS 前缀地址，用于拼接系列页面地址。</param>
    /// <returns>地址符合 Kavita OPDS 形式时返回 <see langword="true"/>。</returns>
    private static bool TryParseSource(OpdsSource source, out Uri serverBase, out string apiKey, out string opdsBaseUrl)
    {
        serverBase = null!;
        apiKey = string.Empty;
        opdsBaseUrl = string.Empty;

        string sourceUrl = source.Url ?? string.Empty;
        Match match = OpdsUrlPattern.Match(sourceUrl);
        if (!match.Success || !Uri.TryCreate(match.Groups["base"].Value, UriKind.Absolute, out Uri? parsedBase))
        {
            return false;
        }

        serverBase = parsedBase;
        apiKey = match.Groups["key"].Value;
        opdsBaseUrl = sourceUrl.TrimEnd('/');
        return true;
    }

    /// <summary>
    /// 请求 Kavita 搜索接口；令牌过期时重新登录并重试一次。
    /// </summary>
    /// <param name="source">目标书源。</param>
    /// <param name="serverBase">服务根地址。</param>
    /// <param name="apiKey">OPDS 地址中的密钥。</param>
    /// <param name="query">用户输入的关键词。</param>
    /// <param name="client">已配置的 HTTP 客户端。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>反序列化后的搜索结果，以及本次请求实际使用的访问令牌。</returns>
    /// <exception cref="KavitaSearchUnavailableException">接口返回失败状态时抛出。</exception>
    private async Task<(KavitaSearchResponse Response, string Token)> SendSearchAsync(
        OpdsSource source,
        Uri serverBase,
        string apiKey,
        string query,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        string token = await GetTokenAsync(source, serverBase, apiKey, client, cancellationToken);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            var requestUri = new Uri(
                serverBase,
                $"/api/search/search?includeChapterAndFiles=false&queryString={Uri.EscapeDataString(query)}");
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await SendAsync(client, request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                // 令牌可能已过期：清除缓存重新登录后再试一次。
                _tokens.Remove(source.Id);
                token = await GetTokenAsync(source, serverBase, apiKey, client, cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new KavitaSearchUnavailableException(
                    $"Kavita 搜索接口不可用：{(int)response.StatusCode} {response.ReasonPhrase}");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            KavitaSearchResponse result = await JsonSerializer.DeserializeAsync<KavitaSearchResponse>(stream, SerializerOptions, cancellationToken)
                                          ?? new KavitaSearchResponse();
            return (result, token);
        }

        throw new KavitaSearchUnavailableException("Kavita 搜索接口不可用。");
    }

    /// <summary>
    /// 获取指定作者的系列列表；展开失败时返回空列表，不影响其它搜索结果。
    /// </summary>
    /// <param name="serverBase">服务根地址。</param>
    /// <param name="person">匹配到的作者。</param>
    /// <param name="token">用于接口鉴权的访问令牌。</param>
    /// <param name="client">已配置的 HTTP 客户端。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>作者的系列列表；请求失败时为空列表。</returns>
    private async Task<IReadOnlyList<KavitaSeriesDto>> GetPersonSeriesAsync(
        Uri serverBase,
        KavitaPersonDto person,
        string token,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestUri = new Uri(serverBase, $"/api/person/series-known-for?personId={person.Id}");
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage response = await SendAsync(client, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<List<KavitaSeriesDto>>(stream, SerializerOptions, cancellationToken)
                   ?? [];
        }
        catch (KavitaSearchUnavailableException)
        {
            return [];
        }
    }

    /// <summary>
    /// 使用 OPDS 地址中的密钥登录 Kavita 并缓存访问令牌。
    /// </summary>
    /// <param name="source">目标书源。</param>
    /// <param name="serverBase">服务根地址。</param>
    /// <param name="apiKey">OPDS 地址中的密钥。</param>
    /// <param name="client">已配置的 HTTP 客户端。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>可用于后续接口调用的 JWT 令牌。</returns>
    /// <exception cref="KavitaSearchUnavailableException">登录失败或响应缺少令牌时抛出。</exception>
    private async Task<string> GetTokenAsync(
        OpdsSource source,
        Uri serverBase,
        string apiKey,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        if (_tokens.TryGetValue(source.Id, out string? cachedToken))
        {
            return cachedToken;
        }

        // Kavita 登录接口要求用户名与密码字段非空；提供 API 密钥时服务端按键校验并忽略密码内容。
        string requestBody = JsonSerializer.Serialize(new { apiKey, username = "-", password = "-" });
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(serverBase, "/api/account/login"))
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage response = await SendAsync(client, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new KavitaSearchUnavailableException(
                $"使用书源密钥登录 Kavita 失败：{(int)response.StatusCode} {response.ReasonPhrase}");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        KavitaLoginResponse? login = await JsonSerializer.DeserializeAsync<KavitaLoginResponse>(stream, SerializerOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(login?.Token))
        {
            throw new KavitaSearchUnavailableException("Kavita 登录响应缺少访问令牌。");
        }

        _tokens[source.Id] = login.Token;
        return login.Token;
    }

    /// <summary>
    /// 发送请求并把网络层异常转换为增强搜索不可用异常。
    /// </summary>
    /// <param name="client">要使用的 HTTP 客户端。</param>
    /// <param name="request">要发送的请求。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>服务端响应。</returns>
    /// <exception cref="KavitaSearchUnavailableException">请求超时或无法连接服务器时抛出。</exception>
    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new KavitaSearchUnavailableException(
                exception is TaskCanceledException ? "Kavita 搜索请求超时。" : $"无法连接 Kavita 服务器：{exception.Message}",
                exception);
        }
    }

    /// <summary>
    /// 为指定书源创建并缓存 HTTP 客户端。
    /// </summary>
    /// <param name="source">目标书源。</param>
    /// <returns>已配置超时、请求头与证书策略的客户端。</returns>
    private HttpClient GetClient(OpdsSource source)
    {
        if (_clients.TryGetValue(source.Id, out HttpClient? cachedClient))
        {
            return cachedClient;
        }

        HttpMessageHandler handler = new HttpClientHandler();
        if (source.IgnoreCertificateErrors)
        {
            // 与 OPDS 请求保持一致：仅在书源显式开启时降低证书校验强度。
            handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
        }

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BookRead/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _clients[source.Id] = client;
        return client;
    }

    /// <summary>
    /// 把搜索或作者作品列表中的系列转换为可导航的 OPDS 条目。
    /// </summary>
    /// <param name="serverBase">服务根地址。</param>
    /// <param name="apiKey">OPDS 地址中的密钥，用于拼接封面地址。</param>
    /// <param name="opdsBaseUrl">书源 OPDS 前缀地址。</param>
    /// <param name="series">系列数据。</param>
    /// <returns>点击后进入该系列 OPDS 页面的导航条目。</returns>
    private static OpdsEntry CreateSeriesEntry(Uri serverBase, string apiKey, string opdsBaseUrl, KavitaSeriesDto series)
    {
        int seriesId = series.EffectiveId;
        string authority = serverBase.GetLeftPart(UriPartial.Authority);
        string coverUrl = $"{authority}/api/image/series-cover?seriesId={seriesId}&apiKey={Uri.EscapeDataString(apiKey)}";

        return new OpdsEntry(
            $"kavita-series-{seriesId}",
            string.IsNullOrWhiteSpace(series.Name) ? "未命名系列" : series.Name,
            null,
            null,
            coverUrl,
            [],
            $"{opdsBaseUrl}/series/{seriesId}");
    }

    /// <summary>
    /// 释放所有书源客户端与缓存的令牌。
    /// </summary>
    public void Dispose()
    {
        foreach (HttpClient client in _clients.Values)
        {
            client.Dispose();
        }

        _clients.Clear();
        _tokens.Clear();
    }

    /// <summary>Kavita 登录接口响应中使用的字段。</summary>
    private sealed class KavitaLoginResponse
    {
        /// <summary>登录成功后返回的 JWT 令牌。</summary>
        public string? Token { get; set; }
    }

    /// <summary>Kavita 搜索接口响应中使用的分组字段。</summary>
    private sealed class KavitaSearchResponse
    {
        /// <summary>按系列名匹配的系列。</summary>
        public List<KavitaSeriesDto> Series { get; set; } = [];

        /// <summary>按名称匹配的作者。</summary>
        public List<KavitaPersonDto> Persons { get; set; } = [];
    }

    /// <summary>Kavita 系列数据中使用的字段。</summary>
    private sealed class KavitaSeriesDto
    {
        /// <summary>作者作品接口返回的系列标识。</summary>
        public int Id { get; set; }

        /// <summary>搜索结果中使用的系列标识字段。</summary>
        public int SeriesId { get; set; }

        /// <summary>系列名称。</summary>
        public string? Name { get; set; }

        /// <summary>获取用于拼接地址与去重的系列标识；搜索接口与作者作品接口的字段名不同。</summary>
        public int EffectiveId => SeriesId != 0 ? SeriesId : Id;
    }

    /// <summary>Kavita 作者数据中使用的字段。</summary>
    private sealed class KavitaPersonDto
    {
        /// <summary>作者标识。</summary>
        public int Id { get; set; }

        /// <summary>作者名称。</summary>
        public string? Name { get; set; }
    }
}
