using System.Windows.Input;

namespace BookRead.Models;

/// <summary>
/// 保存应用的快捷键、界面及窗口行为配置。
/// </summary>
internal sealed class ShortcutSettings
{
    /// <summary>
    /// 初始化应用设置。
    /// </summary>
    public ShortcutSettings()
    {
    }

    /// <summary>是否显示阅读页底部工具栏。</summary>
    public bool ShowReaderToolbar { get; set; } = true;

    /// <summary>点击关闭按钮时是否最小化到系统托盘。</summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>下载完成并加入书架后是否自动打开书籍开始阅读。</summary>
    public bool OpenAfterDownload { get; set; }

    /// <summary>下一页快捷键。</summary>
    public ShortcutBinding NextPage { get; set; } = new(Key.Right, ModifierKeys.None);

    /// <summary>上一页快捷键。</summary>
    public ShortcutBinding PreviousPage { get; set; } = new(Key.Left, ModifierKeys.None);

    /// <summary>下一章快捷键。</summary>
    public ShortcutBinding NextChapter { get; set; } = new(Key.Right, ModifierKeys.Control);

    /// <summary>上一章快捷键。</summary>
    public ShortcutBinding PreviousChapter { get; set; } = new(Key.Left, ModifierKeys.Control);

    /// <summary>向下滚动快捷键。</summary>
    public ShortcutBinding ScrollDown { get; set; } = new(Key.Down, ModifierKeys.None);

    /// <summary>向上滚动快捷键。</summary>
    public ShortcutBinding ScrollUp { get; set; } = new(Key.Up, ModifierKeys.None);

    /// <summary>增大字号快捷键。</summary>
    public ShortcutBinding IncreaseFont { get; set; } = new(Key.Add, ModifierKeys.None);

    /// <summary>减小字号快捷键。</summary>
    public ShortcutBinding DecreaseFont { get; set; } = new(Key.Subtract, ModifierKeys.None);

    /// <summary>增大行间距快捷键。</summary>
    public ShortcutBinding IncreaseLineSpacing { get; set; } = new(Key.Add, ModifierKeys.Control);

    /// <summary>减小行间距快捷键。</summary>
    public ShortcutBinding DecreaseLineSpacing { get; set; } = new(Key.Subtract, ModifierKeys.Control);

    /// <summary>切换透明背景快捷键。</summary>
    public ShortcutBinding ToggleTransparency { get; set; } = new(Key.T, ModifierKeys.Control);

    /// <summary>切换阅读主题快捷键。</summary>
    public ShortcutBinding ToggleTheme { get; set; } = new(Key.T, ModifierKeys.Control | ModifierKeys.Shift);

    /// <summary>切换章节面板快捷键。</summary>
    public ShortcutBinding ToggleChapter { get; set; } = new(Key.L, ModifierKeys.Control);

    /// <summary>
    /// 创建默认阅读页配置。
    /// </summary>
    /// <returns>包含应用默认阅读页配置的对象。</returns>
    public static ShortcutSettings CreateDefault()
    {
        return new ShortcutSettings();
    }

    /// <summary>
    /// 创建当前阅读页配置的独立副本，供设置页编辑。
    /// </summary>
    /// <returns>当前配置的深拷贝。</returns>
    public ShortcutSettings Clone()
    {
        return new ShortcutSettings
        {
            ShowReaderToolbar = ShowReaderToolbar,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose,
            OpenAfterDownload = OpenAfterDownload,
            NextPage = NextPage,
            PreviousPage = PreviousPage,
            NextChapter = NextChapter,
            PreviousChapter = PreviousChapter,
            ScrollDown = ScrollDown,
            ScrollUp = ScrollUp,
            IncreaseFont = IncreaseFont,
            DecreaseFont = DecreaseFont,
            IncreaseLineSpacing = IncreaseLineSpacing,
            DecreaseLineSpacing = DecreaseLineSpacing,
            ToggleTransparency = ToggleTransparency,
            ToggleTheme = ToggleTheme,
            ToggleChapter = ToggleChapter
        };
    }

    /// <summary>
    /// 获取指定操作对应的快捷键。
    /// </summary>
    /// <param name="action">要查询的阅读操作。</param>
    /// <returns>指定操作的快捷键配置。</returns>
    /// <exception cref="ArgumentOutOfRangeException">操作类型不受支持时抛出。</exception>
    public ShortcutBinding GetBinding(ShortcutAction action)
    {
        return action switch
        {
            ShortcutAction.NextPage => NextPage,
            ShortcutAction.PreviousPage => PreviousPage,
            ShortcutAction.NextChapter => NextChapter,
            ShortcutAction.PreviousChapter => PreviousChapter,
            ShortcutAction.ScrollDown => ScrollDown,
            ShortcutAction.ScrollUp => ScrollUp,
            ShortcutAction.IncreaseFont => IncreaseFont,
            ShortcutAction.DecreaseFont => DecreaseFont,
            ShortcutAction.IncreaseLineSpacing => IncreaseLineSpacing,
            ShortcutAction.DecreaseLineSpacing => DecreaseLineSpacing,
            ShortcutAction.ToggleTransparency => ToggleTransparency,
            ShortcutAction.ToggleTheme => ToggleTheme,
            ShortcutAction.ToggleChapter => ToggleChapter,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "不支持的快捷键操作。")
        };
    }

    /// <summary>
    /// 更新指定操作对应的快捷键。
    /// </summary>
    /// <param name="action">要更新的阅读操作。</param>
    /// <param name="binding">新的快捷键配置。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentOutOfRangeException">操作类型不受支持时抛出。</exception>
    public void SetBinding(ShortcutAction action, ShortcutBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        switch (action)
        {
            case ShortcutAction.NextPage:
                NextPage = binding;
                break;
            case ShortcutAction.PreviousPage:
                PreviousPage = binding;
                break;
            case ShortcutAction.NextChapter:
                NextChapter = binding;
                break;
            case ShortcutAction.PreviousChapter:
                PreviousChapter = binding;
                break;
            case ShortcutAction.ScrollDown:
                ScrollDown = binding;
                break;
            case ShortcutAction.ScrollUp:
                ScrollUp = binding;
                break;
            case ShortcutAction.IncreaseFont:
                IncreaseFont = binding;
                break;
            case ShortcutAction.DecreaseFont:
                DecreaseFont = binding;
                break;
            case ShortcutAction.IncreaseLineSpacing:
                IncreaseLineSpacing = binding;
                break;
            case ShortcutAction.DecreaseLineSpacing:
                DecreaseLineSpacing = binding;
                break;
            case ShortcutAction.ToggleTransparency:
                ToggleTransparency = binding;
                break;
            case ShortcutAction.ToggleTheme:
                ToggleTheme = binding;
                break;
            case ShortcutAction.ToggleChapter:
                ToggleChapter = binding;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "不支持的快捷键操作。");
        }
    }
}
