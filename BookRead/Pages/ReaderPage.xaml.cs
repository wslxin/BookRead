using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BookRead.Models;
using BookRead.Services;

namespace BookRead.Pages;

/// <summary>
/// 阅读页面，负责文本加载、分页阅读、章节面板、字号和行间距调整。
/// </summary>
public partial class ReaderPage : UserControl
{
    /// <summary>当前阅读位置发生变化时触发。</summary>
    internal event EventHandler<ReaderProgressChangedEventArgs>? ProgressChanged;

    /// <summary>当前书籍信息加载完成或发生变化时触发。</summary>
    internal event EventHandler<ReaderBookInfoChangedEventArgs>? BookInfoChanged;

    /// <summary>阅读页透明背景状态发生变化时触发。</summary>
    internal event EventHandler? ReaderTransparencyChanged;

    private double _fontSize = 17;
    private double _lineSpacing = 1.6;
    private bool _isReaderBackgroundTransparent;
    private ShortcutSettings _shortcutSettings = ShortcutSettings.CreateDefault();
    private readonly DispatcherTimer _scrollbarHideTimer;
    private readonly DispatcherTimer _styleNotificationHideTimer;
    private readonly List<BookChapter> _chapters = [];
    private readonly List<string> _bookPages = [];
    private int _currentChapter;
    private int _currentBookPage;
    private bool _changingBookPage;
    private const int BookPageLength = 12000;
    private const double MinimumLineSpacing = 1.2;
    private const double MaximumLineSpacing = 2.4;
    private const double LineSpacingStep = 0.1;
    private static readonly Thickness NormalReaderPadding = new(50, 26, 50, 55);
    private static readonly Thickness TransparentReaderPadding = new(0);

    /// <summary>当前阅读页是否启用透明背景。</summary>
    internal bool IsReaderBackgroundTransparent => _isReaderBackgroundTransparent;

    /// <summary>
    /// 初始化阅读页面并设置为空阅读状态。
    /// </summary>
    public ReaderPage()
    {
        InitializeComponent();
        ApplyReaderTextStyle();
        ChapterListPanel.Children.Clear();
        SetEmptyBookState();
        _scrollbarHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(900)
        };
        _scrollbarHideTimer.Tick += ScrollbarHideTimer_Tick;
        _styleNotificationHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1200)
        };
        _styleNotificationHideTimer.Tick += StyleNotificationHideTimer_Tick;
    }

    /// <summary>
    /// 打开指定 TXT 文件并更新阅读页面内容。
    /// </summary>
    /// <param name="filePath">要读取的 TXT 文件路径。</param>
    /// <param name="initialChapterIndex">打开时恢复的章节索引。</param>
    /// <param name="initialPageIndex">打开时恢复的章节内分页索引。</param>
    /// <returns>表示异步读取过程的任务。</returns>
    /// <exception cref="IOException">文件读取失败时抛出。</exception>
    /// <exception cref="UnauthorizedAccessException">没有权限读取文件时抛出。</exception>
    public async Task LoadBookAsync(string filePath, int initialChapterIndex = 0, int initialPageIndex = 0)
    {
        string content = await ReadBookContentAsync(filePath);
        _chapters.Clear();
        _chapters.AddRange(ChapterParser.Parse(content));

        BookInfoChanged?.Invoke(
            this,
            new ReaderBookInfoChangedEventArgs(Path.GetFileNameWithoutExtension(filePath)));
        BuildChapterPanel();
        int chapterIndex = Math.Clamp(initialChapterIndex, 0, _chapters.Count - 1);
        ShowChapter(chapterIndex, initialPageIndex);
    }

    /// <summary>
    /// 使用检测到的文本编码异步读取完整书籍内容。
    /// </summary>
    /// <param name="filePath">要读取的 TXT 文件路径。</param>
    /// <returns>书籍的完整文本。</returns>
    /// <exception cref="IOException">文件读取失败时抛出。</exception>
    /// <exception cref="UnauthorizedAccessException">没有权限读取文件时抛出。</exception>
    private static async Task<string> ReadBookContentAsync(string filePath)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, useAsync: true);
        using var reader = new StreamReader(stream, DetectEncoding(stream), detectEncodingFromByteOrderMarks: true,
            bufferSize: 64 * 1024);
        return await reader.ReadToEndAsync();
    }

    /// <summary>根据文件 BOM 选择文本编码，未带 BOM 时使用 UTF-8。</summary>
    /// <param name="stream">待检测的文件流，调用后位置会恢复到开头。</param>
    /// <returns>检测到的文本编码。</returns>
    /// <exception cref="IOException">读取文件流失败时抛出。</exception>
    private static Encoding DetectEncoding(FileStream stream)
    {
        Span<byte> bom = stackalloc byte[4];
        int read = stream.Read(bom);
        stream.Position = 0;

        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
        {
            return new UTF8Encoding(false);
        }

        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        byte[] sample = new byte[Math.Min(stream.Length, 64 * 1024)];
        int sampleLength = stream.Read(sample, 0, sample.Length);
        stream.Position = 0;
        if (IsValidUtf8(sample, sampleLength))
        {
            return new UTF8Encoding(false, true);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(936);
    }

    /// <summary>验证指定字节序列是否符合 UTF-8 编码规则。</summary>
    /// <param name="bytes">待验证的字节数组。</param>
    /// <param name="length">参与验证的字节数。</param>
    /// <returns>字节序列有效时返回 true，否则返回 false。</returns>
    private static bool IsValidUtf8(byte[] bytes, int length)
    {
        try
        {
            new UTF8Encoding(false, true).GetString(bytes, 0, length);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>为当前书籍创建可点击的章节目录。</summary>
    /// <returns>无。</returns>
    private void BuildChapterPanel()
    {
        ChapterListPanel.Children.Clear();
        for (var index = 0; index < _chapters.Count; index++)
        {
            var chapterButton = new Button
            {
                Content = _chapters[index].Title,
                Tag = index,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 6)
            };
            chapterButton.Click += Chapter_Click;
            ChapterListPanel.Children.Add(chapterButton);
        }

        ChapterCountText.Text = $"共 {_chapters.Count} 章";
    }

    /// <summary>切换到指定章节并显示指定分页。</summary>
    /// <param name="chapterIndex">要显示的章节索引。</param>
    /// <param name="pageIndex">要显示的章节内分页索引。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chapterIndex"/> 超出章节范围时抛出。</exception>
    private void ShowChapter(int chapterIndex, int pageIndex = 0)
    {
        if (chapterIndex < 0 || chapterIndex >= _chapters.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(chapterIndex));
        }

        _currentChapter = chapterIndex;
        _currentBookPage = 0;
        _bookPages.Clear();

        BookChapter chapter = _chapters[chapterIndex];
        // 长章节仍按固定字符数分页，避免一次向 WPF 文本控件提交过多内容导致界面卡顿。
        for (var offset = 0; offset < chapter.Content.Length; offset += BookPageLength)
        {
            int length = Math.Min(BookPageLength, chapter.Content.Length - offset);
            _bookPages.Add(chapter.Content.Substring(offset, length));
        }

        if (_bookPages.Count == 0)
        {
            _bookPages.Add(string.Empty);
        }

        _currentBookPage = Math.Clamp(pageIndex, 0, _bookPages.Count - 1);

        ChapterTitle.Text = chapter.Title;
        ChapterProgressText.Text = $"{chapterIndex + 1} / {_chapters.Count}";
        ShowBookPage();
    }

    /// <summary>将当前虚拟页面显示到阅读控件中。</summary>
    /// <returns>无。</returns>
    private void ShowBookPage()
    {
        if (_bookPages.Count == 0)
        {
            ReaderText.Text = string.Empty;
            ReadingProgressBar.Value = 0;
            return;
        }

        _changingBookPage = true;
        ReaderText.Text = _bookPages[_currentBookPage];
        ReaderScroll.ScrollToTop();
        _changingBookPage = false;
        UpdateReadingProgress();
        ProgressChanged?.Invoke(
            this,
            new ReaderProgressChangedEventArgs(_currentChapter, _currentBookPage));
    }

    /// <summary>
    /// 根据当前章节和章节内分页位置更新整本书的阅读进度。
    /// </summary>
    /// <returns>无。</returns>
    private void UpdateReadingProgress()
    {
        if (_chapters.Count == 0 || _bookPages.Count == 0)
        {
            ReadingProgressBar.Value = 0;
            return;
        }

        int totalPageCount = 0;
        int completedPageCount = 0;

        // 按分页数量而不是章节编号计算，避免长章节和短章节在整本书进度中占据相同权重。
        for (var index = 0; index < _chapters.Count; index++)
        {
            int chapterPageCount = Math.Max(
                1,
                (int)Math.Ceiling((double)_chapters[index].Content.Length / BookPageLength));
            totalPageCount += chapterPageCount;

            if (index < _currentChapter)
            {
                completedPageCount += chapterPageCount;
            }
        }

        double currentPageProgress = (_currentBookPage + 1d) / _bookPages.Count;
        double progress = (completedPageCount + currentPageProgress) / totalPageCount * 100;
        ReadingProgressBar.Value = Math.Clamp(progress, 0, 100);
    }

    /// <summary>
    /// 处理阅读页支持的翻页和滚动快捷键。
    /// </summary>
    /// <param name="key">触发操作的按键。</param>
    /// <param name="modifiers">当前按下的修饰键组合。</param>
    /// <returns>按键已被阅读页处理时返回 true，否则返回 false。</returns>
    internal bool HandleKeyboardInput(Key key, ModifierKeys modifiers)
    {
        if (_bookPages.Count == 0)
        {
            return false;
        }

        if (MatchesShortcut(ShortcutAction.NextPage, key, modifiers))
        {
            MoveToNextBookPage();
            return true;
        }

        if (MatchesShortcut(ShortcutAction.PreviousPage, key, modifiers))
        {
            MoveToPreviousBookPage();
            return true;
        }

        if (MatchesShortcut(ShortcutAction.NextChapter, key, modifiers))
        {
            NextChapter_Click(this, new RoutedEventArgs());
            return true;
        }

        if (MatchesShortcut(ShortcutAction.PreviousChapter, key, modifiers))
        {
            PreviousChapter_Click(this, new RoutedEventArgs());
            return true;
        }

        if (MatchesShortcut(ShortcutAction.ScrollDown, key, modifiers))
        {
            ScrollReader(1);
            return true;
        }

        if (MatchesShortcut(ShortcutAction.ScrollUp, key, modifiers))
        {
            ScrollReader(-1);
            return true;
        }

        if (MatchesShortcut(ShortcutAction.IncreaseFont, key, modifiers))
        {
            ChangeFontSize(1);
            return true;
        }

        if (MatchesShortcut(ShortcutAction.DecreaseFont, key, modifiers))
        {
            ChangeFontSize(-1);
            return true;
        }

        if (MatchesShortcut(ShortcutAction.IncreaseLineSpacing, key, modifiers))
        {
            ChangeLineSpacing(LineSpacingStep);
            return true;
        }

        if (MatchesShortcut(ShortcutAction.DecreaseLineSpacing, key, modifiers))
        {
            ChangeLineSpacing(-LineSpacingStep);
            return true;
        }

        if (MatchesShortcut(ShortcutAction.ToggleTransparency, key, modifiers))
        {
            ToggleReaderTransparency_Click(this, new RoutedEventArgs());
            return true;
        }

        if (MatchesShortcut(ShortcutAction.ToggleTheme, key, modifiers))
        {
            ToggleReadingTheme_Click(this, new RoutedEventArgs());
            return true;
        }

        if (MatchesShortcut(ShortcutAction.ToggleChapter, key, modifiers))
        {
            ToggleChapterPanel_Click(this, new RoutedEventArgs());
            return true;
        }

        return false;
    }

    /// <summary>
    /// 应用设置页保存的阅读页配置。
    /// </summary>
    /// <param name="settings">要应用的阅读页配置。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> 为 null 时抛出。</exception>
    internal void ApplyShortcutSettings(ShortcutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _shortcutSettings = settings.Clone();
        ApplyReaderToolbarVisibility(_shortcutSettings.ShowReaderToolbar);
    }

    /// <summary>
    /// 应用阅读页工具栏的显示状态，并同步调整其所在行的高度。
    /// </summary>
    /// <param name="isVisible">是否显示阅读页底部工具栏。</param>
    /// <returns>无。</returns>
    private void ApplyReaderToolbarVisibility(bool isVisible)
    {
        ReaderToolbar.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        // 同时收起所在行，避免工具栏折叠后仍占据底部空间，让正文区域完整扩展。
        ReaderToolbarRow.Height = new GridLength(isVisible ? 64 : 0);
    }

    /// <summary>
    /// 判断按键和修饰键是否匹配指定操作的当前配置。
    /// </summary>
    /// <param name="action">要匹配的阅读操作。</param>
    /// <param name="key">当前按键。</param>
    /// <param name="modifiers">当前修饰键组合。</param>
    /// <returns>匹配时返回 true，否则返回 false。</returns>
    private bool MatchesShortcut(ShortcutAction action, Key key, ModifierKeys modifiers)
    {
        ShortcutBinding binding = _shortcutSettings.GetBinding(action);
        if (binding.Key == key && binding.Modifiers == modifiers)
        {
            return true;
        }

        // 默认配置沿用原有的空格和翻页键组合；用户改过该项后只使用新的主快捷键。
        return action switch
        {
            ShortcutAction.NextPage when binding == new ShortcutBinding(Key.Right, ModifierKeys.None)
                => modifiers == ModifierKeys.None && key is Key.PageDown or Key.Space,
            ShortcutAction.PreviousPage when binding == new ShortcutBinding(Key.Left, ModifierKeys.None)
                => modifiers == ModifierKeys.None && key == Key.PageUp
                    || modifiers == ModifierKeys.Shift && key == Key.Space,
            _ => false
        };
    }

    /// <summary>
    /// 向后翻到当前书籍的下一页，必要时切换到下一章。
    /// </summary>
    /// <returns>无。</returns>
    private void MoveToNextBookPage()
    {
        if (_bookPages.Count == 0)
        {
            return;
        }

        if (_currentBookPage < _bookPages.Count - 1)
        {
            _currentBookPage++;
            ShowBookPage();
            return;
        }

        if (_currentChapter < _chapters.Count - 1)
        {
            // 翻到章节末页后继续翻页时直接进入下一章，符合连续阅读的预期。
            ShowChapter(_currentChapter + 1);
        }
    }

    /// <summary>
    /// 向前翻到当前书籍的上一页，必要时切换到上一章末页。
    /// </summary>
    /// <returns>无。</returns>
    private void MoveToPreviousBookPage()
    {
        if (_bookPages.Count == 0)
        {
            return;
        }

        if (_currentBookPage > 0)
        {
            _currentBookPage--;
            ShowBookPage();
            return;
        }

        if (_currentChapter > 0)
        {
            // 翻过章节边界后定位到上一章末页，避免上一页快捷键停在空白状态。
            ShowChapter(_currentChapter - 1, int.MaxValue);
        }
    }

    /// <summary>
    /// 按阅读区域高度的比例滚动正文，并保留现有的自动翻页逻辑。
    /// </summary>
    /// <param name="direction">滚动方向，向下为 1，向上为 -1。</param>
    /// <returns>无。</returns>
    private void ScrollReader(int direction)
    {
        if (_bookPages.Count == 0)
        {
            return;
        }

        double scrollDistance = Math.Max(1, ReaderScroll.ViewportHeight * 0.85);
        double targetOffset = ReaderScroll.VerticalOffset + direction * scrollDistance;
        double maximumOffset = Math.Max(0, ReaderScroll.ExtentHeight - ReaderScroll.ViewportHeight);
        ReaderScroll.ScrollToVerticalOffset(Math.Clamp(targetOffset, 0, maximumOffset));
    }

    /// <summary>选择章节并滚动到正文起始位置。</summary>
    /// <param name="sender">触发事件的章节按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void Chapter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int chapterIndex })
        {
            return;
        }

        ShowChapter(chapterIndex);
        ChapterPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>跳转到上一章，并更新章节标题。</summary>
    /// <param name="sender">触发事件的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void PreviousChapter_Click(object sender, RoutedEventArgs e)
    {
        if (_chapters.Count == 0 || _currentChapter == 0)
        {
            return;
        }

        ShowChapter(_currentChapter - 1);
    }

    /// <summary>跳转到下一章，并更新章节标题。</summary>
    /// <param name="sender">触发事件的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void NextChapter_Click(object sender, RoutedEventArgs e)
    {
        if (_chapters.Count == 0 || _currentChapter >= _chapters.Count - 1)
        {
            return;
        }

        ShowChapter(_currentChapter + 1);
    }

    /// <summary>增大正文文字字号。</summary>
    /// <param name="sender">触发事件的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void IncreaseFont_Click(object sender, RoutedEventArgs e)
    {
        ChangeFontSize(1);
    }

    /// <summary>减小正文文字字号。</summary>
    /// <param name="sender">触发事件的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void DecreaseFont_Click(object sender, RoutedEventArgs e)
    {
        ChangeFontSize(-1);
    }

    /// <summary>
    /// 增大正文行间距。
    /// </summary>
    /// <param name="sender">触发操作的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void IncreaseLineSpacing_Click(object sender, RoutedEventArgs e)
    {
        ChangeLineSpacing(LineSpacingStep);
    }

    /// <summary>
    /// 减小正文行间距。
    /// </summary>
    /// <param name="sender">触发操作的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void DecreaseLineSpacing_Click(object sender, RoutedEventArgs e)
    {
        ChangeLineSpacing(-LineSpacingStep);
    }

    /// <summary>
    /// 按指定增量调整正文文字字号，并显示当前设置提示。
    /// </summary>
    /// <param name="delta">字号调整增量。</param>
    /// <returns>无。</returns>
    private void ChangeFontSize(double delta)
    {
        _fontSize = Math.Clamp(_fontSize + delta, 14, 24);
        ApplyReaderTextStyle();
        ShowReaderStyleNotification();
    }

    /// <summary>
    /// 按指定增量调整正文行间距，并显示当前设置提示。
    /// </summary>
    /// <param name="delta">行间距调整增量。</param>
    /// <returns>无。</returns>
    private void ChangeLineSpacing(double delta)
    {
        _lineSpacing = Math.Clamp(
            Math.Round(_lineSpacing + delta, 1),
            MinimumLineSpacing,
            MaximumLineSpacing);
        ApplyReaderTextStyle();
        ShowReaderStyleNotification();
    }

    /// <summary>
    /// 根据当前字号和行间距比例应用正文排版参数。
    /// </summary>
    /// <returns>无。</returns>
    private void ApplyReaderTextStyle()
    {
        ReaderText.FontSize = _fontSize;
        ReaderText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        ReaderText.LineHeight = _fontSize * _lineSpacing;
    }

    /// <summary>
    /// 切换阅读页背景的透明状态，并同步更新按钮提示和反馈信息。
    /// </summary>
    /// <param name="sender">触发透明背景切换的按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void ToggleReaderTransparency_Click(object sender, RoutedEventArgs e)
    {
        _isReaderBackgroundTransparent = !_isReaderBackgroundTransparent;
        if (_isReaderBackgroundTransparent)
        {
            // 分层窗口的完全透明像素可能无法参与鼠标命中测试，因此保留极低 Alpha 以维持拖动能力。
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            ReaderScroll.Padding = TransparentReaderPadding;
            ReaderContentPanel.MaxWidth = double.PositiveInfinity;
            ReaderContentPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            ReaderTransparencyButton.Foreground = (Brush)FindResource("Accent");
            ReaderTransparencyButton.ToolTip = "关闭透明背景";
            ReaderTransparencyChanged?.Invoke(this, EventArgs.Empty);
            ShowReaderNotification("已开启透明背景");
            return;
        }

        // 恢复动态资源引用，确保切换主题后阅读页仍能跟随最新的背景色。
        SetResourceReference(BackgroundProperty, "AppBackground");
        ReaderScroll.Padding = NormalReaderPadding;
        ReaderContentPanel.MaxWidth = 720;
        ReaderContentPanel.HorizontalAlignment = HorizontalAlignment.Center;
        ReaderTransparencyButton.ClearValue(Control.ForegroundProperty);
        ReaderTransparencyButton.ToolTip = "开启透明背景";
        ReaderTransparencyChanged?.Invoke(this, EventArgs.Empty);
        ShowReaderNotification("已关闭透明背景");
    }

    /// <summary>
    /// 显示当前字号和行间距的短暂提示，并重新计时隐藏操作。
    /// </summary>
    /// <returns>无。</returns>
    private void ShowReaderStyleNotification()
    {
        ShowReaderNotification($"字号 {_fontSize:0} · 行距 {_lineSpacing:0.0}");
    }

    /// <summary>
    /// 显示阅读设置反馈并重新计时隐藏操作。
    /// </summary>
    /// <param name="message">要显示的反馈文本。</param>
    /// <returns>无。</returns>
    private void ShowReaderNotification(string message)
    {
        ReaderStyleNotificationText.Text = message;
        ReaderStyleNotification.Visibility = Visibility.Visible;
        _styleNotificationHideTimer.Stop();
        _styleNotificationHideTimer.Start();
    }

    /// <summary>
    /// 隐藏阅读设置操作提示。
    /// </summary>
    /// <param name="sender">触发计时器的对象。</param>
    /// <param name="e">计时器事件参数。</param>
    /// <returns>无。</returns>
    private void StyleNotificationHideTimer_Tick(object? sender, EventArgs e)
    {
        _styleNotificationHideTimer.Stop();
        ReaderStyleNotification.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 在深色和浅色应用主题之间切换，并同步更新主题按钮状态。
    /// </summary>
    /// <param name="sender">触发主题切换的太阳按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void ToggleReadingTheme_Click(object sender, RoutedEventArgs e)
    {
        bool isLightTheme = ApplicationThemeService.Toggle();
        ReadingThemeIcon.Text = isLightTheme ? "\uE708" : "\uE706";
        ReadingThemeButton.ToolTip = isLightTheme ? "切换为深色主题" : "切换为浅色主题";
    }

    /// <summary>切换右侧章节面板的显示状态。</summary>
    /// <param name="sender">触发事件的章节按钮。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无。</returns>
    private void ToggleChapterPanel_Click(object sender, RoutedEventArgs e)
    {
        ChapterPanel.Visibility = ChapterPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>设置未导入书籍时的空阅读状态。</summary>
    /// <returns>无。</returns>
    private void SetEmptyBookState()
    {
        ChapterTitle.Text = "暂无正文";
        ChapterCountText.Text = "暂无章节";
        ChapterProgressText.Text = "— / —";
        ReaderText.Text = string.Empty;
    }

    /// <summary>在滚动发生时显示对应滚动条并重新计时自动隐藏。</summary>
    /// <param name="sender">触发滚动的滚动查看器。</param>
    /// <param name="e">滚动变化事件参数。</param>
    /// <returns>无。</returns>
    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (scrollViewer == ReaderScroll && !_changingBookPage && _bookPages.Count > 1)
        {
            if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 2)
            {
                if (_currentBookPage < _bookPages.Count - 1)
                {
                    _currentBookPage++;
                    ShowBookPage();
                }
            }
            else if (e.VerticalOffset <= 0 && _currentBookPage > 0 && e.VerticalChange < 0)
            {
                _currentBookPage--;
                ShowBookPage();
            }
        }

        foreach (ScrollBar scrollBar in FindVisualChildren<ScrollBar>(scrollViewer))
        {
            scrollBar.Opacity = 1;
        }

        _scrollbarHideTimer.Stop();
        _scrollbarHideTimer.Start();
    }

    /// <summary>滚动停止后隐藏所有滚动条。</summary>
    /// <param name="sender">触发计时器的对象。</param>
    /// <param name="e">计时器事件参数。</param>
    /// <returns>无。</returns>
    private void ScrollbarHideTimer_Tick(object? sender, EventArgs e)
    {
        _scrollbarHideTimer.Stop();
        foreach (ScrollViewer scrollViewer in new[] { ReaderScroll, ChapterScroll })
        {
            foreach (ScrollBar scrollBar in FindVisualChildren<ScrollBar>(scrollViewer))
            {
                scrollBar.ClearValue(UIElement.OpacityProperty);
            }
        }
    }

    /// <summary>递归查找可视树中指定类型的控件。</summary>
    /// <typeparam name="T">要查找的控件类型。</typeparam>
    /// <param name="dependencyObject">可视树的起始节点。</param>
    /// <returns>找到的控件集合。</returns>
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject dependencyObject)
        where T : DependencyObject
    {
        if (dependencyObject is null)
        {
            yield break;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(dependencyObject, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
