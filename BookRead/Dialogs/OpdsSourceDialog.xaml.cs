using System.IO;
using System.Net.Http;
using System.Security;
using System.Windows;
using System.Windows.Input;
using BookRead.Controls;
using BookRead.Models;
using BookRead.Services;

namespace BookRead.Dialogs;

/// <summary>
/// 提供新增和编辑 OPDS 书源的输入弹窗。
/// </summary>
internal partial class OpdsSourceDialog : Window
{
    private readonly OpdsSource _editingSource;
    private readonly bool _isCreating;

    /// <summary>弹窗内状态提示的自动隐藏时长。</summary>
    private static readonly TimeSpan StatusAutoHideDuration = TimeSpan.FromSeconds(3);

    /// <summary>获取用户确认后的书源。</summary>
    internal OpdsSource Source { get; private set; } = new();

    /// <summary>
    /// 使用编辑模式初始化弹窗。
    /// </summary>
    /// <param name="source">要编辑的书源副本。</param>
    /// <param name="isCreating">是否为新增书源。</param>
    private OpdsSourceDialog(OpdsSource source, bool isCreating)
    {
        InitializeComponent();
        _editingSource = source;
        _isCreating = isCreating;
        DialogTitle.Text = isCreating ? "添加 OPDS 书源" : "编辑 OPDS 书源";
        NameTextBox.Text = source.Name;
        UrlTextBox.Text = source.Url;
        UsernameTextBox.Text = source.Username;
        IgnoreCertificateCheckBox.IsChecked = source.IgnoreCertificateErrors;
    }

    /// <summary>
    /// 以新增模式显示弹窗。
    /// </summary>
    /// <param name="owner">承载弹窗的应用窗口。</param>
    /// <returns>用户确认时返回书源；取消时返回 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="InvalidOperationException">窗口当前无法以模态方式显示时抛出。</exception>
    internal static Task<OpdsSource?> ShowForCreateAsync(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return Task.FromResult(ShowInternal(owner, new OpdsSource(), isCreating: true));
    }

    /// <summary>
    /// 以编辑模式显示弹窗。
    /// </summary>
    /// <param name="owner">承载弹窗的应用窗口。</param>
    /// <param name="source">要编辑的书源副本。</param>
    /// <returns>用户确认时返回书源；取消时返回 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException">任一参数为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="InvalidOperationException">窗口当前无法以模态方式显示时抛出。</exception>
    internal static Task<OpdsSource?> ShowForEditAsync(Window owner, OpdsSource source)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(source);
        return Task.FromResult(ShowInternal(owner, source, isCreating: false));
    }

    /// <summary>
    /// 显示弹窗并执行保存前验证。
    /// </summary>
    /// <param name="owner">承载弹窗的应用窗口。</param>
    /// <param name="source">要编辑的书源副本。</param>
    /// <param name="isCreating">是否为新增书源。</param>
    /// <returns>用户确认时返回书源；取消时返回 <see langword="null"/>。</returns>
    private static OpdsSource? ShowInternal(Window owner, OpdsSource source, bool isCreating)
    {
        var dialog = new OpdsSourceDialog(source, isCreating)
        {
            Owner = owner
        };
        bool? result = null;
        try
        {
            result = dialog.ShowDialog();
        }
        catch (Exception exception)
        {
            MessageBox.Show(owner, $"打开书源弹窗失败：{exception}", "BookRead", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        if (result != true)
        {
            return null;
        }

        try
        {
            dialog.Source = dialog.BuildSource();
        }
        catch (Exception exception)
        {
            MessageBox.Show(owner, $"读取书源输入失败：{exception}", "BookRead", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        // 保存时不再阻塞验证网络；连接状态由用户主动点“测试连接”检查。
        return dialog.Source;
    }



    /// <summary>
    /// 根据输入构建书源。
    /// </summary>
    /// <returns>配置完成的书源。</returns>
    private OpdsSource BuildSource()
    {
        return new OpdsSource
        {
            Id = _editingSource.Id,
            Name = NameTextBox.Text.Trim(),
            Url = UrlTextBox.Text.Trim(),
            Username = UsernameTextBox.Text.Trim(),
            EncryptedPassword = PasswordProtector.Protect(PasswordBox.Password),
            IgnoreCertificateErrors = IgnoreCertificateCheckBox.IsChecked == true,
            AddedAt = _editingSource.AddedAt
        };
    }

    /// <summary>
    /// 校验输入；验证失败时显示错误并阻止关闭。
    /// </summary>
    /// <param name="sender">触发确认的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            ShowStatus("请输入书源名称。", InlineNotificationType.Error);
            return;
        }

        if (!Uri.TryCreate(UrlTextBox.Text.Trim(), UriKind.Absolute, out Uri? url) ||
            url.Scheme is not ("http" or "https"))
        {
            ShowStatus("请输入有效的 http:// 或 https:// 书源地址。", InlineNotificationType.Error);
            return;
        }

        DialogResult = true;
    }

    /// <summary>
    /// 手动测试书源连接。
    /// </summary>
    /// <param name="sender">触发测试的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(UrlTextBox.Text.Trim(), UriKind.Absolute, out Uri? url) ||
            url.Scheme is not ("http" or "https"))
        {
            ShowStatus("请输入有效的 http:// 或 https:// 书源地址。", InlineNotificationType.Error);
            return;
        }

        try
        {
            TestButton.IsEnabled = false;
            TestButton.Content = "测试中…";
            ShowStatus("正在连接书源…", InlineNotificationType.Info);
            OpdsSource temporarySource = new()
            {
                Name = NameTextBox.Text,
                Url = UrlTextBox.Text.Trim(),
                Username = UsernameTextBox.Text.Trim(),
                EncryptedPassword = PasswordProtector.Protect(PasswordBox.Password),
                IgnoreCertificateErrors = IgnoreCertificateCheckBox.IsChecked == true
            };

            // 测试与保存验证都使用同一请求配置，避免手动测试结果与最终保存行为不一致。
            string status = await Task.Run(async () =>
            {
                try
                {
                    using var client = OpdsDownloadService.CreateHttpClient(temporarySource);
                    using var request = new HttpRequestMessage(HttpMethod.Get, temporarySource.Url);
                    request.Headers.Accept.ParseAdd("application/atom+xml, application/xml, */*");
                    using HttpResponseMessage response = await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead);
                    return response.IsSuccessStatusCode
                        ? "连接成功。"
                        : $"连接失败：{(int)response.StatusCode} {response.ReasonPhrase}";
                }
                catch (HttpRequestException exception)
                {
                    return $"连接失败：{exception.Message}";
                }
                catch (IOException exception)
                {
                    return $"连接失败：{exception.Message}";
                }
                catch (TaskCanceledException)
                {
                    return "连接失败：请求超时。";
                }
                catch (Exception exception)
                {
                    return $"连接失败：{exception.Message}";
                }
            });

            ShowStatus(status, status == "连接成功。" ? InlineNotificationType.Success : InlineNotificationType.Error);
        }
        finally
        {
            TestButton.IsEnabled = true;
            TestButton.Content = "测试连接";
        }
    }

    /// <summary>
    /// 在弹窗底部显示测试或校验状态。
    /// </summary>
    /// <param name="message">要显示的状态内容。</param>
    /// <param name="type">状态类型，用于选择图标和颜色。</param>
    /// <returns>无。</returns>
    private void ShowStatus(string message, InlineNotificationType type)
    {
        // 状态提示使用更长展示时间，避免用户还没看清结果就自动消失。
        StatusNotification.Show(message, type, StatusAutoHideDuration);
    }

    /// <summary>
    /// 鼠标悬浮在状态提示上时暂停自动隐藏。
    /// </summary>
    /// <param name="sender">触发事件的提示控件。</param>
    /// <param name="e">鼠标事件参数。</param>
    /// <returns>无。</returns>
    private void StatusNotification_MouseEnter(object sender, MouseEventArgs e)
    {
        StatusNotification.SuspendAutoHide();
    }

    /// <summary>
    /// 鼠标离开状态提示后恢复自动隐藏计时。
    /// </summary>
    /// <param name="sender">触发事件的提示控件。</param>
    /// <param name="e">鼠标事件参数。</param>
    /// <returns>无。</returns>
    private void StatusNotification_MouseLeave(object sender, MouseEventArgs e)
    {
        StatusNotification.ResumeAutoHide();
    }

    /// <summary>
    /// 取消编辑并关闭弹窗。
    /// </summary>
    /// <param name="sender">触发取消的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
