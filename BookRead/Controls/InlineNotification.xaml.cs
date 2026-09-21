using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BookRead.Controls;

/// <summary>内联提示展示类型。</summary>
public enum InlineNotificationType
{
    /// <summary>普通信息提示。</summary>
    Info,

    /// <summary>成功提示。</summary>
    Success,

    /// <summary>警告提示。</summary>
    Warning,

    /// <summary>错误提示。</summary>
    Error
}

/// <summary>
/// 提供阅读页和弹窗通用的短暂内联提示，支持换行和状态类型。
/// </summary>
public partial class InlineNotification : UserControl
{
    /// <summary>内联提示默认的自动隐藏时长。</summary>
    private static readonly TimeSpan DefaultAutoHideDuration = TimeSpan.FromMilliseconds(1200);

    /// <summary>当前一次显示使用的自动隐藏时长。</summary>
    private TimeSpan _currentAutoHideDuration = DefaultAutoHideDuration;

    /// <summary>当前提示操作入口的点击回调；提示没有操作入口时为 <see langword="null"/>。</summary>
    private Action? _actionHandler;

    /// <summary>带操作入口的提示显示前的命中测试设置，收起后按该值恢复。</summary>
    private bool _hitTestVisibleBeforeAction;

    /// <summary>标记当前提示是否因操作入口临时开启了命中测试，决定收起时是否恢复。</summary>
    private bool _restoresHitTestAfterAction;

    /// <summary>统计提示未暂停显示的累计时长，用于悬浮暂停后恢复剩余时间。</summary>
    private readonly Stopwatch _visibleElapsed = new();

    private readonly DispatcherTimer _hideTimer;

    /// <summary>
    /// 初始化内联提示并配置自动隐藏计时器。
    /// </summary>
    public InlineNotification()
    {
        InitializeComponent();
        Visibility = Visibility.Collapsed;
        _hideTimer = new DispatcherTimer
        {
            Interval = DefaultAutoHideDuration
        };
        _hideTimer.Tick += HideTimer_Tick;
    }

    /// <summary>
    /// 显示 Info 级别提示并按默认时长自动隐藏。
    /// </summary>
    /// <param name="message">要显示的提示文本。</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> 为 <see langword="null"/> 时抛出。</exception>
    public void Show(string message)
    {
        Show(message, InlineNotificationType.Info, DefaultAutoHideDuration);
    }

    /// <summary>
    /// 显示指定类型提示并按默认时长自动隐藏。
    /// </summary>
    /// <param name="message">要显示的提示文本。</param>
    /// <param name="type">提示类型，用于选择图标和颜色。</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> 为 <see langword="null"/> 时抛出。</exception>
    public void Show(string message, InlineNotificationType type)
    {
        Show(message, type, DefaultAutoHideDuration);
    }

    /// <summary>
    /// 显示指定类型提示并按自定义时长自动隐藏；悬浮期间暂停，离开后按剩余时长隐藏。
    /// </summary>
    /// <param name="message">要显示的提示文本。</param>
    /// <param name="type">提示类型，用于选择图标和颜色。</param>
    /// <param name="autoHideDuration">从显示到自动隐藏的等待时长。</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="autoHideDuration"/> 不大于零时抛出。</exception>
    public void Show(string message, InlineNotificationType type, TimeSpan autoHideDuration)
    {
        ShowCore(message, type, autoHideDuration, null, null);
    }

    /// <summary>
    /// 显示带操作入口的提示：操作按钮可点击，点击后先收起提示再执行回调。
    /// </summary>
    /// <param name="message">要显示的提示文本。</param>
    /// <param name="type">提示类型，用于选择图标和颜色。</param>
    /// <param name="autoHideDuration">从显示到自动隐藏的等待时长。</param>
    /// <param name="actionText">操作按钮显示文本。</param>
    /// <param name="actionHandler">操作按钮点击后执行的回调。</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/>、<paramref name="actionText"/> 或 <paramref name="actionHandler"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="autoHideDuration"/> 不大于零时抛出。</exception>
    public void Show(string message, InlineNotificationType type, TimeSpan autoHideDuration, string actionText, Action actionHandler)
    {
        ArgumentNullException.ThrowIfNull(actionText);
        ArgumentNullException.ThrowIfNull(actionHandler);
        ShowCore(message, type, autoHideDuration, actionText, actionHandler);
    }

    /// <summary>
    /// 执行提示显示逻辑，按需附加操作按钮并同步自动隐藏计时。
    /// </summary>
    /// <param name="message">要显示的提示文本。</param>
    /// <param name="type">提示类型，用于选择图标和颜色。</param>
    /// <param name="autoHideDuration">从显示到自动隐藏的等待时长。</param>
    /// <param name="actionText">操作按钮显示文本；为 <see langword="null"/> 时不显示操作按钮。</param>
    /// <param name="actionHandler">操作按钮点击后的回调；为 <see langword="null"/> 时不显示操作按钮。</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="autoHideDuration"/> 不大于零时抛出。</exception>
    private void ShowCore(string message, InlineNotificationType type, TimeSpan autoHideDuration, string? actionText, Action? actionHandler)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (autoHideDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(autoHideDuration), "自动隐藏时长必须大于零。");
        }

        _currentAutoHideDuration = autoHideDuration;
        MessageText.Text = message;

        (string icon, Brush color) = type switch
        {
            InlineNotificationType.Success => ("\uE73E", (Brush)FindResource("Accent")),
            InlineNotificationType.Warning => ("\uE7BA", new SolidColorBrush(Color.FromRgb(0xF0, 0xB3, 0x44))),
            InlineNotificationType.Error => ("\uE783", new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36))),
            _ => ("\uE946", (Brush)FindResource("TextPrimary"))
        };

        // 只有存在操作入口时才接收鼠标命中，普通提示继续保持不遮挡下方控件点击。
        _actionHandler = actionHandler;
        if (actionHandler is null)
        {
            ActionButton.Visibility = Visibility.Collapsed;
            MessageText.MaxWidth = 448;
            NotificationBorder.Cursor = null;
        }
        else
        {
            // 只在没有待恢复记录时保存使用方声明的命中设置，避免连续显示提示时把临时值误当作原始值。
            if (!_restoresHitTestAfterAction)
            {
                _hitTestVisibleBeforeAction = IsHitTestVisible;
                _restoresHitTestAfterAction = true;
            }

            IsHitTestVisible = true;
            MessageText.MaxWidth = 360;
            ActionButton.Content = actionText;
            ActionButton.Visibility = Visibility.Visible;
            // 整条提示都可触发跳转，用鼠标手型表明这里可以点击，而不仅是右侧的文字按钮。
            NotificationBorder.Cursor = Cursors.Hand;
        }

        IconText.Text = icon;
        IconText.Foreground = color;
        NotificationBorder.BorderBrush = color;
        Visibility = Visibility.Visible;
        _visibleElapsed.Restart();
        _hideTimer.Stop();
        _hideTimer.Interval = autoHideDuration;
        _hideTimer.Start();
    }

    /// <summary>
    /// 暂停自动隐藏计时；悬浮期间提示保持显示。
    /// </summary>
    /// <returns>无。</returns>
    public void SuspendAutoHide()
    {
        if (Visibility != Visibility.Visible)
        {
            return;
        }

        _visibleElapsed.Stop();
        _hideTimer.Stop();
    }

    /// <summary>
    /// 恢复自动隐藏并按剩余时长收起提示；提示未显示时忽略。
    /// </summary>
    /// <returns>无。</returns>
    public void ResumeAutoHide()
    {
        if (Visibility != Visibility.Visible)
        {
            return;
        }

        _visibleElapsed.Start();
        TimeSpan remaining = _currentAutoHideDuration - _visibleElapsed.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            Hide();
            return;
        }

        _hideTimer.Stop();
        _hideTimer.Interval = remaining;
        _hideTimer.Start();
    }

    /// <summary>
    /// 立即隐藏提示。
    /// </summary>
    /// <returns>无。</returns>
    public void Hide()
    {
        _visibleElapsed.Reset();
        _hideTimer.Stop();

        // 收起后清除操作入口，避免旧回调在下次显示时被误触发。
        _actionHandler = null;
        ActionButton.Visibility = Visibility.Collapsed;
        if (_restoresHitTestAfterAction)
        {
            _restoresHitTestAfterAction = false;
            IsHitTestVisible = _hitTestVisibleBeforeAction;
        }

        Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 响应操作按钮点击：先收起提示，再执行使用方传入的回调。
    /// </summary>
    /// <param name="sender">触发点击的操作按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        Action? handler = _actionHandler;
        Hide();
        handler?.Invoke();
    }

    /// <summary>
    /// 响应提示本体点击：存在操作入口时整条提示都可触发，降低精确点中按钮的成本。
    /// </summary>
    /// <param name="sender">触发点击的提示边框。</param>
    /// <param name="e">鼠标按键事件参数。</param>
    /// <returns>无。</returns>
    private void NotificationBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // 操作按钮的点击由按钮自身处理，不会由这里重复触发。
        Action? handler = _actionHandler;
        if (handler is null)
        {
            return;
        }

        Hide();
        handler();
    }

    /// <summary>
    /// 鼠标悬浮在提示上时暂停自动隐藏，为用户留出点击操作入口的时间。
    /// </summary>
    /// <param name="sender">触发事件的提示控件。</param>
    /// <param name="e">鼠标事件参数。</param>
    /// <returns>无。</returns>
    private void InlineNotification_MouseEnter(object sender, MouseEventArgs e)
    {
        SuspendAutoHide();
    }

    /// <summary>
    /// 鼠标离开提示后恢复自动隐藏计时。
    /// </summary>
    /// <param name="sender">触发事件的提示控件。</param>
    /// <param name="e">鼠标事件参数。</param>
    /// <returns>无。</returns>
    private void InlineNotification_MouseLeave(object sender, MouseEventArgs e)
    {
        ResumeAutoHide();
    }

    /// <summary>
    /// 响应自动隐藏计时器并收起提示。
    /// </summary>
    /// <param name="sender">触发计时器的对象。</param>
    /// <param name="e">计时器事件参数。</param>
    /// <returns>无。</returns>
    private void HideTimer_Tick(object? sender, EventArgs e)
    {
        Hide();
    }
}
