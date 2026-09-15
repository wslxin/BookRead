using System.Windows;
using System.Windows.Controls;
using BookRead.Models;

namespace BookRead.Pages;

/// <summary>
/// 书架页面，负责展示书架内容并发起本地书籍导入请求。
/// </summary>
public partial class ShelfPage : UserControl
{
    /// <summary>用户请求导入本地书籍时触发。</summary>
    public event RoutedEventHandler? ImportRequested;

    /// <summary>用户请求打开书架中已有书籍时触发。</summary>
    internal event EventHandler<BookOpenRequestedEventArgs>? BookOpenRequested;

    /// <summary>用户请求从书架移除已有书籍时触发。</summary>
    internal event EventHandler<BookOpenRequestedEventArgs>? BookRemovalRequested;

    /// <summary>用户请求打开书籍所在文件夹时触发。</summary>
    internal event EventHandler<BookOpenRequestedEventArgs>? BookLocationRequested;

    /// <summary>
    /// 初始化书架页面。
    /// </summary>
    public ShelfPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 将导入请求转交给主窗口处理。
    /// </summary>
    /// <param name="sender">触发事件的导入按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void ImportBook_Click(object sender, RoutedEventArgs e)
    {
        ImportRequested?.Invoke(this, e);
    }

    /// <summary>
    /// 使用给定书籍集合刷新书架列表。
    /// </summary>
    /// <param name="books">要在书架中显示的书籍。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="books"/> 为 <see langword="null"/> 时抛出。</exception>
    internal void SetBooks(IReadOnlyList<ShelfBook> books)
    {
        ArgumentNullException.ThrowIfNull(books);
        // 使用新的列表快照触发 WPF 重新生成书架项，确保普通 List 内容变更后界面立即刷新。
        BookList.ItemsSource = books.ToList();
    }

    /// <summary>
    /// 将用户点击的书架项转交给主窗口打开。
    /// </summary>
    /// <param name="sender">触发操作的书籍按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void OpenShelfBook_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShelfBook book })
        {
            return;
        }

        BookOpenRequested?.Invoke(this, new BookOpenRequestedEventArgs(book));
    }

    /// <summary>
    /// 将用户点击的书架移除操作转交给主窗口处理。
    /// </summary>
    /// <param name="sender">触发操作的删除按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void RemoveShelfBook_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShelfBook book })
        {
            return;
        }

        BookRemovalRequested?.Invoke(this, new BookOpenRequestedEventArgs(book));
    }

    /// <summary>
    /// 将用户点击的书籍位置操作转交给主窗口处理。
    /// </summary>
    /// <param name="sender">触发操作的位置按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void OpenShelfBookLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShelfBook book })
        {
            return;
        }

        BookLocationRequested?.Invoke(this, new BookOpenRequestedEventArgs(book));
    }
}
