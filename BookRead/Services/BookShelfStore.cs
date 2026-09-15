using System.IO;
using System.Text.Json;
using BookRead.Models;

namespace BookRead.Services;

/// <summary>
/// 负责将用户书架保存到本地应用数据目录并在启动时恢复。
/// </summary>
internal sealed class BookShelfStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    /// <summary>
    /// 初始化书架存储，并使用当前用户的本地应用数据目录。
    /// </summary>
    public BookShelfStore()
    {
        string applicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _filePath = Path.Combine(applicationData, "BookRead", "bookshelf.json");
    }

    /// <summary>
    /// 从本地存储读取书架记录。
    /// </summary>
    /// <returns>已保存的书籍列表；存储文件尚不存在时返回空列表。</returns>
    /// <exception cref="IOException">读取书架文件失败时抛出。</exception>
    /// <exception cref="UnauthorizedAccessException">没有权限读取书架文件时抛出。</exception>
    /// <exception cref="JsonException">书架文件内容不是有效 JSON 时抛出。</exception>
    internal IReadOnlyList<ShelfBook> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        string json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<ShelfBook>>(json, SerializerOptions) ?? [];
    }

    /// <summary>
    /// 将完整书架异步写入本地存储。
    /// </summary>
    /// <param name="books">要保存的全部书架记录。</param>
    /// <returns>表示异步保存过程的任务。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="books"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="IOException">创建目录或写入书架文件失败时抛出。</exception>
    /// <exception cref="UnauthorizedAccessException">没有权限写入书架文件时抛出。</exception>
    internal async Task SaveAsync(IReadOnlyCollection<ShelfBook> books)
    {
        ArgumentNullException.ThrowIfNull(books);

        string? directory = Path.GetDirectoryName(_filePath);
        if (directory is null)
        {
            throw new IOException("无法确定书架数据目录。");
        }

        Directory.CreateDirectory(directory);
        string json = JsonSerializer.Serialize(books, SerializerOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
