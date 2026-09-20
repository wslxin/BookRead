using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using BookRead.Models;

namespace BookRead.Pages;

/// <summary>
/// 下载详情页面，集中展示正在进行的下载任务与已经结束的下载记录。
/// </summary>
public partial class DownloadDetailPage : UserControl
{
    private ObservableCollection<OpdsDownloadTask>? _activeTasks;
    private ObservableCollection<OpdsDownloadTask>? _completedTasks;

    /// <summary>
    /// 初始化下载详情页面。
    /// </summary>
    public DownloadDetailPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 绑定下载任务集合，重复调用会切换到新的集合。
    /// </summary>
    /// <param name="activeTasks">正在进行的下载任务集合，最新的任务排在前面。</param>
    /// <param name="completedTasks">已结束的下载任务集合，最新的任务排在前面。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentNullException">任一集合为 <see langword="null"/> 时抛出。</exception>
    internal void SetTasks(ObservableCollection<OpdsDownloadTask> activeTasks, ObservableCollection<OpdsDownloadTask> completedTasks)
    {
        ArgumentNullException.ThrowIfNull(activeTasks);
        ArgumentNullException.ThrowIfNull(completedTasks);

        // 解绑旧集合，避免页面重复绑定后收到重复通知。
        if (_activeTasks is not null)
        {
            _activeTasks.CollectionChanged -= Tasks_CollectionChanged;
        }

        if (_completedTasks is not null)
        {
            _completedTasks.CollectionChanged -= Tasks_CollectionChanged;
        }

        _activeTasks = activeTasks;
        _completedTasks = completedTasks;
        ActiveTaskList.ItemsSource = activeTasks;
        CompletedTaskList.ItemsSource = completedTasks;
        activeTasks.CollectionChanged += Tasks_CollectionChanged;
        completedTasks.CollectionChanged += Tasks_CollectionChanged;
        UpdateSections();
    }

    /// <summary>
    /// 响应任务集合增删并刷新两个分区的显示状态。
    /// </summary>
    /// <param name="sender">发生变化的集合。</param>
    /// <param name="e">集合变化事件参数。</param>
    /// <returns>无。</returns>
    private void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateSections();
    }

    /// <summary>
    /// 根据任务数量刷新分区可见性、统计文本与分区间距。
    /// </summary>
    /// <returns>无。</returns>
    private void UpdateSections()
    {
        int activeCount = _activeTasks?.Count ?? 0;
        int completedCount = _completedTasks?.Count ?? 0;

        ActiveSection.Visibility = activeCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        CompletedSection.Visibility = completedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = activeCount == 0 && completedCount == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        // 上方的“正在下载”分区隐藏时不能再保留分区间距，否则页面顶部会出现多余空白。
        CompletedSection.Margin = activeCount > 0 ? new Thickness(0, 22, 0, 0) : new Thickness(0);

        ActiveHeaderText.Text = $"正在下载 · {activeCount} 项";
        CompletedHeaderText.Text = $"下载记录 · {completedCount} 项";
    }

    /// <summary>
    /// 清除全部已结束的下载记录。
    /// </summary>
    /// <param name="sender">触发清除的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void ClearCompleted_Click(object sender, RoutedEventArgs e)
    {
        _completedTasks?.Clear();
    }

    /// <summary>
    /// 取消指定任务对应的下载。
    /// </summary>
    /// <param name="sender">携带任务对象的取消按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void CancelTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OpdsDownloadTask task })
        {
            task.RequestCancel();
        }
    }
}
