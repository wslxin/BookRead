using System.Windows;

namespace BookRead.Dialogs;

/// <summary>
/// 提供与应用视觉一致、内容可配置的通用确认弹窗。
/// </summary>
internal partial class ConfirmationDialog : Window
{
    /// <summary>获取用户在弹窗中最终选择的操作。</summary>
    internal ConfirmationDialogResult Result { get; private set; } = ConfirmationDialogResult.Cancel;

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

        // 提示模式没有需要取消的操作，隐藏取消按钮，避免给用户造成两选一的困惑。
        CancelButton.Visibility = options.IsAlert
            ? Visibility.Collapsed
            : Visibility.Visible;
        AlternativeButton.Content = options.AlternativeText ?? string.Empty;
        AlternativeButton.Visibility = string.IsNullOrWhiteSpace(options.AlternativeText) || options.IsAlert
            ? Visibility.Collapsed
            : Visibility.Visible;

        // 危险操作和提示信息都保留警示图标，只有普通确认改为完成图标，避免用户误判操作风险。
        if (!options.IsDestructive && !options.IsAlert)
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
    /// <returns>用户确认时返回 <see cref="ConfirmationDialogResult.Confirm"/>，备选时返回 <see cref="ConfirmationDialogResult.Alternative"/>，取消或关闭时返回 <see cref="ConfirmationDialogResult.Cancel"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> 或 <paramref name="options"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="InvalidOperationException">窗口当前无法以模态方式显示时抛出。</exception>
    internal static ConfirmationDialogResult ShowFor(Window owner, ConfirmationDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(options);

        var dialog = new ConfirmationDialog(options)
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true ? dialog.Result : ConfirmationDialogResult.Cancel;
    }

    /// <summary>
    /// 以指定窗口为所有者显示仅含确定按钮的提示弹窗。
    /// </summary>
    /// <param name="owner">承载弹窗的应用窗口；为 <see langword="null"/> 时不指定所属窗口。</param>
    /// <param name="title">弹窗标题。</param>
    /// <param name="message">要展示的提示内容。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> 或 <paramref name="message"/> 为 <see langword="null"/> 时抛出。</exception>
    internal static void ShowAlert(Window? owner, string title, string message)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        var dialog = new ConfirmationDialog(new ConfirmationDialogOptions(
            title,
            message,
            ConfirmText: "确定",
            IsAlert: true));
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
    }

    /// <summary>
    /// 记录主确认选择并关闭弹窗。
    /// </summary>
    /// <param name="sender">触发确认的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Result = ConfirmationDialogResult.Confirm;
        DialogResult = true;
    }

    /// <summary>
    /// 记录备选按钮选择并关闭弹窗。
    /// </summary>
    /// <param name="sender">触发备选操作的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Alternative_Click(object sender, RoutedEventArgs e)
    {
        Result = ConfirmationDialogResult.Alternative;
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
