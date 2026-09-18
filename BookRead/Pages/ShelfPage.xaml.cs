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

    /// <summary>用户请求浏览 OPDS 书源时触发。</summary>
    public event RoutedEventHandler? OpdsRequested;

    /// <summary>用户请求打开书架中已有书籍时触发。</summary>
    internal event EventHandler<BookOpenRequestedEventArgs>? BookOpenRequested;

    /// <summary>用户请求从书架移除已有书籍时触发。</summary>
    internal event EventHandler<BookOpenRequestedEventArgs>? BookRemovalRequested;

    /// <summary>用户请求重命名书架中已有书籍时触发。</summary>
    internal event EventHandler<BookOpenRequestedEventArgs>? BookRenameRequested;

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
    /// 将添加书籍请求转交给主窗口处理。
    /// </summary>
    /// <param name="sender">触发事件的添加按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void ImportMenu_Click(object sender, RoutedEventArgs e)
    {
        AddBookPopup.IsOpen = true;
    }

    /// <summary>
    /// 将本地导入请求转交给主窗口处理，并收起添加书籍菜单。
    /// </summary>
    /// <param name="sender">触发事件的菜单项。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void ImportLocal_Click(object sender, RoutedEventArgs e)
    {
        AddBookPopup.IsOpen = false;
        ImportRequested?.Invoke(this, e);
    }

    /// <summary>
    /// 将 OPDS 浏览请求转交给主窗口处理，并收起添加书籍菜单。
    /// </summary>
    /// <param name="sender">触发事件的菜单项。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void BrowseOpds_Click(object sender, RoutedEventArgs e)
    {
        AddBookPopup.IsOpen = false;
        OpdsRequested?.Invoke(this, e);
    }

    /// <summary>
    /// 将给定书籍集合刷新书架列表。
    /// </summary>
    /// <param name="books">要在书架中显示的书籍。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="books"/> 为 <see langword="null"/> 时抛出。</exception>
    internal void SetBooks(IReadOnlyList<ShelfBook> books)
    {
        ArgumentNullException.ThrowIfNull(books);
        // 使用标签集合包装书籍，便于绑定来源名称而不修改持久化记录。
        BookList.ItemsSource = books.Select(book => new ShelfBookLabel(book, ResolveSourceLabel(book))).ToList();
    }

    /// <summary>
    /// 解析书架条目的来源显示名称。
    /// </summary>
    /// <param name="book">书籍记录。</param>
    /// <returns>OPDS 来源名称；本地书籍返回 <see langword="null"/>。</returns>
    private static string? ResolveSourceLabel(ShelfBook book)
    {
        return book.OpdsSourceId is null ? null : book.SourceNameSnapshot;
    }

    /// <summary>
    /// 将用户点击的书架项转交给主窗口打开。
    /// </summary>
    /// <param name="sender">触发操作的书籍按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void OpenShelfBook_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShelfBookLabel label })
        {
            return;
        }

        BookOpenRequested?.Invoke(this, new BookOpenRequestedEventArgs(label.Book));
    }

    /// <summary>
    /// 将用户点击的书架移除操作转交给主窗口处理。
    /// </summary>
    /// <param name="sender">触发操作的删除按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void RemoveShelfBook_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShelfBookLabel label })
        {
            return;
        }

        BookRemovalRequested?.Invoke(this, new BookOpenRequestedEventArgs(label.Book));
    }

    /// <summary>
    /// 将用户点击的书架重命名操作转交给主窗口处理。
    /// </summary>
    /// <param name="sender">触发操作的重命名按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void RenameShelfBook_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShelfBookLabel label })
        {
            return;
        }

        BookRenameRequested?.Invoke(this, new BookOpenRequestedEventArgs(label.Book));
    }

    /// <summary>
    /// 将用户点击的书籍位置操作转交给主窗口处理。
    /// </summary>
    /// <param name="sender">触发操作的位置按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void OpenShelfBookLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShelfBookLabel label })
        {
            return;
        }

        BookLocationRequested?.Invoke(this, new BookOpenRequestedEventArgs(label.Book));
    }
}
