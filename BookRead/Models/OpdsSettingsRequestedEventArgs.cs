namespace BookRead.Models;

/// <summary>
/// 表示 OPDS 浏览页请求打开书源配置页的事件。
/// </summary>
internal sealed class OpdsSettingsRequestedEventArgs : EventArgs
{
    /// <summary>使用要编辑的书源初始化事件。</summary>
    /// <param name="source">要编辑的书源。</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/> 时抛出。</exception>
    public OpdsSettingsRequestedEventArgs(OpdsSource source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>获取要编辑的书源。</summary>
    public OpdsSource Source { get; }
}
