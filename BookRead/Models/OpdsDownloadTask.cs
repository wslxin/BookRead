using System.ComponentModel;

namespace BookRead.Models;

/// <summary>
/// 表示 OPDS 下载任务在生命周期内可能处于的状态。
/// </summary>
internal enum OpdsDownloadStatus
{
    /// <summary>正在下载。</summary>
    Downloading,

    /// <summary>下载成功并已加入书架。</summary>
    Completed,

    /// <summary>用户主动取消了下载。</summary>
    Canceled,

    /// <summary>下载失败。</summary>
    Failed
}

/// <summary>
/// 表示一个可跨页面展示的 OPDS 下载任务，向下载详情页提供进度、状态与取消能力。
/// </summary>
internal sealed class OpdsDownloadTask : INotifyPropertyChanged
{
    private int _progress;
    private OpdsDownloadStatus _status = OpdsDownloadStatus.Downloading;
    private string? _failureMessage;

    /// <summary>
    /// 创建下载任务。
    /// </summary>
    /// <param name="title">书籍显示标题。</param>
    /// <param name="sourceName">提供该书籍的书源名称。</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> 或 <paramref name="sourceName"/> 为 <see langword="null"/> 时抛出。</exception>
    public OpdsDownloadTask(string title, string sourceName)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        SourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>获取书籍显示标题。</summary>
    public string Title { get; }

    /// <summary>获取提供该书籍的书源名称。</summary>
    public string SourceName { get; }

    /// <summary>获取任务创建时间。</summary>
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.Now;

    /// <summary>获取下载进度百分比，取值 0 到 100。</summary>
    public int Progress => _progress;

    /// <summary>获取任务当前状态。</summary>
    public OpdsDownloadStatus Status => _status;

    /// <summary>获取一个值，该值指示任务是否仍在进行中。</summary>
    public bool IsActive => _status == OpdsDownloadStatus.Downloading;

    /// <summary>获取一个值，该值指示任务是否下载成功。</summary>
    public bool IsSuccess => _status == OpdsDownloadStatus.Completed;

    /// <summary>获取一个值，该值指示任务是否下载失败。</summary>
    public bool IsFailed => _status == OpdsDownloadStatus.Failed;

    /// <summary>获取任务状态的展示文案。</summary>
    public string StatusText => _status switch
    {
        OpdsDownloadStatus.Downloading => "下载中",
        OpdsDownloadStatus.Completed => "已加入书架",
        OpdsDownloadStatus.Canceled => "已取消",
        _ => string.IsNullOrWhiteSpace(_failureMessage) ? "下载失败" : _failureMessage
    };

    /// <summary>获取任务状态对应的 Segoe Fluent Icons 图标字形。</summary>
    public string StatusIcon => _status switch
    {
        OpdsDownloadStatus.Downloading => "\uE896",
        OpdsDownloadStatus.Completed => "\uE73E",
        OpdsDownloadStatus.Canceled => "\uE711",
        _ => "\uE783"
    };

    /// <summary>获取或设置取消本次下载的回调；未提供时任务不可取消。</summary>
    public Action? CancelRequested { get; set; }

    /// <summary>获取一个值，该值指示当前是否可以取消该任务。</summary>
    public bool CanCancel => IsActive && CancelRequested is not null;

    /// <summary>
    /// 更新下载进度。
    /// </summary>
    /// <param name="progress">最新进度百分比，超出 0 到 100 时会被截断。</param>
    /// <returns>无。</returns>
    public void ReportProgress(int progress)
    {
        int normalized = Math.Clamp(progress, 0, 100);
        if (_progress == normalized)
        {
            return;
        }

        _progress = normalized;
        OnPropertyChanged(nameof(Progress));
    }

    /// <summary>
    /// 把任务标记为下载成功。
    /// </summary>
    /// <returns>无。</returns>
    public void MarkCompleted()
    {
        ReportProgress(100);
        SetStatus(OpdsDownloadStatus.Completed);
    }

    /// <summary>
    /// 把任务标记为已被用户取消。
    /// </summary>
    /// <returns>无。</returns>
    public void MarkCanceled()
    {
        SetStatus(OpdsDownloadStatus.Canceled);
    }

    /// <summary>
    /// 把任务标记为下载失败。
    /// </summary>
    /// <param name="message">可直接展示给用户的失败原因。</param>
    /// <returns>无。</returns>
    public void MarkFailed(string message)
    {
        _failureMessage = message;
        SetStatus(OpdsDownloadStatus.Failed);
    }

    /// <summary>
    /// 请求取消下载；任务已结束时忽略。
    /// </summary>
    /// <returns>无。</returns>
    public void RequestCancel()
    {
        if (!CanCancel)
        {
            return;
        }

        CancelRequested?.Invoke();
    }

    /// <summary>
    /// 更新任务状态并通知界面刷新所有依赖状态的属性。
    /// </summary>
    /// <param name="status">任务的新状态。</param>
    /// <returns>无。</returns>
    private void SetStatus(OpdsDownloadStatus status)
    {
        if (_status == status)
        {
            return;
        }

        _status = status;

        // 卡片模板按状态切换图标、颜色与按钮，因此需要把派生属性一并通知出去。
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusIcon));
    }

    /// <summary>
    /// 触发指定属性的界面更新。
    /// </summary>
    /// <param name="propertyName">发生变化的属性名称。</param>
    /// <returns>无。</returns>
    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
