using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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
        Visibility = Visibility.Collapsed;
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
