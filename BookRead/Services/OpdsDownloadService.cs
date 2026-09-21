using System.Net;
using System.IO;
using System.Net.Http;
using BookRead.Models;

namespace BookRead.Services;

/// <summary>
/// 表示 OPDS 下载服务可观察的状态。
/// </summary>
internal sealed class OpdsDownloadState
{
    /// <summary>创建下载状态。</summary>
    /// <param name="bookTitle">书籍显示标题。</param>
    /// <exception cref="ArgumentNullException"><paramref name="bookTitle"/> 为 <see langword="null"/> 时抛出。</exception>
    public OpdsDownloadState(string bookTitle)
    {
        BookTitle = bookTitle ?? throw new ArgumentNullException(nameof(bookTitle));
    }

    /// <summary>获取书籍标题。</summary>
    public string BookTitle { get; }

    /// <summary>获取或设置当前进度百分比，取值 0 到 100。</summary>
    public int ProgressPercent { get; set; }
}

/// <summary>
/// 定义下载目标文件与既有文件同名时的处理方式。
/// </summary>
internal enum OpdsDownloadConflictResolution
{
    /// <summary>保留既有文件，把新下载的内容另存为带序号的新文件。</summary>
    KeepBoth,

    /// <summary>用新下载的内容替换既有文件。</summary>
    Overwrite
}

/// <summary>
/// 表示 OPDS 下载服务抛出的、可直接展示给用户的异常。
/// </summary>
internal sealed class OpdsDownloadException : Exception
{
    /// <summary>使用用户可读信息初始化异常。</summary>
    /// <param name="message">可直接展示的错误信息。</param>
    public OpdsDownloadException(string message)
        : base(message)
    {
    }

    /// <summary>使用用户可读信息和内部异常初始化异常。</summary>
    /// <param name="message">可直接展示的错误信息。</param>
    /// <param name="innerException">导致下载失败的基础异常。</param>
    public OpdsDownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// 为 OPDS 书源提供构建认证 HTTP 客户端和下载书籍文件的能力。
/// </summary>
internal sealed class OpdsDownloadService : IDisposable
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 使用默认 HTTP 客户端初始化服务。
    /// </summary>
    public OpdsDownloadService()
        : this(CreateHttpClient())
    {
    }

    /// <summary>
    /// 使用外部 HTTP 客户端初始化服务；测试场景使用。
    /// </summary>
    /// <param name="httpClient">要使用的 HTTP 客户端。</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> 为 <see langword="null"/> 时抛出。</exception>
    public OpdsDownloadService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// 为指定书源构建认证 HTTP 客户端。
    /// </summary>
    /// <param name="source">目标书源。</param>
    /// <returns>已配置请求头和证书策略的客户端。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/> 时抛出。</exception>
    internal static HttpClient CreateHttpClient(OpdsSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return ConfigureClient(new HttpClient(), source);
    }

    /// <summary>
    /// 创建默认 HTTP 客户端。
    /// </summary>
    /// <returns>带基础超时的客户端。</returns>
    internal static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(60);
        return client;
    }

    /// <summary>
    /// 配置请求头和证书校验策略。
    /// </summary>
    /// <param name="client">要配置的客户端。</param>
    /// <param name="source">目标书源。</param>
    /// <returns>原客户端实例。</returns>
    internal static HttpClient ConfigureClient(HttpClient client, OpdsSource source)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(source);
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BookRead/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/atom+xml, application/xml, */*");

        if (!string.IsNullOrWhiteSpace(source.Username))
        {
            string password = PasswordProtector.Unprotect(source.EncryptedPassword);
            string credentials = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{source.Username}:{password}"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        // 该开关面向自建服务的自签名证书；必须显式开启才降低校验强度。
        if (source.IgnoreCertificateErrors)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            return new HttpClient(handler)
            {
                Timeout = client.Timeout
            };
        }

        return client;
    }

    /// <summary>
    /// 下载指定 OPDS 获取链接并保存为受支持的本地书籍文件。
    /// </summary>
    /// <param name="source">下载来源书源。</param>
    /// <param name="entry">下载条目。</param>
    /// <param name="link">下载链接。</param>
    /// <param name="conflictResolution">目标文件与既有文件同名时的处理方式。</param>
    /// <param name="progress">下载进度报告器；可为空。</param>
    /// <param name="cancellationToken">取消下载的令牌。</param>
    /// <returns>表示下载过程的任务；结果为最终文件路径。</returns>
    /// <exception cref="ArgumentNullException">关键参数为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="OpdsDownloadException">链接格式不受支持、响应失败或文件保存失败时抛出。</exception>
    public async Task<string> DownloadAsync(
        OpdsSource source,
        OpdsEntry entry,
        OpdsLink link,
        OpdsDownloadConflictResolution conflictResolution,
        IProgress<OpdsDownloadState>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(link);
        if (!Uri.TryCreate(link.Href, UriKind.Absolute, out Uri? downloadUri) ||
            downloadUri.Scheme is not ("http" or "https"))
        {
            throw new OpdsDownloadException("下载地址无效。");
        }

        // 覆盖模式直接使用基础路径，副本模式改挑一个尚未占用的序号路径；两者都在下载开始前确定。
        string targetPath = conflictResolution == OpdsDownloadConflictResolution.Overwrite
            ? GetBaseTargetPath(source.Name, entry.Title, link.MediaType, link.Href)
            : GetTargetPath(source.Name, entry.Title, link.MediaType, link.Href);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            using HttpResponseMessage response = await _httpClient.GetAsync(
                downloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new OpdsDownloadException($"下载失败：{(int)response.StatusCode} {response.ReasonPhrase}");
            }

            long? totalLength = response.Content.Headers.ContentLength ?? link.Length;
            string temporaryPath = targetPath + ".part";
            await using (Stream sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (Stream fileStream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int lastReported = -1;
                int bytesRead;
                while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;
                    if (totalLength is long length && length > 0)
                    {
                        int percent = (int)Math.Clamp(totalRead * 100 / length, 0, 100);
                        if (percent != lastReported)
                        {
                            lastReported = percent;
                            progress?.Report(new OpdsDownloadState(entry.Title) { ProgressPercent = percent });
                        }
                    }
                }
            }

            // 新内容先完整写入 .part 文件，下载成功后才落位，覆盖模式下也不会因中途失败而破坏既有文件。
            File.Move(temporaryPath, targetPath, overwrite: conflictResolution == OpdsDownloadConflictResolution.Overwrite);
            progress?.Report(new OpdsDownloadState(entry.Title) { ProgressPercent = 100 });
            return targetPath;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CleanupTemporaryFile(targetPath + ".part");
            throw new OpdsDownloadException($"书籍下载失败：{exception.Message}", exception);
        }
        catch (OperationCanceledException)
        {
            CleanupTemporaryFile(targetPath + ".part");
            throw;
        }
    }

    /// <summary>
    /// 清理可能残留的临时文件。
    /// </summary>
    /// <param name="temporaryPath">要清理的临时文件路径。</param>
    /// <returns>无。</returns>
    /// <exception cref="IOException">删除临时文件失败时抛出。</exception>
    private static void CleanupTemporaryFile(string temporaryPath)
    {
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// 生成目标文件路径；同名文件已存在时自动追加序号避让，从而保留既有文件。
    /// </summary>
    /// <param name="sourceName">书源名称。</param>
    /// <param name="bookTitle">书籍标题。</param>
    /// <param name="mediaType">下载链接媒体类型。</param>
    /// <param name="href">下载地址。</param>
    /// <returns>受支持格式且当前未被占用的目标文件路径。</returns>
    /// <exception cref="OpdsDownloadException">格式不受支持或标题为空时抛出。</exception>
    internal static string GetTargetPath(string sourceName, string bookTitle, string? mediaType, string href)
    {
        string basePath = GetBaseTargetPath(sourceName, bookTitle, mediaType, href);
        string directory = Path.GetDirectoryName(basePath)!;
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(basePath);
        string extension = Path.GetExtension(basePath);
        string finalCandidate = basePath;
        var counter = 1;
        while (File.Exists(finalCandidate))
        {
            finalCandidate = Path.Combine(directory, $"{fileNameWithoutExtension} ({counter++}){extension}");
        }

        return finalCandidate;
    }

    /// <summary>
    /// 生成不含避让序号的基础目标文件路径，用于检测下载目录中是否已存在同名文件。
    /// </summary>
    /// <param name="sourceName">书源名称。</param>
    /// <param name="bookTitle">书籍标题。</param>
    /// <param name="mediaType">下载链接媒体类型。</param>
    /// <param name="href">下载地址。</param>
    /// <returns>受支持格式的基础目标文件路径；无论同名文件是否存在都返回同一路径。</returns>
    /// <exception cref="OpdsDownloadException">格式不受支持或标题为空时抛出。</exception>
    internal static string GetBaseTargetPath(string sourceName, string bookTitle, string? mediaType, string href)
    {
        string extension = GetExtension(mediaType, href)
            ?? throw new OpdsDownloadException("没有受支持的下载格式。");
        string cleanTitle = CleanFileName(string.IsNullOrWhiteSpace(bookTitle) ? "未命名书籍" : bookTitle);
        string directory = Path.Combine(GetBooksRootDirectory(), CleanFileName(sourceName));
        return Path.Combine(directory, $"{cleanTitle}{extension}");
    }

    /// <summary>
    /// 获取下载书籍根目录。
    /// </summary>
    /// <returns>本地应用数据下的书籍根目录。</returns>
    internal static string GetBooksRootDirectory()
    {
        string applicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(applicationData, "BookRead", "Books");
    }

    /// <summary>
    /// 根据媒体类型或地址推断受支持的文件扩展名。
    /// </summary>
    /// <param name="mediaType">Atom link 的 type 属性。</param>
    /// <param name="href">下载地址。</param>
    /// <returns>带点的扩展名；格式不受支持时返回 <see langword="null"/>。</returns>
    internal static string? GetExtension(string? mediaType, string href)
    {
        string? type = mediaType?.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        switch (type?.ToLowerInvariant())
        {
            case "application/epub+zip":
                return ".epub";
            case "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
                return ".docx";
            case "text/plain":
                return ".txt";
            case "text/markdown":
                return ".md";
        }

        string extension = Path.GetExtension(new Uri(href).AbsolutePath).ToLowerInvariant();
        return extension switch
        {
            ".epub" => ".epub",
            ".docx" => ".docx",
            ".txt" or ".text" => ".txt",
            ".md" or ".markdown" => ".md",
            _ => null
        };
    }

    /// <summary>
    /// 清理文件名中的非法字符并限制长度。
    /// </summary>
    /// <param name="name">原始名称。</param>
    /// <returns>合法文件或目录名。</returns>
    internal static string CleanFileName(string name)
    {
        string trimmed = name.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(invalidChar, '_');
        }

        trimmed = trimmed.Trim('.', ' ');
        return string.IsNullOrWhiteSpace(trimmed) ? "未命名" : trimmed.Length <= 80 ? trimmed : trimmed[..80].Trim();
    }

    /// <summary>
    /// 释放下载服务资源。
    /// </summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}


