using System.Windows;

namespace BookRead;

/// <summary>
/// BookRead 的自定义系统托盘菜单窗口，负责提供统一风格的窗口恢复和退出入口。
/// </summary>
public partial class TrayMenuWindow : Window
{
    /// <summary>用户请求恢复主窗口时触发。</summary>
    public event EventHandler? OpenRequested;

    /// <summary>用户请求退出应用时触发。</summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// 初始化自定义托盘菜单窗口。
    /// </summary>
    public TrayMenuWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 转发用户的打开请求，由主窗口负责恢复应用。
    /// </summary>
    /// <param name="sender">打开按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 转发用户的退出请求，由主窗口负责结束应用。
    /// </summary>
    /// <param name="sender">退出按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}
