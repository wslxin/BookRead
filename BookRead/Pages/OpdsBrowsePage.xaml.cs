using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BookRead.Data;
using BookRead.Dialogs;
using BookRead.Models;
using BookRead.Services;

namespace BookRead.Pages;

/// <summary>
/// OPDS 浏览页面，负责书源选择、目录导航、搜索、封面展示与下载请求。
/// </summary>
public partial class OpdsBrowsePage : UserControl
{
    private readonly OpdsSourceStore _sourceStore = new();
    private readonly OpdsClient _opdsClient;
    private readonly CoverImageCache _coverCache;
    private readonly List<OpdsSource> _sources = [];
    private readonly List<string> _navigationHistory = [];
    private readonly ObservableCollection<OpdsEntryViewModel> _entries = [];
    private OpdsSource? _selectedSource;
    private string? _currentPageUrl;
    private string? _nextPageUrl;
    private string? _searchTemplateUrl;
    private string? _searchUrl;
    private bool _isNavigating;
    private readonly Dictionary<string, CancellationTokenSource> _downloadCancellations = new(StringComparer.Ordinal);

    /// <summary>浏览页请求返回书架时触发。</summary>
    internal event RoutedEventHandler? BackToShelfRequested;

    /// <summary>浏览页请求打开 OPDS 书源设置时触发。</summary>
    internal event EventHandler? SettingsRequested;

    /// <summary>浏览页完成下载并请求将书籍加入书架时触发。</summary>
    internal event EventHandler<OpdsBookAddedEventArgs>? BookAdded;

    internal Func<OpdsSource, OpdsEntry, ShelfBook?> FindExistingOpdsBook = (_, _) => null;

    /// <summary>
    /// 初始化 OPDS 浏览页面。
    /// </summary>
    public OpdsBrowsePage()
    {
        InitializeComponent();
        EntryList.ItemsSource = _entries;
        using var httpClient = OpdsDownloadService.CreateHttpClient();
        _opdsClient = new OpdsClient(httpClient);
        _coverCache = new CoverImageCache(httpClient);
    }

    /// <summary>
    /// 重新读取书源配置并显示书架页可直接浏览的第一层目录。
    /// </summary>
    /// <returns>无。</returns>
    public void LoadSources()
    {
        try
        {
            _sources.Clear();
            _sources.AddRange(_sourceStore.Load()
                .OrderBy(source => source.Name, StringComparer.CurrentCulture));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            ConfirmationDialog.ShowAlert(
                Window.GetWindow(this),
                "BookRead",
                $"OPDS 书源配置读取失败：{exception.Message}");
            return;
        }

        SourceComboBox.ItemsSource = _sources;
        SourceComboBox.DisplayMemberPath = nameof(OpdsSource.Name);
        if (_selectedSource is null || !_sources.Any(source => source.Id == _selectedSource.Id))
        {
            _selectedSource = _sources.FirstOrDefault();
            SourceComboBox.SelectedItem = _selectedSource;
        }
        else
        {
            SourceComboBox.SelectedItem = _sources.FirstOrDefault(source => source.Id == _selectedSource.Id);
        }

        if (_selectedSource is null)
        {
            StatusText.Text = "还没有 OPDS 书源。请打开设置页添加一个书源。";
            StatusText.Visibility = Visibility.Visible;
            EntryList.ItemsSource = null;
            PageTitleText.Text = "OPDS 书源";
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentPageUrl) || !_sources.Any(source => source.Id == _selectedSource.Id))
        {
            ResetNavigation();
            _ = LoadPageAsync(null);
        }
    }

    /// <summary>
    /// 将书源选择器重置为指定书源并加载根目录。
    /// </summary>
    /// <param name="source">要浏览的书源。</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/> 时抛出。</exception>
    internal void OpenSource(OpdsSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        LoadSources();
        _selectedSource = _sources.FirstOrDefault(item => item.Id == source.Id);
        SourceComboBox.SelectedItem = _selectedSource;
        ResetNavigation();
        if (_selectedSource is not null)
        {
            _ = LoadPageAsync(null);
        }
    }

    /// <summary>
    /// 保留浏览页所有导航状态，供从阅读页返回时恢复。
    /// </summary>
    /// <returns>无。</returns>
    public void PreserveState()
    {
    }

    /// <summary>
    /// 清空导航历史并返回书源根目录。
    /// </summary>
    /// <returns>无。</returns>
    private void ResetNavigation()
    {
        _navigationHistory.Clear();
        _currentPageUrl = null;
        _searchUrl = null;
        BackLevelButton.IsEnabled = false;
        SearchBackButton.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 请求返回书架页。
    /// </summary>
    /// <param name="sender">触发请求的返回按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void BackToShelf_Click(object sender, RoutedEventArgs e)
    {
        BackToShelfRequested?.Invoke(this, e);
    }

    /// <summary>
    /// 请求打开书源设置页。
    /// </summary>
    /// <param name="sender">触发请求的设置按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void ManageSources_Click(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, e);
    }

    /// <summary>
    /// 刷新当前目录。
    /// </summary>
    /// <param name="sender">触发请求的刷新按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _ = LoadPageAsync(_currentPageUrl, append: false);
    }

    /// <summary>
    /// 响应书源切换并加载根目录。
    /// </summary>
    /// <param name="sender">触发切换的下拉框。</param>
    /// <param name="e">选择变化事件参数。</param>
    /// <returns>无。</returns>
    private void SourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Equals(_selectedSource, SourceComboBox.SelectedItem))
        {
            return;
        }

        _selectedSource = SourceComboBox.SelectedItem as OpdsSource;
        ResetNavigation();
        if (_selectedSource is not null)
        {
            _ = LoadPageAsync(null);
        }
    }

    /// <summary>
    /// 响应返回上一级请求，并保留上一级目录状态。
    /// </summary>
    /// <param name="sender">触发请求的上一级按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void BackLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_navigationHistory.Count == 0)
        {
            BackLevelButton.IsEnabled = false;
            return;
        }

        string previousUrl = _navigationHistory[^1];
        _navigationHistory.RemoveAt(_navigationHistory.Count - 1);
        _searchUrl = null;
        SearchBackButton.Visibility = Visibility.Collapsed;
        _currentPageUrl = previousUrl;
        _ = LoadPageAsync(previousUrl, preserveHistory: false);
    }

    /// <summary>
    /// 加载并渲染当前页面，可选择追加分页结果。
    /// </summary>
    /// <param name="url">目标目录地址；为空时使用当前书源根地址。</param>
    /// <param name="append">是否保留现有条目并追加结果。</param>
    /// <param name="preserveHistory">是否保留当前导航历史；返回上一级时为 false。</param>
    /// <returns>表示异步加载过程的任务。</returns>
    private async Task LoadPageAsync(
        string? url,
        bool append = false,
        bool preserveHistory = true)
    {
        if (_selectedSource is null || _isNavigating)
        {
            return;
        }

        _isNavigating = true;
        LoadMoreButton.Visibility = Visibility.Collapsed;
        StatusText.Visibility = Visibility.Collapsed;
        if (!append)
        {
            EntryList.ItemsSource = null;
            _entries.Clear();
            EntryList.ItemsSource = _entries;
            StatusText.Text = "正在加载 OPDS 目录…";
            StatusText.Visibility = Visibility.Visible;
        }

        try
        {
            OpdsPage page = await _opdsClient.LoadPageAsync(_selectedSource, url);
            string? searchTemplate = page.SearchTemplateUrl ?? _searchTemplateUrl;
            if (!append)
            {
                _searchTemplateUrl = searchTemplate;
                bool hasSearch = !string.IsNullOrWhiteSpace(searchTemplate);
                SearchTextBox.IsEnabled = hasSearch;
            }

            foreach (OpdsEntry entry in page.Entries)
            {
                _entries.Add(new OpdsEntryViewModel(entry));
            }

            _ = LoadVisibleCoversAsync();
            _nextPageUrl = page.NextPageUrl;
            PageTitleText.Text = page.Title;
            StatusText.Visibility = Visibility.Collapsed;
            if (_entries.Count == 0)
            {
                StatusText.Text = "当前目录没有条目。";
                StatusText.Visibility = Visibility.Visible;
            }

            if (!append && preserveHistory)
            {
                if (!string.IsNullOrWhiteSpace(_currentPageUrl) &&
                    !string.Equals(_currentPageUrl, url, StringComparison.Ordinal))
                {
                    _navigationHistory.Add(_currentPageUrl);
                }
                else if (!string.IsNullOrWhiteSpace(_currentPageUrl) && _navigationHistory.Count == 0)
                {
                    _navigationHistory.Add(_currentPageUrl);
                }
            }

            _currentPageUrl = url;
            BackLevelButton.IsEnabled = _navigationHistory.Count > 0;
            LoadMoreButton.Visibility = string.IsNullOrWhiteSpace(_nextPageUrl)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        catch (Exception exception) when (exception is OpdsClientException or HttpRequestException or TaskCanceledException)
        {
            if (!append)
            {
                EntryList.ItemsSource = null;
            }

            StatusText.Text = $"加载失败：{exception.Message}";
            StatusText.Visibility = Visibility.Visible;
            PageTitleText.Text = _selectedSource.Name;

            // 加载失败时同步清空本页搜索模板，避免沿用上一本书源的搜索能力造成误导。
            _nextPageUrl = null;
            _searchTemplateUrl = null;
            SearchTextBox.IsEnabled = false;
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    /// 加载下一页并追加条目。
    /// </summary>
    /// <param name="sender">触发请求的加载更多按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void LoadMore_Click(object sender, RoutedEventArgs e)
    {
        _ = LoadPageAsync(_nextPageUrl, append: true);
    }

    /// <summary>
    /// 处理搜索框回车事件。
    /// </summary>
    /// <param name="sender">接收按键的搜索框。</param>
    /// <param name="e">按键事件参数。</param>
    /// <returns>无。</returns>
    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteSearch();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 使用当前搜索词请求搜索结果。
    /// </summary>
    /// <param name="sender">触发请求的搜索按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Search_Click(object sender, RoutedEventArgs e)
    {
        ExecuteSearch();
    }

    /// <summary>
    /// 执行 OpenSearch 搜索。
    /// </summary>
    /// <returns>无。</returns>
    private void ExecuteSearch()
    {
        if (_selectedSource is null || string.IsNullOrWhiteSpace(_searchTemplateUrl))
        {
            return;
        }

        string query = SearchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        // OpenSearch 模板使用 URL 编码后的 searchTerms，避免中文或特殊字符破坏请求。
        string encodedQuery = Uri.EscapeDataString(query);
        string searchUrl = _searchTemplateUrl.Replace("{searchTerms}", encodedQuery, StringComparison.OrdinalIgnoreCase);
        _searchUrl = searchUrl;
        SearchBackButton.Visibility = Visibility.Visible;
        _ = LoadPageAsync(searchUrl);
    }

    /// <summary>
    /// 退出搜索结果并返回搜索前目录。
    /// </summary>
    /// <param name="sender">触发请求的退出搜索按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void SearchBack_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_searchUrl))
        {
            return;
        }

        _searchUrl = null;
        SearchBackButton.Visibility = Visibility.Collapsed;
        string? targetUrl = _navigationHistory.Count > 0 ? _navigationHistory[^1] : null;
        if (_navigationHistory.Count > 0)
        {
            _navigationHistory.RemoveAt(_navigationHistory.Count - 1);
        }

        _ = LoadPageAsync(targetUrl, preserveHistory: false);
    }

    /// <summary>
    /// 处理条目点击：导航条目进入子目录，下载条目复用已有文件或请求下载。
    /// </summary>
    /// <param name="sender">触发请求的条目按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    /// <summary>
    /// 异步加载当前可见条目的封面；加载失败时保留占位图标。
    /// </summary>
    /// <returns>表示异步加载过程的任务。</returns>
    private async Task LoadVisibleCoversAsync()
    {
        foreach (var item in EntryList.Items.OfType<OpdsEntryViewModel>().ToList())
        {
            if (string.IsNullOrWhiteSpace(item.Entry.CoverThumbnailUrl))
            {
                continue;
            }

            if (EntryList.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter container)
            {
                continue;
            }

            if (FindVisualChild<Image>(container) is not Image image)
            {
                continue;
            }

            byte[]? bytes = await _coverCache.GetImageAsync(item.Entry.CoverThumbnailUrl!);
            if (bytes is not null)
            {
                using var stream = new MemoryStream(bytes);
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                image.Source = bitmap;
            }
        }
    }

    /// <summary>
    /// 在视觉树中查找指定类型的子元素。
    /// </summary>
    /// <typeparam name="T">要查找的元素类型。</typeparam>
    /// <param name="parent">要搜索的父元素。</param>
    /// <returns>第一个匹配的元素；不存在时返回 <see langword="null"/>。</returns>
    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            T? result = FindVisualChild<T>(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    private void EntryButton_Click(object sender, RoutedEventArgs e)
    /// </summary>
    /// <param name="sender">触发请求的条目按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    {
        if (sender is not Button { Tag: OpdsEntryViewModel viewModel })
        {
            return;
        }

        if (viewModel.Entry.IsNavigation)
        {
            string? navigationUrl = viewModel.Entry.NavigationUrl;
            if (!string.IsNullOrWhiteSpace(navigationUrl))
            {
                _ = LoadPageAsync(navigationUrl);
            }

            return;
        }

        if (_selectedSource is null || viewModel.IsDownloading)
        {
            return;
        }

        OpdsLink? preferredLink = SelectPreferredAcquisition(viewModel.Entry.Acquisitions);
        if (preferredLink is null)
        {
            ConfirmationDialog.ShowAlert(
                Window.GetWindow(this),
                "BookRead",
                "该条目没有受支持的下载格式。");
            return;
        }

        ShelfBook? existingBook = FindExistingBook(_selectedSource, viewModel.Entry);
        if (existingBook is not null)
        {
            BookAdded?.Invoke(this, new OpdsBookAddedEventArgs(_selectedSource, viewModel.Entry, existingBook.FilePath));
            return;
        }

        _ = DownloadAsync(_selectedSource, viewModel, preferredLink);
    }

    /// <summary>
    /// 按支持格式优先级选择下载链接。
    /// </summary>
    /// <param name="links">可下载链接。</param>
    /// <returns>优先级最高的链接；没有可用链接时返回 <see langword="null"/>。</returns>
    private static OpdsLink? SelectPreferredAcquisition(IReadOnlyList<OpdsLink> links)
    {
        return links
            .OrderBy(link => GetFormatPriority(link.MediaType, link.Href))
            .FirstOrDefault();
    }

    /// <summary>
    /// 获取链接格式优先级。
    /// </summary>
    /// <param name="mediaType">链接媒体类型。</param>
    /// <param name="href">链接地址。</param>
    /// <returns>优先级值，数值越小越优先；不受支持时返回最大值。</returns>
    private static int GetFormatPriority(string? mediaType, string href)
    {
        string? type = mediaType?.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
        return type switch
        {
            "application/epub+zip" => 0,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => 1,
            "text/plain" => 2,
            "text/markdown" => 3,
            _ => GetExtensionPriority(href)
        };
    }

    /// <summary>
    /// 根据地址扩展名推断格式优先级。
    /// </summary>
    /// <param name="href">链接地址。</param>
    /// <returns>优先级值，数值越小越优先；不受支持时返回最大值。</returns>
    private static int GetExtensionPriority(string href)
    {
        string extension = Path.GetExtension(new Uri(href).AbsolutePath).ToLowerInvariant();
        return extension switch
        {
            ".epub" => 0,
            ".docx" => 1,
            ".txt" or ".text" => 2,
            ".md" or ".markdown" => 3,
            _ => int.MaxValue
        };
    }

    /// <summary>
    /// 查找同书源同条目且本地文件仍存在的书架记录。
    /// </summary>
    /// <param name="source">目标书源。</param>
    /// <param name="entry">目标条目。</param>
    /// <returns>已有书籍；不存在时返回 <see langword="null"/>。</returns>
    internal ShelfBook? FindExistingBook(OpdsSource source, OpdsEntry entry)
    {
        return FindExistingOpdsBook(source, entry);
    }

    /// <summary>
    /// <summary>
    /// 取消指定条目的下载任务。
    /// </summary>
    /// <param name="sender">触发取消的菜单项。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void CancelDownload_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: OpdsEntryViewModel viewModel })
        {
            return;
        }

        if (_downloadCancellations.TryGetValue(viewModel.Key, out CancellationTokenSource? cancellationSource))
        {
            cancellationSource.Cancel();
        }
    }
    private async Task DownloadAsync(OpdsSource source, OpdsEntryViewModel viewModel, OpdsLink link)
    {
        viewModel.IsDownloading = true;
        StatusText.Text = string.Format("正在下载《{0}》…", viewModel.Entry.Title);
        StatusText.Visibility = Visibility.Visible;

        var progress = new Progress<OpdsDownloadState>(state =>
        {
            viewModel.DownloadProgress = state.ProgressPercent;
        });

        var cancellationTokenSource = new CancellationTokenSource();
        _downloadCancellations[viewModel.Key] = cancellationTokenSource;
        try
        {
            using var downloadService = new OpdsDownloadService(OpdsDownloadService.CreateHttpClient(source));
            string filePath = await downloadService.DownloadAsync(
                source,
                viewModel.Entry,
                link,
                progress,
                cancellationTokenSource.Token);
            viewModel.DownloadedFilePath = filePath;
            viewModel.IsDownloading = false;
            ConfirmationDialog.ShowAlert(
                Window.GetWindow(this),
                "BookRead",
                $"《{viewModel.Entry.Title}》已加入书架。");
            BookAdded?.Invoke(this, new OpdsBookAddedEventArgs(source, viewModel.Entry, filePath));
        }
        catch (OperationCanceledException)
        {
            viewModel.IsDownloading = false;
            StatusText.Text = "下载已取消。";
        }
        catch (Exception exception) when (exception is OpdsDownloadException or HttpRequestException or IOException or UnauthorizedAccessException)
        {
            viewModel.IsDownloading = false;
            ConfirmationDialog.ShowAlert(
                Window.GetWindow(this),
                "BookRead",
                $"《{viewModel.Entry.Title}》下载失败：{exception.Message}");
        }
        finally
        {
            _downloadCancellations.Remove(viewModel.Key);
        }
    }
}
