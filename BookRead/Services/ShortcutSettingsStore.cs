using System.IO;
using System.Text.Json;
using BookRead.Models;

namespace BookRead.Services;

/// <summary>
/// 负责将阅读页设置保存到本地应用数据目录并在启动时恢复。
/// </summary>
internal sealed class ShortcutSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    /// <summary>
    /// 初始化快捷键设置存储，并使用当前用户的本地应用数据目录。
    /// </summary>
    public ShortcutSettingsStore()
    {
        string applicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _filePath = Path.Combine(applicationData, "BookRead", "settings.json");
    }

    /// <summary>
    /// 从本地存储读取阅读页设置。
    /// </summary>
    /// <returns>已保存的阅读页设置；存储文件不存在时返回默认设置。</returns>
    /// <exception cref="IOException">读取设置文件失败时抛出。</exception>
    /// <exception cref="UnauthorizedAccessException">没有权限读取设置文件时抛出。</exception>
    /// <exception cref="JsonException">设置文件内容不是有效 JSON 时抛出。</exception>
    internal ShortcutSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return ShortcutSettings.CreateDefault();
        }

        string json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<ShortcutSettings>(json, SerializerOptions)
            ?? ShortcutSettings.CreateDefault();
    }

    /// <summary>
    /// 将阅读页设置异步写入本地存储。
    /// </summary>
    /// <param name="settings">要保存的阅读页设置。</param>
    /// <returns>表示异步保存过程的任务。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> 为 null 时抛出。</exception>
    /// <exception cref="IOException">创建目录或写入设置文件失败时抛出。</exception>
    /// <exception cref="UnauthorizedAccessException">没有权限写入设置文件时抛出。</exception>
    internal async Task SaveAsync(ShortcutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? directory = Path.GetDirectoryName(_filePath);
        if (directory is null)
        {
            throw new IOException("无法确定快捷键设置目录。");
        }

        Directory.CreateDirectory(directory);
        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
