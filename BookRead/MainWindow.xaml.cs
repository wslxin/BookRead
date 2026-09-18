using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using BookRead.Dialogs;
using BookRead.Models;
using BookRead.Services;
using BookRead.Data;
using BookRead.Pages;

namespace BookRead;

/// <summary>
/// 应用主窗口，负责在书架页和阅读页之间切换以及管理窗口尺寸。
/// </summary>
public partial class MainWindow : Window
{
    private readonly BookShelfStore _bookShelfStore = new();
    private readonly ShortcutSettingsStore _shortcutSettingsStore = new();
    private readonly List<ShelfBook> _shelfBooks = [];
    private ShortcutSettings _shortcutSettings = ShortcutSettings.CreateDefault();
    private string? _currentBookPath;
    private string? _currentBookTitle;
    private string? _currentChapterTitle;
    private bool _settingsOpenedFromReader;
    private readonly Forms.NotifyIcon _notifyIcon;
    private TrayMenuWindow? _trayMenuWindow;
    private bool _isExiting;
    private readonly OpdsSourceStore _opdsSourceStore = new();
    private bool _isOpdsPageVisible;
    private List<OpdsSource> _opdsSources = [];

    /// <summary>书架页窗口宽度。</summary>
    private const double ShelfWindowWidth = 500;

    /// <summary>OPDS 浏览页窗口宽度。</summary>
    private const double OpdsWindowWidth = 1020;

    /// <summary>阅读页窗口宽度。</summary>
    private const double ReaderWindowWidth = 1020;

    /// <summary>
    /// 初始化主窗口并默认显示书架页。
    /// </summary>
    /// <exception cref="InvalidOperationException">应用程序图标无法加载时抛出。</exception>
    public MainWindow()
    {
        InitializeComponent();
        _notifyIcon = CreateNotifyIcon();
        ShelfPageControl.ImportRequested += OpenBook_Click;
        ShelfPageControl.OpdsRequested += BrowseOpds_Click;
        ShelfPageControl.BookOpenRequested += ShelfPageControl_BookOpenRequested;
        ShelfPageControl.BookRemovalRequested += ShelfPageControl_BookRemovalRequested;
        ShelfPageControl.BookRenameRequested += ShelfPageControl_BookRenameRequested;
        ShelfPageControl.BookLocationRequested += ShelfPageControl_BookLocationRequested;
        ReaderPageControl.ProgressChanged += ReaderPageControl_ProgressChanged;
        ReaderPageControl.BookInfoChanged += ReaderPageControl_BookInfoChanged;
        ReaderPageControl.ChapterChanged += ReaderPageControl_ChapterChanged;
        ReaderPageControl.ReaderTransparencyChanged += ReaderPageControl_ReaderTransparencyChanged;
        TitleBarControl.ShelfRequested += TitleBarControl_ShelfRequested;
        TitleBarControl.SettingsRequested += TitleBarControl_SettingsRequested;
        SettingsPageControl.SettingsSaved += SettingsPageControl_SettingsSaved;
        SettingsPageControl.BackRequested += SettingsPageControl_BackRequested;
        SettingsPageControl.OpdsSourcesChanged += SettingsPageControl_OpdsSourcesChanged;
        OpdsPageControl.BackToShelfRequested += OpdsPageControl_BackToShelfRequested;
        OpdsPageControl.SettingsRequested += OpdsPageControl_SettingsRequested;
        OpdsPageControl.BookAdded += OpdsPageControl_BookAdded;
        OpdsPageControl.FindExistingOpdsBook = FindExistingOpdsBook;
        LoadShortcutSettings();
        ReaderPageControl.ApplyShortcutSettings(_shortcutSettings);
        LoadShelf();
        ShowShelfPage();
    }

    /// <summary>
    /// 创建系统托盘图标，并绑定自定义 WPF 托盘菜单所需的鼠标事件。
    /// </summary>
    /// <returns>已初始化但尚未显示的系统托盘图标。</returns>
    /// <exception cref="InvalidOperationException">当前进程路径为空或应用程序图标无法加载时抛出。</exception>
    private Forms.NotifyIcon CreateNotifyIcon()
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("无法获取应用程序路径，不能创建系统托盘图标。");
        }

        Drawing.Icon? icon = Drawing.Icon.ExtractAssociatedIcon(processPath);
        if (icon is null)
        {
            throw new InvalidOperationException("无法加载应用程序图标，不能创建系统托盘图标。");
        }

        var notifyIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "BookRead",
            Visible = false
        };
        notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
        notifyIcon.MouseUp += NotifyIcon_MouseUp;
        return notifyIcon;
    }

    /// <summary>
    /// 响应系统托盘图标双击，恢复并激活主窗口。
    /// </summary>
    /// <param name="sender">触发事件的系统托盘图标。</param>
    /// <param name="e">事件参数。</param>
    /// <returns>无。</returns>
    private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
    {
        ShowFromTray();
    }

    /// <summary>
    /// 响应系统托盘图标右键操作，打开自定义托盘菜单。
    /// </summary>
    /// <param name="sender">触发事件的系统托盘图标。</param>
    /// <param name="e">包含鼠标按键状态的事件参数。</param>
    /// <returns>无。</returns>
    private void NotifyIcon_MouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Right)
        {
            ShowTrayMenu();
        }
    }

    /// <summary>
    /// 响应系统托盘菜单中的打开操作，恢复并激活主窗口。
    /// </summary>
    /// <param name="sender">触发操作的托盘菜单项。</param>
    /// <param name="e">事件参数。</param>
    /// <returns>无。</returns>
    private void OpenFromTray_Click(object? sender, EventArgs e)
    {
        ShowFromTray();
    }

    /// <summary>
    /// 响应系统托盘菜单中的退出操作，真正结束应用程序。
    /// </summary>
    /// <param name="sender">触发操作的托盘菜单项。</param>
    /// <param name="e">事件参数。</param>
    /// <returns>无。</returns>
    private void ExitFromTray_Click(object? sender, EventArgs e)
    {
        _isExiting = true;
        Close();
    }

    /// <summary>
    /// 显示位于托盘图标上方的自定义托盘菜单。
    /// </summary>
    /// <returns>无。</returns>
    private void ShowTrayMenu()
    {
        CloseTrayMenu();

        var trayMenuWindow = new TrayMenuWindow();
        trayMenuWindow.OpenRequested += OpenFromTray_Click;
        trayMenuWindow.ExitRequested += ExitFromTray_Click;
        trayMenuWindow.Deactivated += TrayMenuWindow_Deactivated;
        trayMenuWindow.Closed += TrayMenuWindow_Closed;

        _trayMenuWindow = trayMenuWindow;

        Drawing.Point cursorPosition = Forms.Cursor.Position;
        Forms.Screen screen = Forms.Screen.FromPoint(cursorPosition);
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = dpi.DpiScaleX;
        double scaleY = dpi.DpiScaleY;
        double menuWidth = trayMenuWindow.Width;
        double menuHeight = trayMenuWindow.Height;
        double workAreaLeft = screen.WorkingArea.Left / scaleX;
        double workAreaTop = screen.WorkingArea.Top / scaleY;
        double workAreaRight = screen.WorkingArea.Right / scaleX;
        double workAreaBottom = screen.WorkingArea.Bottom / scaleY;

        // 托盘菜单固定从鼠标位置的右下方对齐，并限制在工作区内，避免菜单被屏幕边缘截断。
        trayMenuWindow.Left = Math.Clamp(
            cursorPosition.X / scaleX - menuWidth + 10,
            workAreaLeft,
            workAreaRight - menuWidth);
        trayMenuWindow.Top = Math.Clamp(
            cursorPosition.Y / scaleY - menuHeight - 8,
            workAreaTop,
            workAreaBottom - menuHeight);
        trayMenuWindow.Show();
        trayMenuWindow.Activate();
    }

    /// <summary>
    /// 关闭当前显示的自定义托盘菜单并解除事件订阅。
    /// </summary>
    /// <returns>无。</returns>
    private void CloseTrayMenu()
    {
        if (_trayMenuWindow is null)
        {
            return;
        }

        TrayMenuWindow trayMenuWindow = _trayMenuWindow;
        _trayMenuWindow = null;
        trayMenuWindow.OpenRequested -= OpenFromTray_Click;
        trayMenuWindow.ExitRequested -= ExitFromTray_Click;
        trayMenuWindow.Deactivated -= TrayMenuWindow_Deactivated;
        trayMenuWindow.Closed -= TrayMenuWindow_Closed;
        if (trayMenuWindow.IsVisible)
        {
            trayMenuWindow.Close();
        }
    }

    /// <summary>
    /// 在自定义托盘菜单失去焦点时关闭菜单。
    /// </summary>
    /// <param name="sender">失去焦点的托盘菜单窗口。</param>
    /// <param name="e">事件参数。</param>
    /// <returns>无。</returns>
    private void TrayMenuWindow_Deactivated(object? sender, EventArgs e)
    {
        CloseTrayMenu();
    }

    /// <summary>
    /// 清理已经关闭的自定义托盘菜单引用。
    /// </summary>
    /// <param name="sender">已关闭的托盘菜单窗口。</param>
    /// <param name="e">事件参数。</param>
    /// <returns>无。</returns>
    private void TrayMenuWindow_Closed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(_trayMenuWindow, sender))
        {
            _trayMenuWindow = null;
        }
    }

    /// <summary>
    /// 从系统托盘恢复主窗口并将其置于前台。
    /// </summary>
    /// <returns>无。</returns>
    private void ShowFromTray()
    {
        CloseTrayMenu();
        Show();
        _notifyIcon.Visible = false;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Focus();
    }

    /// <summary>
    /// 根据用户设置将窗口关闭操作转换为隐藏到系统托盘或直接退出应用。
    /// </summary>
    /// <param name="sender">接收关闭事件的主窗口。</param>
    /// <param name="e">包含是否取消关闭状态的事件参数。</param>
    /// <returns>无。</returns>
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        CloseTrayMenu();
        if (!_isExiting && _shortcutSettings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon.Visible = true;
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }


    /// <summary>
    /// 响应菜单中的 OPDS 书源浏览请求。
    /// </summary>
    /// <param name="sender">触发事件的菜单项。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void BrowseOpds_Click(object sender, RoutedEventArgs e)
    {
        ShowOpdsBrowsePage();
    }

    /// <summary>
    /// 打开本地书籍文件并切换到阅读页。
    /// </summary>
    /// <param name="sender">触发事件的导入按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    /// <exception cref="IOException">读取文件失败时由内容加载器抛出。</exception>
    /// <exception cref="BookContentLoadException">文件格式不受支持、内容损坏或正文为空时由内容加载器抛出。</exception>
    private async void OpenBook_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "支持的书籍|*.txt;*.text;*.md;*.markdown;*.epub;*.docx|文本文件|*.txt;*.text|Markdown|*.md;*.markdown|EPUB 电子书|*.epub|Word 文档|*.docx|所有文件|*.*",
            Title = "导入本地书籍"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await OpenAndRememberBookAsync(dialog.FileName);
    }

    /// <summary>
    /// 从本地持久化数据恢复书架并刷新书架页面。
    /// </summary>
    /// <returns>无。</returns>
    private void LoadShelf()
    {
        try
        {
            _shelfBooks.AddRange(_bookShelfStore.Load()
                .OrderByDescending(book => book.LastOpenedAt));
            ShelfPageControl.SetBooks(_shelfBooks);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            ConfirmationDialog.ShowAlert(
                this,
                "BookRead",
                $"书架数据读取失败：{exception.Message}");
            ShelfPageControl.SetBooks(_shelfBooks);
        }
    }

    /// <summary>
    /// 打开书架中已保存的书籍。
    /// </summary>
    /// <param name="sender">发起请求的书架页面。</param>
    /// <param name="e">包含所选书籍的事件参数。</param>
    /// <returns>无。</returns>
    private async void ShelfPageControl_BookOpenRequested(object? sender, BookOpenRequestedEventArgs e)
    {
        await OpenAndRememberBookAsync(e.Book.FilePath);
    }

    /// <summary>
    /// 在 Windows 文件资源管理器中打开书籍所在文件夹，并尽可能选中对应文件。
    /// </summary>
    /// <param name="sender">发起请求的书架页面。</param>
    /// <param name="e">包含目标书籍路径的事件参数。</param>
    /// <returns>无。</returns>
    /// <exception cref="InvalidOperationException">文件资源管理器进程无法启动时由系统抛出；此异常会被捕获并提示用户。</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">启动文件资源管理器失败时由系统抛出；此异常会被捕获并提示用户。</exception>
    private void ShelfPageControl_BookLocationRequested(object? sender, BookOpenRequestedEventArgs e)
    {
        string? directory = Path.GetDirectoryName(e.Book.FilePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            ConfirmationDialog.ShowAlert(
                this,
                "BookRead",
                $"书籍所在文件夹不存在：{directory ?? e.Book.FilePath}");
            return;
        }

        try
        {
            // 使用 /select 让用户打开按钮后能立即定位到书籍文件，而不是还要在目录中再次查找。
            string arguments = File.Exists(e.Book.FilePath)
                ? $"/select,\"{e.Book.FilePath}\""
                : $"\"{directory}\"";
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = arguments,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ConfirmationDialog.ShowAlert(
                this,
                "BookRead",
                $"无法打开书籍所在文件夹：{exception.Message}");
        }
    }

    /// <summary>
    /// 将阅读页报告的章节和分页位置写回当前书架记录。
    /// </summary>
    /// <param name="sender">报告进度变化的阅读页面。</param>
    /// <param name="e">包含最新阅读位置的事件参数。</param>
    /// <returns>无。</returns>
    private async void ReaderPageControl_ProgressChanged(object? sender, ReaderProgressChangedEventArgs e)
    {
        if (_currentBookPath is null)
        {
            return;
        }

        int bookIndex = _shelfBooks.FindIndex(book =>
            string.Equals(book.FilePath, _currentBookPath, StringComparison.OrdinalIgnoreCase));
        if (bookIndex < 0)
        {
            return;
        }

        _shelfBooks[bookIndex] = _shelfBooks[bookIndex] with
        {
            ChapterIndex = e.ChapterIndex,
            PageIndex = e.PageIndex,
            LastOpenedAt = DateTimeOffset.Now
        };

        try
        {
            await _bookShelfStore.SaveAsync(_shelfBooks);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 阅读仍可继续；下次进度变化时会再次尝试保存。
        }
    }

    /// <summary>
    /// 将阅读页提供的书籍信息同步到标题栏。
    /// </summary>
    /// <param name="sender">报告书籍信息变化的阅读页面。</param>
    /// <param name="e">包含书名的事件参数。</param>
    /// <returns>无。</returns>
    private void ReaderPageControl_BookInfoChanged(object? sender, ReaderBookInfoChangedEventArgs e)
    {
        _currentBookTitle = e.Title;
        _currentChapterTitle = null;
        UpdateTitleBarReadingTitle();
    }

    /// <summary>
    /// 将阅读页提供的章节名称同步到标题栏。
    /// </summary>
    /// <param name="sender">报告章节变化的阅读页面。</param>
    /// <param name="e">包含章节名称的事件参数。</param>
    /// <returns>无。</returns>
    private void ReaderPageControl_ChapterChanged(object? sender, ReaderChapterChangedEventArgs e)
    {
        _currentChapterTitle = e.Title;
        UpdateTitleBarReadingTitle();
    }

    /// <summary>
    /// 根据当前书名和章节名更新标题栏显示文本。
    /// </summary>
    /// <returns>无。</returns>
    private void UpdateTitleBarReadingTitle()
    {
        string? readingTitle = GetTitleBarReadingTitle();
        if (!string.IsNullOrWhiteSpace(readingTitle))
        {
            TitleBarControl.SetBookInfo(readingTitle);
        }
    }

    /// <summary>
    /// 组合标题栏使用的书名和章节名，避免两者相同时重复显示。
    /// </summary>
    /// <returns>组合后的标题文本；无可用信息时返回 <see langword="null"/>。</returns>
    private string? GetTitleBarReadingTitle()
    {
        if (string.IsNullOrWhiteSpace(_currentBookTitle))
        {
            return _currentChapterTitle;
        }

        if (string.IsNullOrWhiteSpace(_currentChapterTitle) ||
            string.Equals(_currentBookTitle, _currentChapterTitle, StringComparison.Ordinal))
        {
            return _currentBookTitle;
        }

        return $"{_currentBookTitle} · {_currentChapterTitle}";
    }

    /// <summary>
    /// 响应阅读页透明背景状态变化，并同步隐藏或显示标题栏与调整窗口圆角裁剪。
    /// </summary>
    /// <param name="sender">报告透明背景状态变化的阅读页面。</param>
    /// <param name="e">事件参数。</param>
    /// <returns>无。</returns>
    private void ReaderPageControl_ReaderTransparencyChanged(object? sender, EventArgs e)
    {
        SetTitleBarVisible(!ReaderPageControl.IsReaderBackgroundTransparent);
        UpdateWindowFrameClip();
    }

    /// <summary>
    /// 从本地存储加载应用配置，读取失败时使用默认配置。
    /// </summary>
    /// <returns>无。</returns>
    private void LoadShortcutSettings()
    {
        try
        {
            _shortcutSettings = _shortcutSettingsStore.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _shortcutSettings = ShortcutSettings.CreateDefault();
        }
    }

    /// <summary>
    /// 确认用户的移除请求，并在持久化成功后刷新书架。
    /// </summary>
    /// <param name="sender">发起请求的书架页面。</param>
    /// <param name="e">包含待移除书籍的事件参数。</param>
    /// <returns>无。</returns>
    private async void ShelfPageControl_BookRemovalRequested(object? sender, BookOpenRequestedEventArgs e)
    {
        // OPDS 下载书籍由应用托管，允许用户在移除记录时清理文件；本地书籍仍保持只移除记录。
        ConfirmationDialogResult removalResult = ConfirmationDialog.ShowFor(
            this,
            new ConfirmationDialogOptions(
                "从书架移除",
                $"确定移除《{e.Book.Title}》吗？",
                e.Book.OpdsSourceId is null
                    ? "这只会移除书架记录，原始文件会保留。"
                    : "可以选择仅移除书架记录，或同时删除应用托管目录中的下载文件。",
                ConfirmText: "仅移除记录",
                AlternativeText: e.Book.OpdsSourceId is null ? null : "同时删除文件",
                IsDestructive: true));
        if (removalResult == ConfirmationDialogResult.Cancel)
        {
            return;
        }

        if (removalResult == ConfirmationDialogResult.Alternative && File.Exists(e.Book.FilePath))
        {
            try
            {
                File.Delete(e.Book.FilePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                ConfirmationDialog.ShowAlert(
                    this,
                    "BookRead",
                    $"书架记录已保留，但下载文件删除失败：{exception.Message}");
                return;
            }
        }

        // 先保存候选列表，确保写入失败时界面与内存中的原记录仍然存在。
        List<ShelfBook> remainingBooks = _shelfBooks
            .Where(book => !string.Equals(
                book.FilePath,
                e.Book.FilePath,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        try
        {
            await _bookShelfStore.SaveAsync(remainingBooks);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ConfirmationDialog.ShowAlert(
                this,
                "BookRead",
                $"书架更新失败：{exception.Message}");
            return;
        }

        _shelfBooks.Clear();
        _shelfBooks.AddRange(remainingBooks);
        ShelfPageControl.SetBooks(_shelfBooks);
    }

    /// <summary>
    /// 修改书架中指定书籍的显示名称，并在持久化成功后刷新书架。
    /// </summary>
    /// <param name="sender">发起请求的书架页面。</param>
    /// <param name="e">包含待重命名书籍的事件参数。</param>
    /// <returns>无。</returns>
    private async void ShelfPageControl_BookRenameRequested(object? sender, BookOpenRequestedEventArgs e)
    {
        int bookIndex = _shelfBooks.FindIndex(book =>
            string.Equals(book.FilePath, e.Book.FilePath, StringComparison.OrdinalIgnoreCase));
        if (bookIndex < 0)
        {
            return;
        }

        string? newTitle = RenameBookDialog.ShowFor(this, _shelfBooks[bookIndex].Title);
        if (newTitle is null ||
            string.Equals(newTitle, _shelfBooks[bookIndex].Title, StringComparison.Ordinal))
        {
            return;
        }

        // 先保存候选列表，确保写入失败时书架记录和界面都保持原名称。
        List<ShelfBook> updatedBooks = _shelfBooks.ToList();
        updatedBooks[bookIndex] = updatedBooks[bookIndex] with
        {
            Title = newTitle
        };

        try
        {
            await _bookShelfStore.SaveAsync(updatedBooks);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ConfirmationDialog.ShowAlert(
                this,
                "BookRead",
                $"书籍重命名失败：{exception.Message}");
            return;
        }

        _shelfBooks.Clear();
        _shelfBooks.AddRange(updatedBooks);
        ShelfPageControl.SetBooks(_shelfBooks);

        if (string.Equals(_currentBookPath, e.Book.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            _currentBookTitle = newTitle;
            if (ReaderPageControl.Visibility == Visibility.Visible)
            {
                TitleBarControl.SetBookInfo(newTitle);
            }
        }
    }

    /// <summary>
    /// 打开指定书籍文件，并将成功打开的书籍新增或更新到持久化书架。
    /// </summary>
    /// <param name="filePath">要打开的书籍文件绝对路径。</param>
    /// <returns>表示打开和保存过程的任务。</returns>
    private async Task OpenAndRememberBookAsync(string filePath)
    {
        string normalizedPath = Path.GetFullPath(filePath);
        ShelfBook? savedBook = _shelfBooks.FirstOrDefault(book =>
            string.Equals(book.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase));
        string displayTitle = savedBook?.Title ?? Path.GetFileNameWithoutExtension(normalizedPath);
        _currentBookPath = normalizedPath;

        try
        {
            await ReaderPageControl.LoadBookAsync(
                normalizedPath,
                savedBook?.ChapterIndex ?? 0,
                savedBook?.PageIndex ?? 0,
                displayTitle);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or BookContentLoadException)
        {
            ConfirmationDialog.ShowAlert(
                this,
                "BookRead",
                $"书籍打开失败：{exception.Message}");
            _currentBookPath = null;
            return;
        }

        AddOrUpdateShelfBook(normalizedPath, displayTitle);
        ShelfPageControl.SetBooks(_shelfBooks);
        ShowReaderPage();

        try
        {
            await _bookShelfStore.SaveAsync(_shelfBooks);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ConfirmationDialog.ShowAlert(
                this,
                "BookRead",
                $"书籍已打开，但书架数据保存失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 将书籍加入书架；相同文件路径已存在时更新显示名称和最近打开时间。
    /// </summary>
    /// <param name="filePath">已成功打开的书籍文件绝对路径。</param>
    /// <param name="displayTitle">书籍在书架和标题栏中的显示名称。</param>
    /// <returns>无。</returns>
    private void AddOrUpdateShelfBook(string filePath, string displayTitle)
    {
        string normalizedPath = Path.GetFullPath(filePath);
        string title = string.IsNullOrWhiteSpace(displayTitle)
            ? Path.GetFileNameWithoutExtension(normalizedPath)
            : displayTitle.Trim();
        DateTimeOffset now = DateTimeOffset.Now;
        int existingIndex = _shelfBooks.FindIndex(book =>
            string.Equals(book.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase));

        ShelfBook book = existingIndex >= 0
            ? _shelfBooks[existingIndex] with
            {
                Title = title,
                LastOpenedAt = now
            }
            : new ShelfBook(
                normalizedPath,
                title,
                now,
                now);

        if (existingIndex >= 0)
        {
            _shelfBooks.RemoveAt(existingIndex);
        }

        _shelfBooks.Insert(0, book);
    }

    /// <summary>
    /// 为已有书籍记录刷新来源名称快照。
    /// </summary>
    /// <param name="source">当前书源。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <see langword="null"/> 时抛出。</exception>
    private void RefreshSourceNameSnapshot(OpdsSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        for (int index = 0; index < _shelfBooks.Count; index++)
        {
            if (_shelfBooks[index].OpdsSourceId == source.Id)
            {
                _shelfBooks[index] = _shelfBooks[index] with { SourceNameSnapshot = source.Name };
            }
        }

        ShelfPageControl.SetBooks(_shelfBooks);
    }
    /// <summary>
    /// 查找同书源同条目且文件仍存在的书架记录。
    /// </summary>
    /// <param name="source">目标书源。</param>
    /// <param name="entry">目标 OPDS 条目。</param>
    /// <returns>已有书籍；不存在时返回 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException">任一参数为 <see langword="null"/> 时抛出。</exception>
    /// <summary>
    /// 在书源列表变化后刷新所有书架记录的来源名称快照。
    /// </summary>
    /// <returns>无。</returns>
    private void RefreshSourceSnapshots()
    {
        if (_opdsSources.Count == 0)
        {
            return;
        }

        bool changed = false;
        for (int index = 0; index < _shelfBooks.Count; index++)
        {
            Guid? sourceId = _shelfBooks[index].OpdsSourceId;
            if (sourceId is null)
            {
                continue;
            }

            OpdsSource? source = _opdsSources.FirstOrDefault(item => item.Id == sourceId);
            if (source is not null && !string.Equals(_shelfBooks[index].SourceNameSnapshot, source.Name, StringComparison.Ordinal))
            {
                _shelfBooks[index] = _shelfBooks[index] with { SourceNameSnapshot = source.Name };
                changed = true;
            }
        }

        if (changed)
        {
            ShelfPageControl.SetBooks(_shelfBooks);
        }
    }
    private ShelfBook? FindExistingOpdsBook(OpdsSource source, OpdsEntry entry)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entry);
        return _shelfBooks.FirstOrDefault(book =>
            book.OpdsSourceId == source.Id &&
            string.Equals(book.OpdsBookId, entry.BookId, StringComparison.Ordinal) &&
            File.Exists(book.FilePath));
    }

    /// <summary>
    /// 将 OPDS 下载完成的书籍写入书架。
    /// </summary>
    /// <param name="sender">发起事件的浏览页面。</param>
    /// <param name="e">包含书源、条目和文件路径的事件参数。</param>
    /// <returns>无。</returns>
    private async void OpdsPageControl_BookAdded(object? sender, OpdsBookAddedEventArgs e)
    {
        if (_shelfBooks.Any(book => book.OpdsSourceId == e.Source.Id &&
                                    string.Equals(book.OpdsBookId, e.Entry.BookId, StringComparison.Ordinal)))
        {
            return;
        }

        // 先更新内存与界面，再保存配置；若保存失败仍保留已下载书籍的可见记录。
        _shelfBooks.Insert(0, new ShelfBook(
            Path.GetFullPath(e.FilePath),
            e.Entry.Title,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            OpdsSourceId: e.Source.Id,
            OpdsBookId: e.Entry.BookId,
            SourceNameSnapshot: e.Source.Name));
        ShelfPageControl.SetBooks(_shelfBooks);
        try
        {
            await _bookShelfStore.SaveAsync(_shelfBooks);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ConfirmationDialog.ShowAlert(
                this,
                "BookRead",
                $"书籍已下载，但书架数据保存失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 同步设置页中更新后的书源列表。
    /// </summary>
    /// <param name="sender">发起事件的设置页面。</param>
    /// <param name="e">最新书源列表。</param>
    /// <returns>无。</returns>
    private void SettingsPageControl_OpdsSourcesChanged(object? sender, IReadOnlyList<OpdsSource> e)
    {
        _opdsSources = e.ToList();
        RefreshSourceSnapshots();
    }

    /// <summary>
    /// 响应 OPDS 浏览页返回书架请求。
    /// </summary>
    /// <param name="sender">发起事件的浏览页面。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void OpdsPageControl_BackToShelfRequested(object? sender, RoutedEventArgs e)
    {
        ShowShelfPage();
    }

    /// <summary>
    /// 响应 OPDS 浏览页打开设置页请求。
    /// </summary>
    /// <param name="sender">发起事件的浏览页面。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void OpdsPageControl_SettingsRequested(object? sender, EventArgs e)
    {
        ShowSettingsPage();
        SettingsPageControl.LoadOpdsSources();
    }

    /// <summary>
    /// 显示 OPDS 浏览页并恢复其宽屏尺寸。
    /// </summary>
    /// <returns>无。</returns>
    private void ShowOpdsBrowsePage()
    {
        WindowState = WindowState.Normal;
        MinWidth = 500;
        MinHeight = 600;
        Width = OpdsWindowWidth;
        CenterWindowOnScreen();
        ShelfPageControl.Visibility = Visibility.Collapsed;
        ReaderPageControl.Visibility = Visibility.Collapsed;
        SettingsPageControl.Visibility = Visibility.Collapsed;
        OpdsPageControl.Visibility = Visibility.Visible;
        _isOpdsPageVisible = true;
        SetTitleBarVisible(true);
        TitleBarControl.SetBackButtonVisible(false);
        TitleBarControl.ClearBookInfo();
        UpdateWindowFrameClip();
        OpdsPageControl.LoadSources();
    }

    /// <summary>
    /// 显示书架页并恢复其紧凑窗口尺寸。
    /// </summary>
    /// <returns>无。</returns>
    private void ShowShelfPage()
    {
        // 先恢复普通窗口并设置最小宽度，避免之前的阅读页约束阻止窗口缩小。
        WindowState = WindowState.Normal;
        MinWidth = ShelfWindowWidth;
        MinHeight = 600;
        Width = ShelfWindowWidth;
        CenterWindowOnScreen();
        ShelfPageControl.Visibility = Visibility.Visible;
        ReaderPageControl.Visibility = Visibility.Collapsed;
        SettingsPageControl.Visibility = Visibility.Collapsed;
        OpdsPageControl.Visibility = Visibility.Collapsed;
        _isOpdsPageVisible = false;
        SetTitleBarVisible(true);
        TitleBarControl.SetBackButtonVisible(false);
        TitleBarControl.ClearBookInfo();
        _currentChapterTitle = null;
        UpdateWindowFrameClip();
    }

    /// <summary>
    /// 显示阅读页并恢复正文排版所需的窗口尺寸。
    /// </summary>
    /// <returns>无。</returns>
    private void ShowReaderPage()
    {
        WindowState = WindowState.Normal;
        // 阅读页保留足够的排版空间，同时允许窗口缩放到默认阅读尺寸的一半。
        MinWidth = 200;
		MinHeight = 100;
        Width = ReaderWindowWidth;
        CenterWindowOnScreen();
        ShelfPageControl.Visibility = Visibility.Collapsed;
        ReaderPageControl.Visibility = Visibility.Visible;
        SettingsPageControl.Visibility = Visibility.Collapsed;
        OpdsPageControl.Visibility = Visibility.Collapsed;
        _isOpdsPageVisible = false;
        SetTitleBarVisible(!ReaderPageControl.IsReaderBackgroundTransparent);
        TitleBarControl.SetBackButtonVisible(true);
        UpdateTitleBarReadingTitle();
        UpdateWindowFrameClip();
    }

    /// <summary>
    /// 显示设置页，并记录设置页打开前显示的页面。
    /// </summary>
    /// <returns>无。</returns>
    private void ShowSettingsPage()
    {
        if (SettingsPageControl.Visibility == Visibility.Visible)
        {
            return;
        }

        _settingsOpenedFromReader = ReaderPageControl.Visibility == Visibility.Visible;
        SettingsPageControl.LoadSettings(_shortcutSettings);
        ShelfPageControl.Visibility = Visibility.Collapsed;
        ReaderPageControl.Visibility = Visibility.Collapsed;
        SettingsPageControl.Visibility = Visibility.Visible;
        OpdsPageControl.Visibility = Visibility.Collapsed;
        _isOpdsPageVisible = false;
        SetTitleBarVisible(true);
        TitleBarControl.SetBackButtonVisible(false);
        TitleBarControl.ClearBookInfo();
        UpdateWindowFrameClip();
    }

    /// <summary>
    /// 设置标题栏的显示状态，并同步调整窗口顶部行高。
    /// </summary>
    /// <param name="isVisible">是否显示标题栏。</param>
    /// <returns>无。</returns>
    private void SetTitleBarVisible(bool isVisible)
    {
        TitleBarControl.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        // 同时收起标题栏所在行，避免隐藏控件后窗口顶部仍保留空白区域。
        TitleBarRow.Height = new GridLength(isVisible ? 64 : 0);
    }

    /// <summary>
    /// 根据窗口内容尺寸更新圆角裁剪区域，确保各页面内容不会溢出窗口边界。
    /// </summary>
    /// <param name="sender">尺寸发生变化的窗口内容边框。</param>
    /// <param name="e">尺寸变化事件参数。</param>
    /// <returns>无。</returns>
    private void WindowFrame_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowFrameClip();
    }

    /// <summary>
    /// 根据当前窗口尺寸和透明模式更新边框裁剪圆角。
    /// </summary>
    /// <returns>无。</returns>
    private void UpdateWindowFrameClip()
    {
        if (ReaderPageControl.Visibility == Visibility.Visible && ReaderPageControl.IsReaderBackgroundTransparent)
        {
            WindowFrame.Clip = null;
            return;
        }

        WindowFrame.Clip = new RectangleGeometry(
            new Rect(0, 0, WindowFrame.ActualWidth, WindowFrame.ActualHeight),
            12,
            12);
    }

    /// <summary>
    /// 从设置页返回设置页打开前的页面。
    /// </summary>
    /// <returns>无。</returns>
    internal void NavigateBackFromSettings()
    {
        if (_settingsOpenedFromReader && !string.IsNullOrWhiteSpace(_currentBookPath))
        {
            ShowReaderPage();
            return;
        }

        ShowShelfPage();
    }

    /// <summary>
    /// 将窗口移动到当前系统工作区的中心位置。
    /// </summary>
    /// <returns>无。</returns>
    private void CenterWindowOnScreen()
    {
        var workArea = SystemParameters.WorkArea;

        // 调整窗口尺寸后重新计算两个坐标，确保窗口以自身中心对齐屏幕中心，而不是只向右扩展。
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;
    }

    /// <summary>
    /// 响应标题栏返回请求，切回书架页。
    /// </summary>
    /// <param name="sender">触发事件的标题栏控件。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void TitleBarControl_ShelfRequested(object sender, RoutedEventArgs e)
    {
        if (_isOpdsPageVisible)
        {
            ShowOpdsBrowsePage();
            return;
        }

        ShowShelfPage();
    }

    /// <summary>
    /// 响应标题栏设置请求并打开设置页。
    /// </summary>
    /// <param name="sender">触发设置请求的标题栏控件。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void TitleBarControl_SettingsRequested(object? sender, RoutedEventArgs e)
    {
        ShowSettingsPage();
    }

    /// <summary>
    /// 应用设置页保存的应用配置并异步持久化。
    /// </summary>
    /// <param name="sender">提交设置的设置页面。</param>
    /// <param name="e">包含新的应用设置的事件参数。</param>
    /// <returns>无。</returns>
    private async void SettingsPageControl_SettingsSaved(object? sender, ShortcutSettingsChangedEventArgs e)
    {
        _shortcutSettings = e.Settings.Clone();
        ReaderPageControl.ApplyShortcutSettings(_shortcutSettings);

        try
        {
            await _shortcutSettingsStore.SaveAsync(_shortcutSettings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ConfirmationDialog.ShowAlert(
                this,
                "BookRead",
                $"快捷键设置保存失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 响应设置页取消或保存后的返回请求。
    /// </summary>
    /// <param name="sender">发起返回请求的设置页面。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void SettingsPageControl_BackRequested(object? sender, RoutedEventArgs e)
    {
        NavigateBackFromSettings();
    }

    /// <summary>
    /// 在透明背景且标题栏隐藏时，捕获阅读区域的鼠标按下事件以拖动窗口。
    /// </summary>
    /// <param name="sender">接收鼠标预览事件的主窗口。</param>
    /// <param name="e">鼠标按键事件参数。</param>
    /// <returns>无。</returns>
    /// <exception cref="InvalidOperationException">窗口当前无法执行拖动时抛出。</exception>
    private void MainWindow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            TitleBarControl.Visibility != Visibility.Collapsed ||
            ReaderPageControl.Visibility != Visibility.Visible ||
            IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        DragMove();
        e.Handled = true;
    }

    /// <summary>
    /// 判断鼠标事件源是否位于按钮、输入框或滚动条等交互控件内。
    /// </summary>
    /// <param name="source">鼠标事件的原始视觉元素。</param>
    /// <returns>位于交互控件内时返回 true，否则返回 false。</returns>
    private static bool IsInteractiveElement(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is ButtonBase or TextBox or ScrollBar or Thumb)
            {
                return true;
            }

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                ContentElement contentElement => ContentOperations.GetParent(contentElement),
                _ => null
            };
        }

        return false;
    }

    /// <summary>
    /// 处理应用全局快捷键及阅读页的翻页和滚动快捷键。
    /// </summary>
    /// <param name="sender">接收键盘事件的主窗口。</param>
    /// <param name="e">包含按键和修饰键状态的事件参数。</param>
    /// <returns>无。</returns>
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ReaderPageControl.HandleGlobalKeyboardInput(e.Key, Keyboard.Modifiers))
        {
            e.Handled = true;
            return;
        }

        if (ReaderPageControl.Visibility != Visibility.Visible)
        {
            return;
        }

        if (ReaderPageControl.HandleKeyboardInput(e.Key, Keyboard.Modifiers))
        {
            e.Handled = true;
        }
    }
}
