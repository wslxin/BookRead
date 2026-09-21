namespace BookRead.Models;

/// <summary>
/// 为批量移除书架书籍的请求提供数据。
/// </summary>
internal sealed class BooksRemovalRequestedEventArgs : EventArgs
{
    /// <summary>
    /// 初始化批量移除请求参数。
    /// </summary>
    /// <param name="books">用户选中的待移除书籍。</param>
    /// <exception cref="ArgumentNullException"><paramref name="books"/> 为 <see langword="null"/> 时抛出。</exception>
    internal BooksRemovalRequestedEventArgs(IReadOnlyList<ShelfBook> books)
    {
        Books = books ?? throw new ArgumentNullException(nameof(books));
    }

    /// <summary>
    /// 获取用户选中的待移除书籍。
    /// </summary>
    internal IReadOnlyList<ShelfBook> Books { get; }
}
