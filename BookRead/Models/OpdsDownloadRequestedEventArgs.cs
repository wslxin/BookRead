namespace BookRead.Models;

/// <summary>
/// 表示 OPDS 下载操作的用户事件。
/// </summary>
internal sealed class OpdsDownloadRequestedEventArgs : EventArgs
{
    /// <summary>使用书源、条目和视图模型初始化事件。</summary>
    /// <param name="source">目标书源。</param>
    /// <param name="entry">目标 OPDS 条目。</param>
    /// <param name="entryViewModel">浏览页中对应条目的可更新状态。</param>
    /// <exception cref="ArgumentNullException">任一参数为 <see langword="null"/> 时抛出。</exception>
    public OpdsDownloadRequestedEventArgs(OpdsSource source, OpdsEntry entry, OpdsEntryViewModel entryViewModel)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        EntryViewModel = entryViewModel ?? throw new ArgumentNullException(nameof(entryViewModel));
    }

    /// <summary>获取目标书源。</summary>
    public OpdsSource Source { get; }

    /// <summary>获取目标 OPDS 条目。</summary>
    public OpdsEntry Entry { get; }

    /// <summary>获取浏览页中对应条目的可更新状态。</summary>
    public OpdsEntryViewModel EntryViewModel { get; }
}
