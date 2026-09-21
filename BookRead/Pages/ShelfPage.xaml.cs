using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BookRead.Models;

namespace BookRead.Pages;

/// <summary>
/// 书架页面，负责展示书架内容并发起本地书籍导入请求。
/// </summary>
public partial class ShelfPage : UserControl
{
    /// <summary>Segoe Fluent Icons 中“多选”字形，用于进入多选模式。</summary>
    private const string SelectionIcon = "\uE8FD";

    /// <summary>Segoe Fluent Icons 中“取消”字形，用于退出多选模式。</summary>
    private const string CancelIcon = "\uE711";

    /// <summary>
    /// 标识多选模式依赖属性，供数据模板中的勾选框与操作按钮通过绑定响应模式切换。
    /// </summary>
    public static readonly DependencyProperty IsSelectionModeProperty = DependencyProperty.Register(
        nameof(IsSelectionMode),
        typeof(bool),
        typeof(ShelfPage),
        new PropertyMetadata(false));

    /// <summary>用户请求导入本地书籍时触发。</summary>
    public event RoutedEventHandler? ImportRequested;

    /// <summary>用户请求浏览 OPDS 书源时触发。</summary>
    public event RoutedEventHandler? OpdsRequested;

    /// <summary>用户请求打开书架中已有书籍时触发。</summary>
    internal event EventHandler<BookOpenRequestedEventArgs>? BookOpenRequested;

    /// <summary>用户请求从书架移除已有书籍时触发。</summary>
    internal event EventHandler<BookOpenRequestedEventArgs>? BookRemovalRequested;

    /// <summary>用户请求批量移除多选模式下勾选的书籍时触发。</summary>
    internal event EventHandler<BooksRemovalRequestedEventArgs>? BooksRemovalRequested;

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
    /// 获取一个值，该值指示书架是否处于多选模式；多选模式下点击整行改为勾选书籍。
    /// </summary>
    public bool IsSelectionMode
    {
        get => (bool)GetValue(IsSelectionModeProperty);
        private set => SetValue(IsSelectionModeProperty, value);
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

        // 没有可勾选的书籍时不提供多选入口，也退出可能残留的多选模式。
        SelectionToggleButton.IsEnabled = books.Count > 0;
        if (books.Count == 0 && IsSelectionMode)
        {
            SetSelectionMode(false);
        }
    }

    /// <summary>
    /// 切换书架的多选模式：进入后每行显示勾选框，退出时清空已选书籍。
    /// </summary>
    /// <param name="isSelectionMode">为 <see langword="true"/> 时进入多选模式。</param>
    /// <returns>无。</returns>
    internal void SetSelectionMode(bool isSelectionMode)
    {
        if (IsSelectionMode == isSelectionMode)
        {
            return;
        }

        // 先清空已选内容再切换选择模式，避免切回单选时残留多选结果中的一项。
        BookList.UnselectAll();
        BookList.SelectionMode = isSelectionMode ? SelectionMode.Multiple : SelectionMode.Single;
        IsSelectionMode = isSelectionMode;

        // 入口只用图标：进入多选显示勾选框字形，激活后换成退出标记，并把含义交给工具提示。
        SelectionToggleIcon.Text = isSelectionMode ? CancelIcon : SelectionIcon;
        SelectionToggleButton.ToolTip = isSelectionMode ? "退出多选" : "多选";
        AddBookButton.Visibility = isSelectionMode ? Visibility.Collapsed : Visibility.Visible;
        DeleteSelectedButton.Visibility = isSelectionMode ? Visibility.Visible : Visibility.Collapsed;
        ShelfHeaderText.Foreground = (Brush)FindResource(isSelectionMode ? "TextPrimary" : "TextMuted");
        UpdateSelectionStatus();
    }

    /// <summary>
    /// 响应标题栏的选择按钮，在进入多选与退出多选之间切换。
    /// </summary>
    /// <param name="sender">触发点击的选择按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void SelectionToggle_Click(object sender, RoutedEventArgs e)
    {
        SetSelectionMode(!IsSelectionMode);
    }

    /// <summary>
    /// 将多选模式下勾选的书籍转交主窗口批量移除。
    /// </summary>
    /// <param name="sender">触发点击的批量删除按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        List<ShelfBook> books = BookList.SelectedItems
            .OfType<ShelfBookLabel>()
            .Select(label => label.Book)
            .ToList();
        if (books.Count == 0)
        {
            return;
        }

        BooksRemovalRequested?.Invoke(this, new BooksRemovalRequestedEventArgs(books));
    }

    /// <summary>
    /// 响应列表选择变化，同步批量删除入口的可用状态与选中数量。
    /// </summary>
    /// <param name="sender">发起事件的书架列表。</param>
    /// <param name="e">选择变化事件参数。</param>
    /// <returns>无。</returns>
    private void BookList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionStatus();
    }

    /// <summary>
    /// 刷新批量删除入口的可用状态与选中数量文案。
    /// </summary>
    /// <returns>无。</returns>
    private void UpdateSelectionStatus()
    {
        int selectedCount = BookList.SelectedItems.Count;

        if (!IsSelectionMode)
        {
            ShelfHeaderText.Text = "我的书架";
            return;
        }

        // 未选中任何书籍时用标题说明下一步动作，选中后改为显示数量，让用户始终清楚会移除几本。
        ShelfHeaderText.Text = selectedCount == 0 ? "选择要移除的书籍" : $"已选择 {selectedCount} 项";
        DeleteSelectedButton.IsEnabled = selectedCount > 0;
    }

    /// <summary>
    /// 处理多选模式下的键盘操作：Esc 退出多选，Ctrl+A 全选当前书架。
    /// </summary>
    /// <param name="sender">接收按键事件的书架页面。</param>
    /// <param name="e">包含按键与修饰键状态的事件参数。</param>
    /// <returns>无。</returns>
    private void ShelfPage_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsSelectionMode)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            SetSelectionMode(false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            BookList.SelectAll();
            e.Handled = true;
        }
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
    /// 滚动到指定路径的书籍所在行并短暂高亮，用于从其他页面跳转过来时指明目标。
    /// </summary>
    /// <param name="filePath">要定位的书籍文件路径。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> 为 <see langword="null"/>、空或仅由空白字符组成时抛出。</exception>
    internal void FocusBook(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("书籍文件路径不能为空。", nameof(filePath));
        }

        if (BookList.ItemsSource is not IEnumerable<ShelfBookLabel> labels)
        {
            return;
        }

        ShelfBookLabel? target = labels.FirstOrDefault(label =>
            string.Equals(label.Book.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return;
        }

        // 列表可能刚切换到可见状态，等布局完成后再滚动，否则行容器尚未生成、找不到高亮元素。
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                BookList.ScrollIntoView(target);
                BookList.UpdateLayout();

                if (BookList.ItemContainerGenerator.ContainerFromItem(target) is ListBoxItem item &&
                    FindVisualDescendant<Border>(item, "RowHighlight") is { } highlight)
                {
                    PlayFocusHighlight(highlight);
                }
            }));
    }

    /// <summary>
    /// 播放一次性的行高亮动画，从柔和的强调色快速淡出，只用于指明目标位置。
    /// </summary>
    /// <param name="highlight">书籍行内的背景高亮元素。</param>
    /// <returns>无。</returns>
    private static void PlayFocusHighlight(Border highlight)
    {
        var fade = new DoubleAnimation
        {
            From = 0.22,
            To = 0,
            Duration = TimeSpan.FromSeconds(1.2),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // 动画结束后停在透明状态，与初始外观一致，不影响行本身的悬浮与选中效果。
        highlight.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>
    /// 在可视树中按名称查找指定类型的元素，用于定位数据模板内部的行内元素。
    /// </summary>
    /// <typeparam name="T">要查找的元素类型。</typeparam>
    /// <param name="root">开始查找的根元素。</param>
    /// <param name="name">元素的 <c>x:Name</c> 名称。</param>
    /// <returns>找到的元素；未找到时返回 <see langword="null"/>。</returns>
    private static T? FindVisualDescendant<T>(DependencyObject? root, string name)
        where T : FrameworkElement
    {
        if (root is null)
        {
            return null;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T element && string.Equals(element.Name, name, StringComparison.Ordinal))
            {
                return element;
            }

            if (FindVisualDescendant<T>(child, name) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
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

        // 多选模式下整行点击用于勾选书籍，避免误触直接打开阅读。
        if (IsSelectionMode && BookList.ItemContainerGenerator.ContainerFromItem(label) is ListBoxItem item)
        {
            item.IsSelected = !item.IsSelected;
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
