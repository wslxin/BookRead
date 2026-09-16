using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BookRead.Controls;

/// <summary>
/// 提供应用程序自定义标题栏及窗口控制功能。
/// </summary>
public partial class TitleBar : System.Windows.Controls.UserControl
{
    /// <summary>请求所属窗口返回书架视图时触发。</summary>
    public event RoutedEventHandler? ShelfRequested;

    /// <summary>请求所属窗口打开设置页时触发。</summary>
    public event RoutedEventHandler? SettingsRequested;

    /// <summary>
    /// 初始化标题栏控件。
    /// </summary>
    public TitleBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 设置标题栏返回书架按钮的可见状态。
    /// </summary>
    /// <param name="isVisible">需要显示时为 true，否则为 false。</param>
    /// <returns>无。</returns>
    public void SetBackButtonVisible(bool isVisible)
    {
        BackToShelfButton.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 在标题栏显示当前阅读书名，并隐藏应用图标和应用名称。
    /// </summary>
    /// <param name="title">书籍显示名称。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> 为 <see langword="null"/> 时抛出。</exception>
    public void SetBookInfo(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        BookTitleText.Text = title;
        AppBrandPanel.Visibility = Visibility.Collapsed;
        BookInfoPanel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 清除标题栏中的书籍信息并恢复应用图标和应用名称。
    /// </summary>
    /// <returns>无。</returns>
    public void ClearBookInfo()
    {
        BookInfoPanel.Visibility = Visibility.Collapsed;
        AppBrandPanel.Visibility = Visibility.Visible;
        BookTitleText.Text = string.Empty;
    }

    /// <summary>
    /// 处理标题栏拖动，使无系统标题栏的窗口仍可移动。
    /// </summary>
    /// <param name="sender">触发事件的标题栏元素。</param>
    /// <param name="e">鼠标按钮事件参数。</param>
    /// <returns>无。</returns>
    /// <exception cref="InvalidOperationException">窗口当前无法执行拖动时抛出。</exception>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Button || e.OriginalSource is System.Windows.Controls.TextBox)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            return;
        }

        Window.GetWindow(this)?.DragMove();
    }

    /// <summary>
    /// 切换标题栏所属窗口的置顶状态。
    /// </summary>
    /// <param name="sender">触发事件的置顶按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Topmost_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }

        var isTopmost = !window.Topmost;
        window.Topmost = isTopmost;
        TopmostIcon.Text = isTopmost ? "\uE77A" : "\uE718";
        TopmostButton.ToolTip = isTopmost ? "取消置顶" : "置顶窗口";
    }

    /// <summary>
    /// 关闭标题栏所属窗口。
    /// </summary>
    /// <param name="sender">触发事件的关闭按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
    }

    /// <summary>
    /// 通知所属窗口打开设置页。
    /// </summary>
    /// <param name="sender">触发设置操作的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, e);
    }

    /// <summary>
    /// 通知所属窗口返回书架视图。
    /// </summary>
    /// <param name="sender">触发事件的返回书架按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void BackToShelf_Click(object sender, RoutedEventArgs e)
    {
        ShelfRequested?.Invoke(this, e);
    }

}
