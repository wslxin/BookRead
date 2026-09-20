using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BookRead.Controls;

/// <summary>
/// 提供应用程序自定义标题栏及窗口控制功能。
/// </summary>
public partial class TitleBar : System.Windows.Controls.UserControl
{
    /// <summary>下载入口图标在“空闲”与“下载中”两种颜色之间过渡的时长。</summary>
    private static readonly Duration IndicatorColorDuration = new(TimeSpan.FromMilliseconds(160));

    /// <summary>下载入口空闲态的宽度，与标题栏中的置顶、设置等图标按钮保持完全一致。</summary>
    private const double IndicatorIdleWidth = 30;

    /// <summary>下载入口空闲态的内边距，让图标在 30×30 的方块内居中。</summary>
    private static readonly Thickness IndicatorIdlePadding = new(0);

    /// <summary>下载入口在展示进度文本时使用的水平内边距。</summary>
    private static readonly Thickness IndicatorTextPadding = new(10, 0, 10, 0);

    /// <summary>下载入口图标的专属画刷，仅动画它自身的颜色，避免影响主题中的共享画刷。</summary>
    private readonly SolidColorBrush _indicatorIconBrush = new();

    /// <summary>请求所属窗口直接返回书架视图时触发。</summary>
    public event RoutedEventHandler? ShelfRequested;

    /// <summary>请求所属窗口返回上一视图或上一级目录时触发。</summary>
    public event RoutedEventHandler? BackRequested;

    /// <summary>请求所属窗口打开设置页时触发。</summary>
    public event RoutedEventHandler? SettingsRequested;

    /// <summary>请求所属窗口刷新当前 OPDS 目录时触发。</summary>
    public event RoutedEventHandler? RefreshRequested;

    /// <summary>请求所属窗口切回 OPDS 浏览页查看下载时触发。</summary>
    public event RoutedEventHandler? DownloadIndicatorRequested;

    /// <summary>
    /// 初始化标题栏控件。
    /// </summary>
    public TitleBar()
    {
        InitializeComponent();

        _indicatorIconBrush.Color = GetThemeColor("ButtonForeground");
        DownloadIndicatorIcon.Foreground = _indicatorIconBrush;

        // 归位到空闲态：进度文本即使内容为空也会占住左侧外边距，会把图标挤出按钮中心。
        SetDownloadIndicator(null);
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
    /// 更新标题栏下载入口的显示：有下载任务时显示进度文本并高亮图标，否则只保留入口图标。
    /// </summary>
    /// <param name="text">要显示的下载进度文本；为 <see langword="null"/> 或空白时仅显示图标。</param>
    /// <returns>无。</returns>
    public void SetDownloadIndicator(string? text)
    {
        bool hasText = !string.IsNullOrWhiteSpace(text);
        DownloadIndicatorText.Text = hasText ? text! : string.Empty;
        DownloadIndicatorText.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;

        // 空闲态必须收成与置顶、设置按钮完全相同的 30×30 方块，否则图标的视觉中心会与相邻按钮错位；
        // 只有需要展示进度文本时才放开宽度和内边距，让入口按内容自然扩展。
        DownloadIndicatorButton.Width = hasText ? double.NaN : IndicatorIdleWidth;
        DownloadIndicatorButton.Padding = hasText ? IndicatorTextPadding : IndicatorIdlePadding;

        // 用颜色而不是显隐表达“正在下载”，入口常驻后标题栏布局不会随任务开始和结束跳动。
        AnimateIndicatorIconColor(GetThemeColor(hasText ? "Accent" : "ButtonForeground"));
    }

    /// <summary>
    /// 读取主题资源中的颜色。
    /// </summary>
    /// <param name="resourceKey">主题资源键。</param>
    /// <returns>资源对应的颜色；资源缺失或类型不符时返回按钮默认前景色。</returns>
    private Color GetThemeColor(string resourceKey)
    {
        return TryFindResource(resourceKey) is SolidColorBrush brush
            ? brush.Color
            : Color.FromRgb(0xD9, 0xDE, 0xE2);
    }

    /// <summary>
    /// 平滑过渡下载入口图标的颜色。
    /// </summary>
    /// <param name="color">目标颜色。</param>
    /// <returns>无。</returns>
    private void AnimateIndicatorIconColor(Color color)
    {
        _indicatorIconBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, IndicatorColorDuration));
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

    /// <summary>
    /// 通知所属窗口切回 OPDS 浏览页查看下载进度。
    /// </summary>
    /// <param name="sender">触发点击的下载指示器按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void DownloadIndicator_Click(object sender, RoutedEventArgs e)
    {
        DownloadIndicatorRequested?.Invoke(this, e);
    }

}
