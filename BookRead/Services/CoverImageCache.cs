using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace BookRead.Services;

/// <summary>
/// 为 OPDS 封面图片提供磁盘和内存两级缓存。
/// </summary>
internal sealed class CoverImageCache
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly Dictionary<string, byte[]> _memoryCache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _requestLock = new(1, 1);

    /// <summary>
    /// 初始化封面缓存。
    /// </summary>
    /// <param name="httpClient">用于下载封面的 HTTP 客户端。</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> 为 <see langword="null"/> 时抛出。</exception>
    public CoverImageCache(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        string applicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDirectory = Path.Combine(applicationData, "BookRead", "covers");

        // 初始化时先建目录，便于尽早发现权限问题，并保证首次下载可直接写入缓存。
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// 获取封面图片字节数据，命中磁盘时直接读取，未命中时异步下载。
    /// </summary>
    /// <param name="url">封面地址或 data URI。</param>
    /// <param name="cancellationToken">用于取消下载的令牌。</param>
    /// <returns>图片字节数据；图片无法获取时返回 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> 为 <see langword="null"/> 时抛出。</exception>
    public async Task<byte[]?> GetImageAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (_memoryCache.TryGetValue(url, out byte[]? cachedBytes))
        {
            return cachedBytes;
        }

        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(url, out cachedBytes))
            {
                return cachedBytes;
            }

            string filePath = GetCacheFilePath(url);
            if (File.Exists(filePath))
            {
                byte[] fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                _memoryCache[url] = fileBytes;
                return fileBytes;
            }

            byte[] downloadedBytes;
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int separatorIndex = url.IndexOf(',');
                if (separatorIndex < 0)
                {
                    return null;
                }

                try
                {
                    downloadedBytes = Convert.FromBase64String(url[(separatorIndex + 1)..]);
                }
                catch (FormatException)
                {
                    return null;
                }
            }
            else
            {
                downloadedBytes = await _httpClient.GetByteArrayAsync(url, cancellationToken);
            }

            if (downloadedBytes.Length == 0)
            {
                return null;
            }

            // 缓存目录可能随应用数据清理而消失，写入前必须重建，否则所有封面都会保存失败。
            Directory.CreateDirectory(_cacheDirectory);

            // 先写临时文件再替换，避免并发读取时把半张图片当作有效缓存。
            string temporaryPath = filePath + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, downloadedBytes, cancellationToken);
            File.Move(temporaryPath, filePath, overwrite: true);
            _memoryCache[url] = downloadedBytes;
            return downloadedBytes;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    /// <summary>
    /// 删除封面缓存目录。
    /// </summary>
    /// <returns>无。</returns>
    /// <exception cref="IOException">删除缓存目录失败时抛出。</exception>
    /// <exception cref="UnauthorizedAccessException">没有权限删除缓存目录时抛出。</exception>
    public void Clear()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    /// <summary>
    /// 根据封面地址生成稳定的缓存文件路径。
    /// </summary>
    /// <param name="url">封面地址。</param>
    /// <returns>缓存目录中的绝对文件路径。</returns>
    private string GetCacheFilePath(string url)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Path.Combine(_cacheDirectory, Convert.ToHexString(hash) + ".img");
    }
}

