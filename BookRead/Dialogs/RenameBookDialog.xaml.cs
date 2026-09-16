using System.Windows;

namespace BookRead.Dialogs;

/// <summary>
/// 提供用于修改书架书籍显示名称的输入弹窗。
/// </summary>
internal partial class RenameBookDialog : Window
{
    /// <summary>
    /// 使用当前书籍名称初始化重命名弹窗。
    /// </summary>
    /// <param name="initialTitle">当前书籍显示名称。</param>
    private RenameBookDialog(string initialTitle)
    {
        InitializeComponent();
        BookTitleTextBox.Text = initialTitle;
    }

    /// <summary>
    /// 获取用户确认后的新书籍名称。
    /// </summary>
    internal string BookTitle { get; private set; } = string.Empty;

    /// <summary>
    /// 以指定窗口为所有者显示模态重命名弹窗。
    /// </summary>
    /// <param name="owner">承载弹窗的应用窗口。</param>
    /// <param name="initialTitle">当前书籍显示名称。</param>
    /// <returns>用户确认时返回去除首尾空白后的名称；取消或关闭时返回 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> 或 <paramref name="initialTitle"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="InvalidOperationException">窗口当前无法以模态方式显示时抛出。</exception>
    internal static string? ShowFor(Window owner, string initialTitle)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(initialTitle);

        var dialog = new RenameBookDialog(initialTitle)
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true ? dialog.BookTitle : null;
    }

    /// <summary>
    /// 弹窗显示后聚焦名称输入框并选中现有名称，便于用户直接替换。
    /// </summary>
    /// <param name="sender">已加载的重命名弹窗。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void RenameBookDialog_Loaded(object sender, RoutedEventArgs e)
    {
        BookTitleTextBox.Focus();
        BookTitleTextBox.SelectAll();
    }

    /// <summary>
    /// 校验并确认新的书籍名称。
    /// </summary>
    /// <param name="sender">触发确认的保存按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        string title = BookTitleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ValidationMessage.Visibility = Visibility.Visible;
            BookTitleTextBox.Focus();
            return;
        }

        BookTitle = title;
        DialogResult = true;
    }

    /// <summary>
    /// 取消重命名并关闭弹窗。
    /// </summary>
    /// <param name="sender">触发取消的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
