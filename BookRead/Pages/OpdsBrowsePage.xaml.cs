using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BookRead.Controls;
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
    private readonly HttpClient _httpClient;
    private readonly List<OpdsSource> _sources = [];
    private readonly List<string?> _navigationHistory = [];
    private readonly ObservableCollection<OpdsEntryViewModel> _entries = [];
    private OpdsSource? _selectedSource;
    private string? _currentPageUrl;
    private string? _previousPageUrl;
    private string? _nextPageUrl;
    private int _currentPageNumber = 1;
    private string? _searchTemplateUrl;
    private bool _isNavigating;
    private readonly Dictionary<string, CancellationTokenSource> _downloadCancellations = new(StringComparer.Ordinal);

    /// <summary>Segoe Fluent Icons 中“书库”字形，用于没有书源的空状态提示。</summary>
    private const string LibraryIcon = "\uE8F1";

    /// <summary>Segoe Fluent Icons 中“同步”字形，用于目录加载中提示。</summary>
    private const string LoadingIcon = "\uE895";

    /// <summary>Segoe Fluent Icons 中“文件夹”字形，用于目录无条目提示。</summary>
    private const string FolderIcon = "\uE8B7";

    /// <summary>Segoe Fluent Icons 中“错误徽章”字形，用于加载失败提示。</summary>
    private const string ErrorIcon = "\uE783";

    /// <summary>加载图标旋转一圈所需时长。</summary>
    private static readonly TimeSpan LoadingIconSpinDuration = TimeSpan.FromSeconds(1.1);

    /// <summary>下载完成通知的停留时长，需保证用户能读完结果。</summary>
    private static readonly TimeSpan DownloadCompletedNoticeDuration = TimeSpan.FromSeconds(3.5);

    /// <summary>下载失败通知的停留时长，长于成功提示以便用户看清失败原因。</summary>
    private static readonly TimeSpan DownloadFailureNoticeDuration = TimeSpan.FromSeconds(5);

    /// <summary>“已在书架中”提示的停留时长，短于下载完成提示。</summary>
    private static readonly TimeSpan ExistingBookNoticeDuration = TimeSpan.FromSeconds(2.2);

    /// <summary>浏览页标题变化时触发，事件参数为最新标题。</summary>
    internal event EventHandler<string>? PageTitleChanged;

    /// <summary>浏览页上一级导航可用状态变化时触发。</summary>
    internal event EventHandler? BackLevelStateChanged;

    /// <summary>获取浏览页当前显示的标题。</summary>
    internal string CurrentPageTitle { get; private set; } = "OPDS 书源";

    /// <summary>获取一个值，该值指示是否存在可返回的上一级目录。</summary>
    internal bool CanGoBack => _navigationHistory.Count > 0;

    /// <summary>浏览页完成下载并请求将书籍加入书架时触发。</summary>
    internal event EventHandler<OpdsBookAddedEventArgs>? BookAdded;

    /// <summary>创建新的下载任务时触发，供主窗口在下载详情页中跨页面展示该任务。</summary>
    internal event EventHandler<OpdsDownloadTask>? DownloadTaskCreated;

    /// <summary>下载在浏览页不可见时结束时触发，供主窗口跨页面提示下载结果。</summary>
    internal event EventHandler<OpdsDownloadFinishedEventArgs>? DownloadFinished;

    internal Func<OpdsSource, OpdsEntry, ShelfBook?> FindExistingOpdsBook = (_, _) => null;

    /// <summary>
    /// 初始化 OPDS 浏览页面。
    /// </summary>
    public OpdsBrowsePage()
    {
        InitializeComponent();
        EntryList.ItemsSource = _entries;
        _httpClient = OpdsDownloadService.CreateHttpClient();
        _opdsClient = new OpdsClient(_httpClient);
        _coverCache = new CoverImageCache(_httpClient);
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
            ShowEmptyState(LibraryIcon, "还没有 OPDS 书源。请打开设置页添加一个书源。");
            EntryList.ItemsSource = null;
            SearchTextBox.IsEnabled = false;
            SearchButton.IsEnabled = false;
            SetPageTitle("OPDS 书源");
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
        _currentPageNumber = 1;
        RefreshBackLevelState();
    }

    /// <summary>
    /// 显示居中的空状态提示，用于没有书源、正在加载、目录为空或加载失败等列表无内容的场景。
    /// </summary>
    /// <param name="icon">Segoe Fluent Icons 字体中的图标字形。</param>
    /// <param name="text">提示正文，宽度不足时自动换行。</param>
    /// <param name="animateIcon">为 <see langword="true"/> 时让图标持续旋转，用于加载中提示。</param>
    /// <returns>无。</returns>
    private void ShowEmptyState(string icon, string text, bool animateIcon = false)
    {
        EmptyStateIcon.Text = icon;
        EmptyStateText.Text = text;
        EmptyStatePanel.Visibility = Visibility.Visible;
        if (animateIcon)
        {
            StartLoadingAnimation();
        }
        else
        {
            StopLoadingAnimation();
        }
    }

    /// <summary>
    /// 隐藏居中的空状态提示。
    /// </summary>
    /// <returns>无。</returns>
    private void HideEmptyState()
    {
        StopLoadingAnimation();
        EmptyStatePanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 启动加载图标的循环旋转动画。
    /// </summary>
    /// <returns>无。</returns>
    private void StartLoadingAnimation()
    {
        var spinAnimation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = new Duration(LoadingIconSpinDuration),
            RepeatBehavior = RepeatBehavior.Forever
        };
        EmptyStateIconRotation.BeginAnimation(RotateTransform.AngleProperty, spinAnimation);
    }

    /// <summary>
    /// 停止加载图标的旋转动画，使图标恢复静止角度。
    /// </summary>
    /// <returns>无。</returns>
    private void StopLoadingAnimation()
    {
        EmptyStateIconRotation.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    /// <summary>
    /// 刷新当前目录。
    /// </summary>
    /// <returns>无。</returns>
    internal void Refresh()
    {
        _ = LoadPageAsync(_currentPageUrl, preserveHistory: false);
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
    /// <returns>无。</returns>
    internal void GoBack()
    {
        if (_navigationHistory.Count == 0)
        {
            RefreshBackLevelState();
            return;
        }

        string? previousUrl = _navigationHistory[^1];
        _navigationHistory.RemoveAt(_navigationHistory.Count - 1);
        _currentPageUrl = previousUrl;
        _ = LoadPageAsync(previousUrl, preserveHistory: false);
    }

    /// <summary>
    /// 加载并渲染当前 OPDS 页面。
    /// </summary>
    /// <param name="url">目标目录地址；为空时使用当前书源根地址。</param>
    /// <param name="preserveHistory">是否保留当前导航历史；返回上一级时为 false。</param>
    /// <param name="pageStep">分页方向步进；下一页为 1，上一页为 -1，普通导航为 0。</param>
    /// <returns>表示异步加载过程的任务。</returns>
    private async Task LoadPageAsync(
        string? url,
        bool preserveHistory = true,
        int pageStep = 0)
    {
        if (_selectedSource is null || _isNavigating)
        {
            return;
        }

        _isNavigating = true;
        PaginationBar.Visibility = Visibility.Collapsed;
        HideEmptyState();
        {
            EntryList.ItemsSource = null;
            _entries.Clear();
            EntryList.ItemsSource = _entries;
            ShowEmptyState(LoadingIcon, "正在加载 OPDS 目录…", animateIcon: true);
            _previousPageUrl = null;
            _nextPageUrl = null;

            int? requestedPageNumber = GetPageNumberFromUrl(url);
            if (requestedPageNumber.HasValue)
            {
                _currentPageNumber = requestedPageNumber.Value;
            }
            else if (pageStep != 0)
            {
                _currentPageNumber = Math.Max(1, _currentPageNumber + pageStep);
            }
            else if (!string.Equals(url, _currentPageUrl, StringComparison.Ordinal))
            {
                _currentPageNumber = 1;
            }
        }

        try
        {
            OpdsPage page = await _opdsClient.LoadPageAsync(_selectedSource, url);
            string? searchTemplate = page.SearchTemplateUrl ?? _searchTemplateUrl;
            _searchTemplateUrl = searchTemplate;
            bool hasSearch = !string.IsNullOrWhiteSpace(searchTemplate);
            SearchTextBox.IsEnabled = hasSearch;
            SearchButton.IsEnabled = hasSearch;

            foreach (OpdsEntry entry in page.Entries)
            {
                _entries.Add(new OpdsEntryViewModel(entry));
            }

            _ = LoadCoversAsync();
            _nextPageUrl = page.NextPageUrl;
            SetPageTitle(page.Title);
            HideEmptyState();
            if (_entries.Count == 0)
            {
                ShowEmptyState(FolderIcon, "当前目录没有条目。");
            }

            if (preserveHistory)
            {
                // 根目录地址为 null，也必须入栈，否则从根目录进入子目录后无法返回上一级。
                if (!string.Equals(_currentPageUrl, url, StringComparison.Ordinal))
                {
                    _navigationHistory.Add(_currentPageUrl);
                }
            }

            _currentPageUrl = url;
            _previousPageUrl = page.PreviousPageUrl;
            _nextPageUrl = page.NextPageUrl;
            RefreshBackLevelState();
            UpdatePagination();
        }
        catch (Exception exception) when (exception is OpdsClientException or HttpRequestException or TaskCanceledException)
        {
            ShowEmptyState(ErrorIcon, $"加载失败：{exception.Message}");
            SetPageTitle(_selectedSource?.Name ?? CurrentPageTitle);

            // 加载失败时同步清空本页搜索模板，避免沿用上一本书源的搜索能力造成误导。
            _nextPageUrl = null;
            _searchTemplateUrl = null;
            SearchTextBox.IsEnabled = false;
            SearchButton.IsEnabled = false;
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    /// 更新浏览页标题并通知外部标题栏。
    /// </summary>
    /// <param name="title">要显示的浏览页标题。</param>
    /// <exception cref="ArgumentException"><paramref name="title"/> 为 null、空字符串或仅包含空白字符时抛出。</exception>
    private void SetPageTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        CurrentPageTitle = title;
        PageTitleChanged?.Invoke(this, title);
    }

    /// <summary>
    /// 通知外部标题栏同步上一级导航按钮状态。
    /// </summary>
    /// <returns>无。</returns>
    private void RefreshBackLevelState()
    {
        BackLevelStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 更新底部分页栏的可见性、按钮状态和页码。
    /// </summary>
    /// <returns>无。</returns>
    private void UpdatePagination()
    {
        bool hasPagination = _previousPageUrl is not null || _nextPageUrl is not null;
        PaginationBar.Visibility = hasPagination ? Visibility.Visible : Visibility.Collapsed;
        PreviousPageButton.IsEnabled = _previousPageUrl is not null;
        NextPageButton.IsEnabled = _nextPageUrl is not null;
        PaginationText.Text = $"第 {_currentPageNumber} 页";
    }

    /// <summary>
    /// 响应上一页请求并替换当前列表。
    /// </summary>
    /// <param name="sender">触发请求的上一页按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        if (_previousPageUrl is not null)
        {
            _ = LoadPageAsync(_previousPageUrl, preserveHistory: false, pageStep: -1);
        }
    }

    /// <summary>
    /// 响应下一页请求并替换当前列表。
    /// </summary>
    /// <param name="sender">触发请求的下一页按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_nextPageUrl is not null)
        {
            _ = LoadPageAsync(_nextPageUrl, preserveHistory: false, pageStep: 1);
        }
    }

    /// <summary>
    /// 从 OPDS 分页地址中读取页码。
    /// </summary>
    /// <param name="url">分页地址。</param>
    /// <returns>有效页码；地址未提供页码时返回 <see langword="null"/>。</returns>
    private static int? GetPageNumberFromUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? requestUri))
        {
            return null;
        }

        string query = requestUri.Query.TrimStart('?');
        foreach (string parameter in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separatorIndex = parameter.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            string name = Uri.UnescapeDataString(parameter[..separatorIndex]);
            if (!string.Equals(name, "pageNumber", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = Uri.UnescapeDataString(parameter[(separatorIndex + 1)..]);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pageNumber) && pageNumber > 0)
            {
                return pageNumber;
            }
        }

        return null;
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
            PageNotification.Show("当前目录不支持搜索。", InlineNotificationType.Warning);
            return;
        }

        string query = SearchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            // 空关键词时给出明确反馈，避免用户误以为搜索按钮失效。
            PageNotification.Show("请输入要搜索的内容。", InlineNotificationType.Warning);
            SearchTextBox.Focus();
            return;
        }

        // OpenSearch 模板使用 URL 编码后的 searchTerms，避免中文或特殊字符破坏请求。
        string encodedQuery = Uri.EscapeDataString(query);
        string searchUrl = _searchTemplateUrl.Replace("{searchTerms}", encodedQuery, StringComparison.OrdinalIgnoreCase);
        _ = LoadPageAsync(searchUrl);
    }

    /// <summary>
    /// 处理条目点击：导航条目进入子目录，下载条目复用已有文件或请求下载。
    /// </summary>
    /// <param name="sender">触发请求的条目按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    /// <summary>
    /// 异步加载条目封面；单个封面加载失败时保留占位图标。
    /// </summary>
    /// <returns>表示异步加载过程的任务。</returns>
    private async Task LoadCoversAsync()
    {
        foreach (var item in EntryList.Items.OfType<OpdsEntryViewModel>())
        {
            if (string.IsNullOrWhiteSpace(item.Entry.CoverThumbnailUrl))
            {
                continue;
            }

            try
            {
                byte[]? bytes = await _coverCache.GetImageAsync(item.Entry.CoverThumbnailUrl!);
                if (bytes is null)
                {
                    continue;
                }

                using var stream = new MemoryStream(bytes);
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                item.SetCoverImage(bitmap);
            }
            catch (Exception)
            {
                // 封面属于增强展示内容；单个图片下载或解码失败时继续加载其他封面。
            }
        }
    }

    /// <summary>
    /// 处理条目点击：导航条目进入子目录，下载条目复用已有文件或请求下载。
    /// </summary>
    /// <param name="sender">触发请求的条目按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void EntryButton_Click(object sender, RoutedEventArgs e)
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
            // 本地文件已存在时不再重复下载，但必须给出反馈，否则用户会以为点击没有生效。
            PageNotification.Show(
                $"《{viewModel.Entry.Title}》已在书架中。",
                InlineNotificationType.Info,
                ExistingBookNoticeDuration);

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

    /// <summary>
    /// 下载指定条目并加入书架，结束后收起进行中的操作提示，避免残留状态误导用户。
    /// </summary>
    /// <param name="source">目标 OPDS 书源。</param>
    /// <param name="viewModel">待下载条目的视图模型。</param>
    /// <param name="link">条目的下载链接。</param>
    /// <returns>表示异步下载过程的任务。</returns>
    private async Task DownloadAsync(OpdsSource source, OpdsEntryViewModel viewModel, OpdsLink link)
    {
        viewModel.IsDownloading = true;

        // 重置进度，避免复用视图模型时显示上一次下载的残留百分比。
        viewModel.DownloadProgress = 0;

        var cancellationTokenSource = new CancellationTokenSource();
        _downloadCancellations[viewModel.Key] = cancellationTokenSource;

        // 创建可供下载详情页展示的任务对象，并把取消能力交给详情页的取消按钮。
        var downloadTask = new OpdsDownloadTask(viewModel.Entry.Title, source.Name)
        {
            CancelRequested = cancellationTokenSource.Cancel
        };
        DownloadTaskCreated?.Invoke(this, downloadTask);

        var progress = new Progress<OpdsDownloadState>(state =>
        {
            viewModel.DownloadProgress = state.ProgressPercent;
            downloadTask.ReportProgress(state.ProgressPercent);
        });

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
            downloadTask.MarkCompleted();

            // 完成结果用可自动收起、不打断操作的通知呈现，替代原先需要点“确定”的模态弹窗。
            ReportDownloadFinished(
                $"《{viewModel.Entry.Title}》已加入书架。",
                InlineNotificationType.Success,
                DownloadCompletedNoticeDuration,
                OpdsDownloadStatus.Completed);

            BookAdded?.Invoke(this, new OpdsBookAddedEventArgs(source, viewModel.Entry, filePath));
        }
        catch (OperationCanceledException)
        {
            viewModel.IsDownloading = false;
            downloadTask.MarkCanceled();

            // 取消属于瞬时结果，改用可自动收起的通知，避免提示长期停留。
            ReportDownloadFinished(
                "下载已取消。",
                InlineNotificationType.Info,
                ExistingBookNoticeDuration,
                OpdsDownloadStatus.Canceled);
        }
        catch (Exception exception)
        {
            // 兜底处理全部失败情形：失败原因留在下载详情页中备查，同时用轻量提示即时告知。
            viewModel.IsDownloading = false;
            downloadTask.MarkFailed(exception.Message);

            ReportDownloadFinished(
                $"《{viewModel.Entry.Title}》下载失败：{exception.Message}",
                InlineNotificationType.Warning,
                DownloadFailureNoticeDuration,
                OpdsDownloadStatus.Failed);
        }
        finally
        {
            _downloadCancellations.Remove(viewModel.Key);
        }
    }

    /// <summary>
    /// 报告单个下载的结束结果：浏览页可见时使用页面内提示，否则交由主窗口跨页面提示。
    /// </summary>
    /// <param name="message">要展示给用户的结果消息。</param>
    /// <param name="type">结果对应的提示类型。</param>
    /// <param name="noticeDuration">页面内提示的停留时长。</param>
    /// <param name="status">下载结束时所处的状态，用于决定跨页面提示的呈现方式。</param>
    /// <returns>无。</returns>
    private void ReportDownloadFinished(string message, InlineNotificationType type, TimeSpan noticeDuration, OpdsDownloadStatus status)
    {
        // 页面隐藏后页面内提示会随页面一起不可见，此时改由主窗口在任意页面弹出提示。
        if (IsVisible)
        {
            PageNotification.Show(message, type, noticeDuration);
            return;
        }

        DownloadFinished?.Invoke(this, new OpdsDownloadFinishedEventArgs(message, status));
    }
}
