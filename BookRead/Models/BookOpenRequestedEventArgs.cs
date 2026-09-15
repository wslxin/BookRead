namespace BookRead.Models;

/// <summary>
/// 为从书架打开书籍的请求提供数据。
/// </summary>
internal sealed class BookOpenRequestedEventArgs : EventArgs
{
    /// <summary>
    /// 初始化书籍打开请求参数。
    /// </summary>
    /// <param name="book">用户选择的书架书籍。</param>
    /// <exception cref="ArgumentNullException"><paramref name="book"/> 为 <see langword="null"/> 时抛出。</exception>
    internal BookOpenRequestedEventArgs(ShelfBook book)
    {
        Book = book ?? throw new ArgumentNullException(nameof(book));
    }

    /// <summary>
    /// 获取用户选择的书架书籍。
    /// </summary>
    internal ShelfBook Book { get; }
}
