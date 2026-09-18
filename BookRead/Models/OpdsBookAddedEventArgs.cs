namespace BookRead.Models;

/// <summary>
/// 为浏览页报告的下载加入书架事件提供数据。
/// </summary>
internal sealed class OpdsBookAddedEventArgs : EventArgs
{
    /// <summary>使用书源、条目和本地文件初始化事件。</summary>
    /// <param name="source">下载来源书源。</param>
    /// <param name="entry">下载的 OPDS 条目。</param>
    /// <param name="filePath">下载完成后的本地文件路径。</param>
    /// <exception cref="ArgumentNullException">任一参数为 <see langword="null"/> 时抛出。</exception>
    public OpdsBookAddedEventArgs(OpdsSource source, OpdsEntry entry, string filePath)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    /// <summary>获取下载来源书源。</summary>
    public OpdsSource Source { get; }

    /// <summary>获取下载的 OPDS 条目。</summary>
    public OpdsEntry Entry { get; }

    /// <summary>获取下载完成后的本地文件路径。</summary>
    public string FilePath { get; }
}
