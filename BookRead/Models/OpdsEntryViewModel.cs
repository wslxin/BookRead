namespace BookRead.Models;

/// <summary>
/// 为浏览页中单个 OPDS 条目的显示和下载状态提供可更新数据。
/// </summary>
internal sealed class OpdsEntryViewModel
{
    /// <summary>创建 OPDS 条目视图模型。</summary>
    /// <param name="entry">要显示的条目。</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 <see langword="null"/> 时抛出。</exception>
    public OpdsEntryViewModel(OpdsEntry entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    /// <summary>获取底层 OPDS 条目数据。</summary>
    public OpdsEntry Entry { get; }

    /// <summary>获取条目在界面中的稳定标识。</summary>
    public string Key => $"{Entry.BookId}|{Entry.Title}|{Entry.NavigationUrl ?? Entry.Acquisitions.FirstOrDefault()?.Href ?? string.Empty}";

    /// <summary>是否正在下载。</summary>
    public bool IsDownloading { get; set; }

    /// <summary>下载进度百分比，取值 0 到 100。</summary>
    public int DownloadProgress { get; set; }

    /// <summary>下载完成后生成的本地文件路径。</summary>
    public string? DownloadedFilePath { get; set; }

    /// <summary>获取下载按钮可显示的标题。</summary>
    public string DownloadTitle => IsDownloading ? "取消" : "下载";

    /// <summary>获取是否应在卡片上显示作者；目录条目不显示作者。</summary>
    public bool ShowAuthor => !Entry.IsNavigation && !string.IsNullOrWhiteSpace(Entry.Author);

    /// <summary>获取条目作者；缺少作者信息或为目录条目时返回空字符串。</summary>
    public string Subtitle => Entry.Author ?? string.Empty;
}
