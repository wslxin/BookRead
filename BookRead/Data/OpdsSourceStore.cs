using System.IO;
using System.Text.Json;
using BookRead.Models;

namespace BookRead.Data;

/// <summary>
/// 负责持久化 OPDS 书源列表，并与现有应用数据文件保持独立。
/// </summary>
internal sealed class OpdsSourceStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    /// <summary>
    /// 初始化 OPDS 书源存储，并使用当前用户的本地应用数据目录。
    /// </summary>
    public OpdsSourceStore()
    {
        string applicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _filePath = Path.Combine(applicationData, "BookRead", "opds-sources.json");
    }

    /// <summary>
    /// 从本地存储读取全部 OPDS 书源。
    /// </summary>
    /// <returns>已保存的书源列表；存储文件尚不存在时返回空列表。</returns>
    /// <exception cref="IOException">读取配置文件失败时抛出。</exception>
    /// <exception cref="UnauthorizedAccessException">没有权限读取配置文件时抛出。</exception>
    /// <exception cref="JsonException">配置文件内容不是有效 JSON 时抛出。</exception>
    internal IReadOnlyList<OpdsSource> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        string json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<OpdsSource>>(json, SerializerOptions) ?? [];
    }

    /// <summary>
    /// 将完整书源列表异步写入本地存储。
    /// </summary>
    /// <param name="sources">要保存的全部书源。</param>
    /// <returns>表示异步保存过程的任务。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="IOException">创建目录或写入配置文件失败时抛出。</exception>
    /// <exception cref="UnauthorizedAccessException">没有权限写入配置文件时抛出。</exception>
    internal async Task SaveAsync(IReadOnlyList<OpdsSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        string? directory = Path.GetDirectoryName(_filePath);
        if (directory is null)
        {
            throw new IOException("无法确定 OPDS 书源配置目录。");
        }

        Directory.CreateDirectory(directory);
        string json = JsonSerializer.Serialize(sources, SerializerOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
