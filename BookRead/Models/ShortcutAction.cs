namespace BookRead.Models;

/// <summary>
/// 阅读页支持配置快捷键的操作类型。
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
