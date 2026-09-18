using System.IO;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using BookRead.Models;

namespace BookRead.Services;

/// <summary>
/// 表示 OPDS 目录请求或解析失败。
/// </summary>
internal sealed class OpdsClientException : Exception
{
    /// <summary>使用用户可读信息初始化异常。</summary>
    /// <param name="message">可直接展示的错误信息。</param>
    public OpdsClientException(string message)
        : base(message)
    {
    }

    /// <summary>使用用户可读信息和内部异常初始化异常。</summary>
    /// <param name="message">可直接展示的错误信息。</param>
    /// <param name="innerException">导致失败的基础异常。</param>
    public OpdsClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// 负责请求 OPDS 1.x 目录并解析导航、搜索、分页、封面和下载链接。
/// </summary>
internal sealed class OpdsClient
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, HttpClient> _authenticatedClients = new();
    private readonly Dictionary<string, string?> _searchTemplates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 初始化 OPDS 客户端。
    /// </summary>
    /// <param name="httpClient">用于无认证请求的默认客户端。</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> 为 <see langword="null"/> 时抛出。</exception>
    public OpdsClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// 加载指定书源或子目录的 OPDS 页面。
    /// </summary>
    /// <param name="source">目标书源。</param>
    /// <param name="url">目录地址；为空时使用书源根地址。</param>
    /// <param name="cancellationToken">取消请求的令牌。</param>
    /// <returns>解析后的 OPDS 页面。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="OpdsClientException">地址无效、请求失败、内容不是有效 Atom/OPDS 或 XML 存在安全风险时抛出。</exception>
    public async Task<OpdsPage> LoadPageAsync(
        OpdsSource source,
        string? url,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        string requestUrl = string.IsNullOrWhiteSpace(url) ? source.Url : url;
        if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri? requestUri) ||
            requestUri.Scheme is not ("http" or "https"))
        {
            throw new OpdsClientException("OPDS 书源地址无效。");
        }

        // OpenSearch 文档解析失败不应阻塞目录浏览；先按当前请求地址返回，后续在后台补齐搜索模板。
        string? searchTemplateUrl = _searchTemplates.GetValueOrDefault(requestUrl);
        HttpResponseMessage response;
        try
        {
            response = await GetClient(source).GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new OpdsClientException(exception is TaskCanceledException
                ? "连接 OPDS 书源超时。"
                : $"无法连接 OPDS 书源：{exception.Message}", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new OpdsClientException(
                    $"读取 OPDS 目录失败：{(int)response.StatusCode} {response.ReasonPhrase}");
            }

            Uri responseBaseUri = response.RequestMessage?.RequestUri ?? requestUri;
            try
            {
                await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = XmlReader.Create(
                    contentStream,
                    new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Ignore,
                        XmlResolver = null,
                        IgnoreComments = true
                    });
                XDocument document = XDocument.Load(reader);
                OpdsPage page = await ParseFeedAsync(document, source, responseBaseUri);
                _searchTemplates[requestUrl] = page.SearchTemplateUrl ?? searchTemplateUrl;
                return page;
            }
            catch (Exception exception) when (exception is XmlException or OpdsClientException)
            {
                if (exception is OpdsClientException)
                {
                    throw;
                }

                throw new OpdsClientException("OPDS 目录格式无效。", exception);
            }
        }
    }

    /// <summary>
    /// 解析 OpenSearch 描述文档以获得 Atom 搜索模板。
    /// </summary>
    /// <param name="source">目标书源。</param>
    /// <param name="searchDescriptionUrl">搜索描述文档地址。</param>
    /// <param name="cancellationToken">取消请求的令牌。</param>
    /// <returns>包含 {searchTerms} 的搜索模板；无法解析时返回 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException">关键参数为 <see langword="null"/> 时抛出。</exception>
    public async Task<string?> GetSearchTemplateAsync(
        OpdsSource source,
        string searchDescriptionUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(searchDescriptionUrl);
        try
        {
            string xml = await GetClient(source).GetStringAsync(searchDescriptionUrl, cancellationToken);
            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(
                stringReader,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null,
                    IgnoreComments = true
                });
            XDocument document = XDocument.Load(xmlReader);
            XElement? atomUrl = document.Descendants()
                .FirstOrDefault(element =>
                    element.Name.LocalName == "Url" &&
                    string.Equals((string?)element.Attribute("type"), "application/atom+xml", StringComparison.OrdinalIgnoreCase));
            string? template = atomUrl?.Attribute("template")?.Value;
            return string.IsNullOrWhiteSpace(template) ? null : ResolveUrl(responseBaseUriFor(source, searchDescriptionUrl), template).ToString();
        }
        catch (Exception exception) when (exception is HttpRequestException or XmlException or TaskCanceledException or UriFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// 获取或创建书源对应的认证客户端。
    /// </summary>
    /// <param name="source">目标书源。</param>
    /// <returns>可复用的 HTTP 客户端。</returns>
    private HttpClient GetClient(OpdsSource source)
    {
        string cacheKey = $"{source.Id}|{source.Username}|{source.EncryptedPassword}|{source.IgnoreCertificateErrors}";
        if (_authenticatedClients.TryGetValue(cacheKey, out HttpClient? cachedClient))
        {
            return cachedClient;
        }

        // 忽略证书错误只作用于显式配置的书源，避免影响全局默认客户端。
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BookRead/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/atom+xml, application/xml, */*");
        if (!string.IsNullOrWhiteSpace(source.Username))
        {
            string password = PasswordProtector.Unprotect(source.EncryptedPassword);
            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{source.Username}:{password}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        var handler = new HttpClientHandler();
        if (source.IgnoreCertificateErrors)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        var configuredClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        CopyDefaultHeaders(configuredClient, client);
        _authenticatedClients[cacheKey] = configuredClient;
        client.Dispose();
        return configuredClient;
    }

    /// <summary>
    /// 复制默认请求头到新客户端。
    /// </summary>
    /// <param name="target">目标客户端。</param>
    /// <param name="source">来源客户端。</param>
    /// <returns>无。</returns>
    private static void CopyDefaultHeaders(HttpClient target, HttpClient source)
    {
        foreach (var header in source.DefaultRequestHeaders)
        {
            target.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    /// <summary>
    /// 为搜索模板地址构建请求基地址。
    /// </summary>
    /// <param name="source">目标书源。</param>
    /// <param name="url">搜索描述文档地址。</param>
    /// <returns>可解析相对地址的绝对 URI。</returns>
    private static Uri responseBaseUriFor(OpdsSource source, string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute)
            ? absolute
            : new Uri(source.Url);
    }

    /// <summary>
    /// 解析 Atom feed。
    /// </summary>
    /// <param name="document">已加载的 XML 文档。</param>
    /// <param name="source">目标书源。</param>
    /// <param name="baseUri">当前响应基地址。</param>
    /// <returns>解析后的 OPDS 页面。</returns>
    /// <exception cref="OpdsClientException">根节点不是 feed 时抛出。</exception>
    private async Task<OpdsPage> ParseFeedAsync(XDocument document, OpdsSource source, Uri baseUri)
    {
        XElement? feed = document.Root;
        if (feed is null || feed.Name.LocalName != "feed")
        {
            throw new OpdsClientException("OPDS 目录格式无效。");
        }

        string title = feed.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "title")?.Value.Trim() ?? "OPDS 目录";
        string? nextPageUrl = feed.Elements()
            .Where(element => element.Name.LocalName == "link")
            .Select(element => CreateLink(element, baseUri))
            .FirstOrDefault(link => string.Equals(link.Relation, "next", StringComparison.OrdinalIgnoreCase))
            ?.Href;
        string? searchDescriptionUrl = feed.Elements()
            .Where(element => element.Name.LocalName == "link")
            .Select(element => CreateLink(element, baseUri))
            .FirstOrDefault(link => string.Equals(link.Relation, "search", StringComparison.OrdinalIgnoreCase))
            ?.Href;
        string? searchTemplateUrl = string.IsNullOrWhiteSpace(searchDescriptionUrl)
            ? null
            : await GetSearchTemplateAsync(source, searchDescriptionUrl);

        var entries = new List<OpdsEntry>();
        foreach (XElement entryElement in feed.Elements().Where(element => element.Name.LocalName == "entry"))
        {
            OpdsEntry? entry = ParseEntry(entryElement, baseUri);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return new OpdsPage(title, entries, nextPageUrl, searchTemplateUrl);
    }

    /// <summary>
    /// 解析单个 Atom entry。
    /// </summary>
    /// <param name="entryElement">entry 元素。</param>
    /// <param name="baseUri">当前响应基地址。</param>
    /// <returns>解析后的条目；缺少标题或可用链接时返回 <see langword="null"/>。</returns>
    private static OpdsEntry? ParseEntry(XElement entryElement, Uri baseUri)
    {
        string title = entryElement.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "title")?.Value.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        string bookId = entryElement.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "id")?.Value.Trim() ?? title;
        string? author = entryElement.Elements()
            .Where(element => element.Name.LocalName == "author")
            .Select(element => element.Elements().FirstOrDefault(child => child.Name.LocalName == "name")?.Value.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        string? summary = ExtractPlainText(entryElement, baseUri);
        OpdsLink? coverLink = entryElement.Elements()
            .Where(element => element.Name.LocalName == "link")
            .Select(element => CreateLink(element, baseUri))
            .Where(link => link.MediaType?.Contains("image/", StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(link => GetCoverPriority(link))
            .FirstOrDefault();
        var acquisitions = entryElement.Elements()
            .Where(element => element.Name.LocalName == "link")
            .Select(element => CreateLink(element, baseUri))
            .Where(link => link.Relation?.Contains("acquisition", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        OpdsLink? navigationLink = entryElement.Elements()
            .Where(element => element.Name.LocalName == "link")
            .Select(element => CreateLink(element, baseUri))
            .FirstOrDefault(link =>
                string.Equals(link.Relation, "subsection", StringComparison.OrdinalIgnoreCase) ||
                link.MediaType?.Contains("atom+xml", StringComparison.OrdinalIgnoreCase) == true);
        string? navigationUrl = navigationLink?.Href;
        if (string.IsNullOrWhiteSpace(navigationUrl) && acquisitions.Count == 0)
        {
            return null;
        }

        return new OpdsEntry(
            bookId,
            title,
            author,
            summary,
            coverLink?.Href,
            acquisitions,
            navigationUrl);
    }

    /// <summary>
    /// 提取条目 content 或 summary 的纯文本。
    /// </summary>
    /// <param name="entryElement">entry 元素。</param>
    /// <param name="baseUri">当前响应基地址。</param>
    /// <returns>清理后的简介；没有内容时返回 <see langword="null"/>。</returns>
    private static string? ExtractPlainText(XElement entryElement, Uri baseUri)
    {
        XElement? contentElement = entryElement.Elements()
            .FirstOrDefault(element => element.Name.LocalName is "content" or "summary");
        if (contentElement is null)
        {
            return null;
        }

        string text = contentElement.Value
            .Replace("\r", " ")
            .Replace("\n", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= 260 ? text : text[..260].TrimEnd() + "…";
    }

    /// <summary>
    /// 获取封面链接优先级。
    /// </summary>
    /// <param name="link">封面链接。</param>
    /// <returns>优先级值，数值越小越优先。</returns>
    private static int GetCoverPriority(OpdsLink link)
    {
        if (string.Equals(link.Relation, "http://opds-spec.org/image/thumbnail", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(link.Relation, "http://opds-spec.org/image", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (link.Relation?.Contains("cover-thumbnail", StringComparison.OrdinalIgnoreCase) == true)
        {
            return 2;
        }

        return 3;
    }

    /// <summary>
    /// 将 Atom link 元素转换为链接模型。
    /// </summary>
    /// <param name="linkElement">link 元素。</param>
    /// <param name="baseUri">当前响应基地址。</param>
    /// <returns>包含绝对地址的链接。</returns>
    /// <exception cref="OpdsClientException">链接地址无效时抛出。</exception>
    private static OpdsLink CreateLink(XElement linkElement, Uri baseUri)
    {
        string href = linkElement.Attribute("href")?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(href) || href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return new OpdsLink(
                href,
                linkElement.Attribute("rel")?.Value,
                linkElement.Attribute("type")?.Value,
                linkElement.Attribute("title")?.Value,
                long.TryParse(linkElement.Attribute("length")?.Value, out long dataLength) ? dataLength : null);
        }

        Uri resolved = ResolveUrl(baseUri, href);
        long? length = long.TryParse(linkElement.Attribute("length")?.Value, out long parsedLength)
            ? parsedLength
            : null;
        return new OpdsLink(
            resolved.ToString(),
            linkElement.Attribute("rel")?.Value,
            linkElement.Attribute("type")?.Value,
            linkElement.Attribute("title")?.Value,
            length);
    }

    /// <summary>
    /// 将相对地址解析为绝对地址。
    /// </summary>
    /// <param name="baseUri">当前响应基地址。</param>
    /// <param name="url">相对或绝对地址。</param>
    /// <returns>解析后的绝对地址。</returns>
    /// <exception cref="OpdsClientException">地址无法解析时抛出。</exception>
    private static Uri ResolveUrl(Uri baseUri, string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute))
        {
            return absolute;
        }

        if (!Uri.TryCreate(baseUri, url, out Uri? resolved))
        {
            throw new OpdsClientException("OPDS 目录包含无效链接。");
        }

        return resolved;
    }

    /// <summary>
    /// 释放所有书源客户端。
    /// </summary>
    public void Dispose()
    {
        foreach (HttpClient client in _authenticatedClients.Values)
        {
            client.Dispose();
        }

        _authenticatedClients.Clear();
    }
}
