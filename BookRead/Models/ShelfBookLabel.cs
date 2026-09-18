using System.Windows;
using System.Windows.Controls;
using BookRead.Models;

namespace BookRead.Pages;

/// <summary>
/// 为书架条目提供来源显示标签。
/// </summary>
internal sealed class ShelfBookLabel
{
    /// <summary>创建书架条目标签。</summary>
    /// <param name="book">原始书籍记录。</param>
    /// <param name="sourceLabel">解析后的来源名称。</param>
    /// <exception cref="ArgumentNullException"><paramref name="book"/> 为 <see langword="null"/> 时抛出。</exception>
    public ShelfBookLabel(ShelfBook book, string? sourceLabel)
    {
        Book = book ?? throw new ArgumentNullException(nameof(book));
        SourceLabel = sourceLabel;
    }

    /// <summary>获取原始书籍记录。</summary>
    public ShelfBook Book { get; }

    /// <summary>获取显示标题。</summary>
    public string Title => Book.Title;

    /// <summary>获取书籍文件路径。</summary>
    public string FilePath => Book.FilePath;

    /// <summary>获取来源名称；本地书籍为空。</summary>
    public string? SourceLabel { get; }
}
