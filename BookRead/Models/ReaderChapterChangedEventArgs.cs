namespace BookRead.Models;

/// <summary>
/// 提供阅读页当前章节的显示名称。
/// </summary>
/// <param name="title">章节显示名称。</param>
/// <exception cref="ArgumentNullException"><paramref name="title"/> 为 <see langword="null"/> 时抛出。</exception>
internal sealed class ReaderChapterChangedEventArgs(string title) : EventArgs
{
    /// <summary>获取章节显示名称。</summary>
    internal string Title { get; } = title ?? throw new ArgumentNullException(nameof(title));
}