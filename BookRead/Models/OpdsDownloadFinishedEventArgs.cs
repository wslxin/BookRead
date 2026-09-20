namespace BookRead.Models;

/// <summary>
/// 为浏览页报告的下载结果事件提供数据。
/// </summary>
internal sealed class OpdsDownloadFinishedEventArgs : EventArgs
{
    /// <summary>使用结果消息和结束状态初始化事件。</summary>
    /// <param name="message">可直接展示给用户的下载结果消息。</param>
    /// <param name="status">下载结束时所处的状态。</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> 为 <see langword="null"/> 时抛出。</exception>
    public OpdsDownloadFinishedEventArgs(string message, OpdsDownloadStatus status)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Status = status;
    }

    /// <summary>获取可直接展示给用户的下载结果消息。</summary>
    public string Message { get; }

    /// <summary>获取下载结束时所处的状态。</summary>
    public OpdsDownloadStatus Status { get; }
}
