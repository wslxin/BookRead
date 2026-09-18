using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BookRead.Controls;

/// <summary>
/// 提供应用程序自定义标题栏及窗口控制功能。
/// </summary>
public partial class TitleBar : System.Windows.Controls.UserControl
{
    /// <summary>请求所属窗口直接返回书架视图时触发。</summary>
    public event RoutedEventHandler? ShelfRequested;

    /// <summary>请求所属窗口返回上一视图或上一级目录时触发。</summary>
    public event RoutedEventHandler? BackRequested;

    /// <summary>请求所属窗口打开设置页时触发。</summary>
    public event RoutedEventHandler? SettingsRequested;

    /// <summary>请求所属窗口刷新当前 OPDS 目录时触发。</summary>
    public event RoutedEventHandler? RefreshRequested;

    /// <summary>
    /// 初始化标题栏控件。
    /// </summary>
    public TitleBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 设置标题栏返回按钮的可见状态。
    /// </summary>
    /// <param name="isVisible">需要显示时为 true，否则为 false。</param>
    /// <returns>无。</returns>
    public void SetBackButtonVisible(bool isVisible)
    {
        BackButton.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 设置 OPDS 浏览页专属标题栏控件的可见状态。
    /// </summary>
    /// <param name="isVisible">当前处于 OPDS 浏览页时为 true，否则为 false。</param>
    /// <returns>无。</returns>
    public void SetBrowseControlsVisible(bool isVisible)
    {
        BrowseActionsPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        SettingsButton.ToolTip = isVisible ? "管理书源" : "设置";
    }

    /// <summary>
    /// 将返回按钮设置为统一的“返回”显示状态。
    /// </summary>
    /// <returns>无。</returns>
    public void SetBackButtonTarget()
    {
        BackButtonIcon.Text = "\uE76B";
        BackButtonText.Text = "返回";
        BackButton.ToolTip = "返回";
    }

    /// <summary>
    /// 在标题栏显示当前阅读名称，并隐藏应用图标和应用名称。
    /// </summary>
    /// <param name="title">当前书名或章节名称。</param>
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
    /// 通知所属窗口返回上一视图或上一级目录。
    /// </summary>
    /// <param name="sender">触发事件的返回按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, e);
    }

    /// <summary>
    /// 处理返回按钮右键点击，直接返回书架。
    /// </summary>
    /// <param name="sender">触发右键操作的返回按钮。</param>
    /// <param name="e">鼠标按钮事件参数。</param>
    /// <returns>无。</returns>
    private void BackButton_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShelfRequested?.Invoke(this, e);
    }

    /// <summary>
    /// 通知所属窗口刷新当前 OPDS 目录。
    /// </summary>
    /// <param name="sender">触发刷新操作的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this, e);
    }

}
