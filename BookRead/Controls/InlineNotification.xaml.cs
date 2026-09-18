using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BookRead.Controls;

/// <summary>
/// 提供阅读页和弹窗通用的短暂内联提示。
/// </summary>
public partial class InlineNotification : UserControl
{
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
            Interval = TimeSpan.FromMilliseconds(1200)
        };
        _hideTimer.Tick += HideTimer_Tick;
    }

    /// <summary>
    /// 显示提示并重新计时自动隐藏。
    /// </summary>
    /// <param name="message">要显示的提示文本。</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> 为 <see langword="null"/> 时抛出。</exception>
    public void Show(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        MessageText.Text = message;
        Visibility = Visibility.Visible;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    /// <summary>
    /// 立即隐藏提示。
    /// </summary>
    /// <returns>无。</returns>
    public void Hide()
    {
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
