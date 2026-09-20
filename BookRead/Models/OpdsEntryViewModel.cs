using System.ComponentModel;
using System.Windows.Media;

namespace BookRead.Models;

/// <summary>
/// 为浏览页中单个 OPDS 条目的显示和下载状态提供可更新数据。
/// </summary>
internal sealed class OpdsEntryViewModel : INotifyPropertyChanged
{
    private ImageSource? _coverImage;
    private bool _isDownloading;
    private int _downloadProgress;

    /// <summary>创建 OPDS 条目视图模型。</summary>
    /// <param name="entry">要显示的条目。</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 <see langword="null"/> 时抛出。</exception>
    public OpdsEntryViewModel(OpdsEntry entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    /// <summary>获取底层 OPDS 条目数据。</summary>
    public OpdsEntry Entry { get; }

    /// <summary>获取卡片封面；尚未加载完成时为 <see langword="null"/>。</summary>
    public ImageSource? CoverImage => _coverImage;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>获取条目在界面中的稳定标识。</summary>
    public string Key => $"{Entry.BookId}|{Entry.Title}|{Entry.NavigationUrl ?? Entry.Acquisitions.FirstOrDefault()?.Href ?? string.Empty}";

    /// <summary>获取或设置一个值，该值指示条目是否正在下载。</summary>
    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (_isDownloading == value)
            {
                return;
            }

            _isDownloading = value;
            OnPropertyChanged(nameof(IsDownloading));
        }
    }

    /// <summary>获取或设置下载进度百分比，取值 0 到 100。</summary>
    public int DownloadProgress
    {
        get => _downloadProgress;
        set
        {
            if (_downloadProgress == value)
            {
                return;
            }

            _downloadProgress = value;
            OnPropertyChanged(nameof(DownloadProgress));
        }
    }

    /// <summary>下载完成后生成的本地文件路径。</summary>
    public string? DownloadedFilePath { get; set; }

    /// <summary>获取下载按钮可显示的标题。</summary>
    public string DownloadTitle => IsDownloading ? "取消" : "下载";

    /// <summary>获取是否应在卡片上显示作者；目录条目不显示作者。</summary>
    public bool ShowAuthor => !Entry.IsNavigation && !string.IsNullOrWhiteSpace(Entry.Author);

    /// <summary>获取是否应显示默认封面；封面为空或尚未加载完成时显示，加载成功后隐藏。</summary>
    public bool ShowDefaultCover => _coverImage is null;

    /// <summary>获取条目作者；缺少作者信息或为目录条目时返回空字符串。</summary>
    public string Subtitle => Entry.Author ?? string.Empty;

    /// <summary>更新卡片封面并通知界面刷新。</summary>
    /// <param name="coverImage">已加载的封面图像。</param>
    /// <returns>无。</returns>
    internal void SetCoverImage(ImageSource? coverImage)
    {
        if (ReferenceEquals(_coverImage, coverImage))
        {
            return;
        }

        _coverImage = coverImage;
        OnPropertyChanged(nameof(CoverImage));
        OnPropertyChanged(nameof(ShowDefaultCover));
    }

    /// <summary>触发指定属性的界面更新。</summary>
    /// <param name="propertyName">发生变化的属性名称。</param>
    /// <returns>无。</returns>
    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
