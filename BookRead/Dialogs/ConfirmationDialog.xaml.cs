using System.Windows;

namespace BookRead.Dialogs;

/// <summary>
/// 提供与应用视觉一致、内容可配置的通用确认弹窗。
/// </summary>
internal partial class ConfirmationDialog : Window
{
    /// <summary>
    /// 使用指定配置初始化确认弹窗。
    /// </summary>
    /// <param name="options">弹窗的内容与操作语义配置。</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 <see langword="null"/> 时抛出。</exception>
    private ConfirmationDialog(ConfirmationDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        InitializeComponent();
        Title = options.Title;
        DialogTitle.Text = options.Title;
        DialogMessage.Text = options.Message;
        DialogDetail.Text = options.Detail;
        DialogDetail.Visibility = string.IsNullOrWhiteSpace(options.Detail)
            ? Visibility.Collapsed
            : Visibility.Visible;
        ConfirmButton.Content = options.ConfirmText;
        CancelButton.Content = options.CancelText;
        ConfirmButton.Style = (Style)FindResource(
            options.IsDestructive ? "DialogDangerButton" : "DialogPrimaryButton");

        if (!options.IsDestructive)
        {
            IconBackground.Background = (System.Windows.Media.Brush)FindResource("Line");
            DialogIcon.Text = "\uE73E";
            DialogIcon.Foreground = (System.Windows.Media.Brush)FindResource("Accent");
        }
    }

    /// <summary>
    /// 以指定窗口为所有者显示模态确认弹窗。
    /// </summary>
    /// <param name="owner">承载弹窗的应用窗口。</param>
    /// <param name="options">弹窗的内容与操作语义配置。</param>
    /// <returns>用户确认时返回 <see langword="true"/>，取消或关闭时返回 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> 或 <paramref name="options"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="InvalidOperationException">窗口当前无法以模态方式显示时抛出。</exception>
    internal static bool ShowFor(Window owner, ConfirmationDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(options);

        var dialog = new ConfirmationDialog(options)
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true;
    }

    /// <summary>
    /// 确认当前操作并关闭弹窗。
    /// </summary>
    /// <param name="sender">触发确认的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    /// <summary>
    /// 取消当前操作并关闭弹窗。
    /// </summary>
    /// <param name="sender">触发取消的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
