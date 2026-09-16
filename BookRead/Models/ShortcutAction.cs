namespace BookRead.Models;

/// <summary>
/// 应用支持配置快捷键的操作类型，包含全局操作和阅读页操作。
/// </summary>
internal enum ShortcutAction
{
    NextPage,
    PreviousPage,
    NextChapter,
    PreviousChapter,
    ScrollDown,
    ScrollUp,
    IncreaseFont,
    DecreaseFont,
    IncreaseLineSpacing,
    DecreaseLineSpacing,
    ToggleTransparency,
    ToggleTheme,
    ToggleChapter
}
