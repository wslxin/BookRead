using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BookRead.Models;

namespace BookRead.Pages;

/// <summary>
/// 设置页面，负责编辑应用的快捷键、界面显示和窗口行为配置并提交配置。
/// </summary>
public partial class SettingsPage : UserControl
{
    private ShortcutSettings _editingSettings = ShortcutSettings.CreateDefault();

    /// <summary>阅读页设置保存后触发。</summary>
    internal event EventHandler<ShortcutSettingsChangedEventArgs>? SettingsSaved;

    /// <summary>设置页请求返回上一页时触发。</summary>
    internal event RoutedEventHandler? BackRequested;

    /// <summary>
    /// 初始化设置页面并载入默认阅读页配置显示值。
    /// </summary>
    public SettingsPage()
    {
        InitializeComponent();
        SelectSettingsTab(showShortcuts: true);
        UpdateShortcutTextBoxes();
    }

    /// <summary>
    /// 载入应用设置，覆盖当前页面中的待编辑副本。
    /// </summary>
    /// <param name="settings">要载入的应用设置。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> 为 null 时抛出。</exception>
    internal void LoadSettings(ShortcutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _editingSettings = settings.Clone();
        SelectSettingsTab(showShortcuts: true);
        ShowReaderToolbarCheckBox.IsChecked = _editingSettings.ShowReaderToolbar;
        MinimizeToTrayOnCloseCheckBox.IsChecked = _editingSettings.MinimizeToTrayOnClose;
        UpdateShortcutTextBoxes();
    }

    /// <summary>
    /// 响应左侧设置导航项点击，并切换右侧显示的设置内容。
    /// </summary>
    /// <param name="sender">触发切换的左侧导航按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void SettingsTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        bool showShortcuts = string.Equals(
            button.Tag?.ToString(),
            "Shortcuts",
            StringComparison.Ordinal);
        SelectSettingsTab(showShortcuts);
    }

    /// <summary>
    /// 更新左侧导航按钮的选中状态，并显示对应的设置面板。
    /// </summary>
    /// <param name="showShortcuts">为 true 时显示快捷键面板，否则显示显示选项面板。</param>
    /// <returns>无。</returns>
    private void SelectSettingsTab(bool showShortcuts)
    {
        ShortcutSettingsPanel.Visibility = showShortcuts
            ? Visibility.Visible
            : Visibility.Collapsed;
        DisplaySettingsPanel.Visibility = showShortcuts
            ? Visibility.Collapsed
            : Visibility.Visible;
        SettingsSectionTitle.Text = showShortcuts ? "快捷键" : "显示";
        UpdateSettingsNavigationButton(ShortcutTabButton, showShortcuts);
        UpdateSettingsNavigationButton(DisplayTabButton, !showShortcuts);
    }

    /// <summary>
    /// 应用左侧导航按钮的选中或未选中视觉状态。
    /// </summary>
    /// <param name="button">要更新状态的导航按钮。</param>
    /// <param name="isSelected">按钮是否处于选中状态。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="button"/> 为 null 时抛出。</exception>
    private static void UpdateSettingsNavigationButton(Button button, bool isSelected)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (isSelected)
        {
            button.SetResourceReference(Control.BackgroundProperty, "AccentSurface");
            button.SetResourceReference(Control.ForegroundProperty, "Accent");
            button.FontWeight = FontWeights.SemiBold;
            return;
        }

        button.ClearValue(Control.BackgroundProperty);
        button.ClearValue(Control.ForegroundProperty);
        button.FontWeight = FontWeights.Normal;
    }

    /// <summary>
    /// 捕获输入框中按下的按键组合并写入当前待编辑配置。
    /// </summary>
    /// <param name="sender">接收快捷键的只读文本框。</param>
    /// <param name="e">按键事件参数。</param>
    /// <returns>无。</returns>
    private void ShortcutTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || !Enum.TryParse(textBox.Tag?.ToString(), out ShortcutAction action))
        {
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Delete or Key.Back)
        {
            _editingSettings.SetBinding(action, new ShortcutBinding(Key.None, ModifierKeys.None));
            textBox.Text = "未设置";
            e.Handled = true;
            return;
        }

        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        _editingSettings.SetBinding(action, new ShortcutBinding(key, Keyboard.Modifiers));
        textBox.Text = FormatBinding(_editingSettings.GetBinding(action));
        e.Handled = true;
    }

    /// <summary>
    /// 取消当前修改并请求返回上一页。
    /// </summary>
    /// <param name="sender">触发取消操作的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        RequestBack();
    }

    /// <summary>
    /// 校验并提交当前应用设置修改。
    /// </summary>
    /// <param name="sender">触发保存操作的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _editingSettings.ShowReaderToolbar = ShowReaderToolbarCheckBox.IsChecked == true;
        _editingSettings.MinimizeToTrayOnClose = MinimizeToTrayOnCloseCheckBox.IsChecked == true;
        ShortcutAction[] actions = Enum.GetValues<ShortcutAction>();
        var usedBindings = new Dictionary<ShortcutBinding, ShortcutAction>();
        foreach (ShortcutAction action in actions)
        {
            ShortcutBinding binding = _editingSettings.GetBinding(action);
            if (binding.Key == Key.None)
            {
                continue;
            }

            if (usedBindings.TryGetValue(binding, out ShortcutAction existingAction))
            {
                MessageBox.Show(
                    Window.GetWindow(this),
                    $"“{GetActionName(existingAction)}”和“{GetActionName(action)}”不能使用相同快捷键。",
                    "快捷键冲突",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            usedBindings[binding] = action;
        }

        SettingsSaved?.Invoke(this, new ShortcutSettingsChangedEventArgs(_editingSettings.Clone()));
        RequestBack();
    }

    /// <summary>
    /// 更新设置页中所有快捷键文本框的显示内容。
    /// </summary>
    /// <returns>无。</returns>
    private void UpdateShortcutTextBoxes()
    {
        foreach ((TextBox textBox, ShortcutAction action) in GetShortcutTextBoxes())
        {
            textBox.Text = FormatBinding(_editingSettings.GetBinding(action));
        }
    }

    /// <summary>
    /// 请求主窗口返回设置页打开前的页面。
    /// </summary>
    /// <returns>无。</returns>
    private void RequestBack()
    {
        BackRequested?.Invoke(this, new RoutedEventArgs());
    }

    /// <summary>
    /// 将快捷键绑定格式化为适合在设置页显示的文本。
    /// </summary>
    /// <param name="binding">要格式化的快捷键绑定。</param>
    /// <returns>格式化后的快捷键文本。</returns>
    private static string FormatBinding(ShortcutBinding binding)
    {
        if (binding.Key == Key.None)
        {
            return "未设置";
        }

        var parts = new List<string>();
        if (binding.Modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (binding.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (binding.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        parts.Add(binding.Key switch
        {
            Key.Add => "+",
            Key.Subtract => "-",
            Key.PageDown => "PageDown",
            Key.PageUp => "PageUp",
            Key.Space => "Space",
            Key.Return => "Enter",
            _ => binding.Key.ToString()
        });
        return string.Join(" + ", parts);
    }

    /// <summary>
    /// 判断指定按键是否只是修饰键本身。
    /// </summary>
    /// <param name="key">待判断的按键。</param>
    /// <returns>按键为修饰键时返回 true，否则返回 false。</returns>
    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin;
    }

    /// <summary>
    /// 返回设置页中快捷键文本框与操作类型的对应关系。
    /// </summary>
    /// <returns>快捷键文本框和操作类型的枚举集合。</returns>
    private IEnumerable<(TextBox TextBox, ShortcutAction Action)> GetShortcutTextBoxes()
    {
        yield return (NextPageTextBox, ShortcutAction.NextPage);
        yield return (PreviousPageTextBox, ShortcutAction.PreviousPage);
        yield return (NextChapterTextBox, ShortcutAction.NextChapter);
        yield return (PreviousChapterTextBox, ShortcutAction.PreviousChapter);
        yield return (ScrollDownTextBox, ShortcutAction.ScrollDown);
        yield return (ScrollUpTextBox, ShortcutAction.ScrollUp);
        yield return (IncreaseFontTextBox, ShortcutAction.IncreaseFont);
        yield return (DecreaseFontTextBox, ShortcutAction.DecreaseFont);
        yield return (IncreaseLineSpacingTextBox, ShortcutAction.IncreaseLineSpacing);
        yield return (DecreaseLineSpacingTextBox, ShortcutAction.DecreaseLineSpacing);
        yield return (ToggleTransparencyTextBox, ShortcutAction.ToggleTransparency);
        yield return (ToggleThemeTextBox, ShortcutAction.ToggleTheme);
        yield return (ToggleChapterTextBox, ShortcutAction.ToggleChapter);
    }

    /// <summary>
    /// 获取快捷键操作的中文名称。
    /// </summary>
    /// <param name="action">要获取名称的操作。</param>
    /// <returns>操作中文名称。</returns>
    private static string GetActionName(ShortcutAction action)
    {
        return action switch
        {
            ShortcutAction.NextPage => "下一页",
            ShortcutAction.PreviousPage => "上一页",
            ShortcutAction.NextChapter => "下一章",
            ShortcutAction.PreviousChapter => "上一章",
            ShortcutAction.ScrollDown => "向下滚动",
            ShortcutAction.ScrollUp => "向上滚动",
            ShortcutAction.IncreaseFont => "增大字号",
            ShortcutAction.DecreaseFont => "减小字号",
            ShortcutAction.IncreaseLineSpacing => "增大行间距",
            ShortcutAction.DecreaseLineSpacing => "减小行间距",
            ShortcutAction.ToggleTransparency => "透明背景",
            ShortcutAction.ToggleTheme => "切换主题",
            ShortcutAction.ToggleChapter => "章节面板",
            _ => action.ToString()
        };
    }
}
