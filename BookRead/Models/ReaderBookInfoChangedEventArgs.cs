namespace BookRead.Models;

/// <summary>
/// 提供阅读页当前书籍的显示名称。
/// </summary>
/// <param name="title">书籍显示名称。</param>
/// <exception cref="ArgumentNullException"><paramref name="title"/> 为 <see langword="null"/> 时抛出。</exception>
internal sealed class ReaderBookInfoChangedEventArgs(string title) : EventArgs
{
    /// <summary>获取书籍显示名称。</summary>
    internal string Title { get; } = title ?? throw new ArgumentNullException(nameof(title));
}
