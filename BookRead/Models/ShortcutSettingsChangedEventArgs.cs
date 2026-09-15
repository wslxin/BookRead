namespace BookRead.Models;

/// <summary>
/// 包含已保存阅读页设置的事件参数。
/// </summary>
internal sealed class ShortcutSettingsChangedEventArgs : EventArgs
{
    /// <summary>
    /// 初始化阅读页设置变更事件参数。
    /// </summary>
    /// <param name="settings">已保存的阅读页配置。</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> 为 null 时抛出。</exception>
    public ShortcutSettingsChangedEventArgs(ShortcutSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>已保存的阅读页配置。</summary>
    public ShortcutSettings Settings { get; }
}
