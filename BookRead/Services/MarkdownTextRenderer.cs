using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BookRead.Models;

namespace BookRead.Services;

/// <summary>
/// 将 Markdown 文本转换为 WPF 文本块，以保留标题、列表和行内样式。
/// </summary>
internal static class MarkdownTextRenderer
{
    private const string BodyFontFamilyName = "Microsoft YaHei";
    private const string CodeFontFamilyName = "Consolas";
    private const string TextPrimaryResourceKey = "TextPrimary";
    private const string TextSecondaryResourceKey = "TextSecondary";
    private const string TextMutedResourceKey = "TextMuted";
    private const string PanelResourceKey = "Panel";
    private const string LineResourceKey = "Line";

    private static readonly FontFamily BodyFontFamily = new(BodyFontFamilyName);
    private static readonly FontFamily CodeFontFamily = new(CodeFontFamilyName);

    private static readonly Regex HeadingRegex = new(
        @"^[ \t]{0,3}(?<marker>#{1,6})[ \t]+(?<text>.+?)[ \t]*#*[ \t]*$",
        RegexOptions.Compiled);

    private static readonly Regex UnorderedListItemRegex = new(
        @"^[ \t]{0,3}[-+*][ \t]+(?<text>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex OrderedListItemRegex = new(
        @"^[ \t]{0,3}(?<number>\d{1,9})[.)][ \t]+(?<text>.+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// 将 Markdown 页面内容转换并添加到指定面板。
    /// </summary>
    /// <param name="target">接收渲染结果的面板。</param>
    /// <param name="markdown">当前页面中的原始 Markdown 文本。</param>
    /// <param name="fontSize">正文基础字号。</param>
    /// <param name="lineSpacing">正文行间距倍数。</param>
    /// <returns>无。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> 或 <paramref name="markdown"/> 为 <see langword="null"/> 时抛出。</exception>
    public static void Render(StackPanel target, string markdown, double fontSize, double lineSpacing)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(markdown);

        target.Children.Clear();
        AppendMarkdownContent(target, markdown, fontSize, lineSpacing);
    }

    /// <summary>
    /// 将整本 Markdown 按顺序渲染到同一页面，并返回每章标题对应的锚点元素。
    /// </summary>
    /// <param name="target">接收整本 Markdown 内容的面板。</param>
    /// <param name="chapters">按原文顺序排列的 Markdown 章节。</param>
    /// <param name="fontSize">正文基础字号。</param>
    /// <param name="lineSpacing">正文行间距倍数。</param>
    /// <returns>与章节列表顺序一致的标题锚点集合。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> 或 <paramref name="chapters"/> 为 <see langword="null"/> 时抛出。</exception>
    internal static IReadOnlyList<TextBlock> RenderDocument(
        StackPanel target,
        IReadOnlyList<BookChapter> chapters,
        double fontSize,
        double lineSpacing)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(chapters);

        target.Children.Clear();
        var headings = new List<TextBlock>(chapters.Count);
        foreach (BookChapter chapter in chapters)
        {
            int level = chapter.MarkdownHeadingLevel is >= 1 and <= 6
                ? chapter.MarkdownHeadingLevel
                : 1;
            TextBlock heading = CreateHeadingBlock(level, chapter.Title, fontSize, lineSpacing);
            target.Children.Add(heading);
            headings.Add(heading);
            AppendMarkdownContent(target, chapter.Content, fontSize, lineSpacing);
        }

        return headings;
    }

    /// <summary>
    /// 将 Markdown 文本追加到指定面板，不清空面板中的现有内容。
    /// </summary>
    /// <param name="target">接收渲染结果的面板。</param>
    /// <param name="markdown">要渲染的原始 Markdown 文本。</param>
    /// <param name="fontSize">正文基础字号。</param>
    /// <param name="lineSpacing">正文行间距倍数。</param>
    /// <returns>无。</returns>
    private static void AppendMarkdownContent(
        StackPanel target,
        string markdown,
        double fontSize,
        double lineSpacing)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return;
        }

        string normalized = NormalizeLineEndings(markdown);
        string[] lines = normalized.Split('\n');
        var paragraph = new StringBuilder();
        var codeBlock = new StringBuilder();
        string? fenceMarker = null;

        foreach (string line in lines)
        {
            if (TryGetFenceMarker(line, out string currentFence))
            {
                if (fenceMarker is null)
                {
                    FlushParagraph(target, paragraph, fontSize, lineSpacing);
                    fenceMarker = currentFence;
                    codeBlock.Clear();
                }
                else if (IsClosingFence(line, fenceMarker))
                {
                    AddCodeBlock(target, codeBlock, fontSize, lineSpacing);
                    fenceMarker = null;
                }
                else
                {
                    codeBlock.AppendLine(line);
                }

                continue;
            }

            if (fenceMarker is not null)
            {
                codeBlock.AppendLine(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(target, paragraph, fontSize, lineSpacing);
                continue;
            }

            Match headingMatch = HeadingRegex.Match(line);
            if (headingMatch.Success)
            {
                FlushParagraph(target, paragraph, fontSize, lineSpacing);
                TextBlock heading = CreateHeadingBlock(
                    headingMatch.Groups["marker"].Value.Length,
                    headingMatch.Groups["text"].Value.Trim(),
                    fontSize,
                    lineSpacing);
                target.Children.Add(heading);
                continue;
            }

            if (IsThematicBreak(line))
            {
                FlushParagraph(target, paragraph, fontSize, lineSpacing);
                AddThematicBreak(target);
                continue;
            }

            Match unorderedListMatch = UnorderedListItemRegex.Match(line);
            if (unorderedListMatch.Success)
            {
                FlushParagraph(target, paragraph, fontSize, lineSpacing);
                AddListItem(target, "• ", unorderedListMatch.Groups["text"].Value, fontSize, lineSpacing);
                continue;
            }

            Match orderedListMatch = OrderedListItemRegex.Match(line);
            if (orderedListMatch.Success)
            {
                FlushParagraph(target, paragraph, fontSize, lineSpacing);
                AddListItem(
                    target,
                    $"{orderedListMatch.Groups["number"].Value}. ",
                    orderedListMatch.Groups["text"].Value,
                    fontSize,
                    lineSpacing);
                continue;
            }

            if (TryGetBlockQuoteText(line, out string quoteText))
            {
                FlushParagraph(target, paragraph, fontSize, lineSpacing);
                AddBlockQuote(target, quoteText, fontSize, lineSpacing);
                continue;
            }

            if (paragraph.Length > 0)
            {
                paragraph.Append('\n');
            }

            paragraph.Append(line.TrimEnd());
        }

        if (fenceMarker is not null)
        {
            AddCodeBlock(target, codeBlock, fontSize, lineSpacing);
        }

        FlushParagraph(target, paragraph, fontSize, lineSpacing);
    }

    /// <summary>
    /// 将当前收集的段落内容转换为普通正文块。
    /// </summary>
    /// <param name="target">接收文本块的面板。</param>
    /// <param name="paragraph">段落文本缓冲区。</param>
    /// <param name="fontSize">正文基础字号。</param>
    /// <param name="lineSpacing">正文行间距倍数。</param>
    /// <returns>无。</returns>
    private static void FlushParagraph(
        StackPanel target,
        StringBuilder paragraph,
        double fontSize,
        double lineSpacing)
    {
        if (paragraph.Length == 0)
        {
            return;
        }

        TextBlock textBlock = CreateTextBlock(
            fontSize,
            lineSpacing,
            TextSecondaryResourceKey,
            new Thickness(0, 0, 0, 12));
        AppendInlineContent(textBlock.Inlines, paragraph.ToString(), default);
        target.Children.Add(textBlock);
        paragraph.Clear();
    }

    /// <summary>
    /// 创建一个 Markdown 标题文本块。
    /// </summary>
    /// <param name="level">标题级别，范围为 1 到 6。</param>
    /// <param name="text">标题文本。</param>
    /// <param name="fontSize">正文基础字号。</param>
    /// <param name="lineSpacing">正文行间距倍数。</param>
    /// <returns>初始化后的标题文本块。</returns>
    private static TextBlock CreateHeadingBlock(
        int level,
        string text,
        double fontSize,
        double lineSpacing)
    {
        double headingFontSize = fontSize * level switch
        {
            1 => 1.7,
            2 => 1.5,
            3 => 1.3,
            4 => 1.15,
            5 => 1.05,
            _ => 1
        };

        TextBlock textBlock = CreateTextBlock(
            headingFontSize,
            lineSpacing,
            TextPrimaryResourceKey,
            new Thickness(0, 6, 0, 14),
            FontWeights.SemiBold);
        AppendInlineContent(textBlock.Inlines, text, new InlineFormat(Bold: true));
        return textBlock;
    }

    /// <summary>
    /// 添加一个 Markdown 无序或有序列表项。
    /// </summary>
    /// <param name="target">接收文本块的面板。</param>
    /// <param name="prefix">列表项前缀，例如圆点或序号。</param>
    /// <param name="text">列表项正文。</param>
    /// <param name="fontSize">正文基础字号。</param>
    /// <param name="lineSpacing">正文行间距倍数。</param>
    /// <returns>无。</returns>
    private static void AddListItem(
        StackPanel target,
        string prefix,
        string text,
        double fontSize,
        double lineSpacing)
    {
        TextBlock textBlock = CreateTextBlock(
            fontSize,
            lineSpacing,
            TextSecondaryResourceKey,
            new Thickness(12, 0, 0, 5));
        textBlock.Inlines.Add(new Run(prefix));
        AppendInlineContent(textBlock.Inlines, text, default);
        target.Children.Add(textBlock);
    }

    /// <summary>
    /// 添加一个 Markdown 引用块。
    /// </summary>
    /// <param name="target">接收文本块的面板。</param>
    /// <param name="text">引用正文。</param>
    /// <param name="fontSize">正文基础字号。</param>
    /// <param name="lineSpacing">正文行间距倍数。</param>
    /// <returns>无。</returns>
    private static void AddBlockQuote(
        StackPanel target,
        string text,
        double fontSize,
        double lineSpacing)
    {
        TextBlock textBlock = CreateTextBlock(
            fontSize,
            lineSpacing,
            TextMutedResourceKey,
            new Thickness(10, 0, 0, 10),
            FontWeights.Normal,
            FontStyles.Italic);
        textBlock.Inlines.Add(new Run("│ "));
        AppendInlineContent(textBlock.Inlines, text, new InlineFormat(Italic: true));
        target.Children.Add(textBlock);
    }

    /// <summary>
    /// 添加一个 Markdown 围栏代码块。
    /// </summary>
    /// <param name="target">接收代码块的面板。</param>
    /// <param name="codeBlock">已收集的代码内容。</param>
    /// <param name="fontSize">正文基础字号。</param>
    /// <param name="lineSpacing">正文行间距倍数。</param>
    /// <returns>无。</returns>
    private static void AddCodeBlock(
        StackPanel target,
        StringBuilder codeBlock,
        double fontSize,
        double lineSpacing)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4, 0, 14)
        };
        border.SetResourceReference(Border.BackgroundProperty, PanelResourceKey);
        border.SetResourceReference(Border.BorderBrushProperty, LineResourceKey);

        TextBlock textBlock = CreateTextBlock(
            fontSize * 0.92,
            lineSpacing,
            TextSecondaryResourceKey,
            new Thickness(0));
        textBlock.FontFamily = CodeFontFamily;
        textBlock.Text = codeBlock.ToString().TrimEnd();
        border.Child = textBlock;
        target.Children.Add(border);
        codeBlock.Clear();
    }

    /// <summary>
    /// 添加一个 Markdown 分隔线。
    /// </summary>
    /// <param name="target">接收分隔线的面板。</param>
    /// <returns>无。</returns>
    private static void AddThematicBreak(StackPanel target)
    {
        var border = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 12, 0, 18)
        };
        border.SetResourceReference(Border.BackgroundProperty, LineResourceKey);
        target.Children.Add(border);
    }

    /// <summary>
    /// 创建具有统一字体、行高和主题前景色的文本块。
    /// </summary>
    /// <param name="fontSize">文本字号。</param>
    /// <param name="lineSpacing">行间距倍数。</param>
    /// <param name="foregroundResourceKey">主题前景色资源键。</param>
    /// <param name="margin">文本块外边距。</param>
    /// <param name="fontWeight">字体粗细。</param>
    /// <param name="fontStyle">字体样式。</param>
    /// <returns>初始化后的文本块。</returns>
    private static TextBlock CreateTextBlock(
        double fontSize,
        double lineSpacing,
        string foregroundResourceKey,
        Thickness margin,
        FontWeight? fontWeight = null,
        FontStyle? fontStyle = null)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = BodyFontFamily,
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeights.Normal,
            FontStyle = fontStyle ?? FontStyles.Normal,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            LineHeight = fontSize * lineSpacing,
            Margin = margin
        };
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, foregroundResourceKey);
        return textBlock;
    }

    /// <summary>
    /// 将包含 Markdown 行内标记的文本追加到指定行内集合。
    /// </summary>
    /// <param name="target">接收格式化文本的行内集合。</param>
    /// <param name="text">包含 Markdown 行内标记的文本。</param>
    /// <param name="format">当前文本继承的格式。</param>
    /// <returns>无。</returns>
    private static void AppendInlineContent(InlineCollection target, string text, InlineFormat format)
    {
        var index = 0;
        while (index < text.Length)
        {
            char current = text[index];

            if (current == '\\' && index + 1 < text.Length && IsEscapableCharacter(text[index + 1]))
            {
                AddRun(target, text[index + 1].ToString(), format);
                index += 2;
                continue;
            }

            if (current == '\n')
            {
                target.Add(new LineBreak());
                index++;
                continue;
            }

            if (current == '`' && TryReadDelimited(text, index, "`", out string codeText, out int codeEnd))
            {
                AddRun(target, codeText, format with { Code = true });
                index = codeEnd;
                continue;
            }

            if (TryReadDelimited(text, index, "**", out string strongText, out int strongEnd))
            {
                AddNestedInline(target, strongText, format with { Bold = true }, strongEnd, ref index);
                continue;
            }

            if (TryReadDelimited(text, index, "__", out strongText, out strongEnd))
            {
                AddNestedInline(target, strongText, format with { Bold = true }, strongEnd, ref index);
                continue;
            }

            if (TryReadDelimited(text, index, "~~", out string strikeText, out int strikeEnd))
            {
                AddNestedInline(target, strikeText, format with { Strikethrough = true }, strikeEnd, ref index);
                continue;
            }

            if (current == '*' && TryReadDelimited(text, index, "*", out string italicText, out int italicEnd))
            {
                AddNestedInline(target, italicText, format with { Italic = true }, italicEnd, ref index);
                continue;
            }

            if (current == '_' && TryReadDelimited(text, index, "_", out italicText, out italicEnd))
            {
                AddNestedInline(target, italicText, format with { Italic = true }, italicEnd, ref index);
                continue;
            }

            if (current == '!' && TryReadLinkOrImage(text, index, out string imageText, out int imageEnd, out bool isImage))
            {
                AddRun(
                    target,
                    isImage ? $"图片：{imageText}" : imageText,
                    format with { Italic = isImage, Underline = !isImage });
                index = imageEnd;
                continue;
            }

            if (current == '[' && TryReadLinkOrImage(text, index, out string linkText, out int linkEnd, out isImage))
            {
                AddRun(target, linkText, format with { Underline = !isImage });
                index = linkEnd;
                continue;
            }

            int nextIndex = FindNextInlineMarker(text, index + 1);
            AddRun(target, text[index..nextIndex], format);
            index = nextIndex;
        }
    }

    /// <summary>
    /// 递归添加带有嵌套格式的 Markdown 行内内容。
    /// </summary>
    /// <param name="target">接收格式化文本的行内集合。</param>
    /// <param name="text">嵌套标记内部的文本。</param>
    /// <param name="format">嵌套内容使用的格式。</param>
    /// <param name="endIndex">嵌套标记结束后的字符索引。</param>
    /// <param name="index">当前解析位置，方法完成后更新为 <paramref name="endIndex"/>。</param>
    /// <returns>无。</returns>
    private static void AddNestedInline(
        InlineCollection target,
        string text,
        InlineFormat format,
        int endIndex,
        ref int index)
    {
        var span = new Span();
        AppendInlineContent(span.Inlines, text, format);
        target.Add(span);
        index = endIndex;
    }

    /// <summary>
    /// 尝试读取由相同标记包围的 Markdown 行内内容。
    /// </summary>
    /// <param name="text">待解析文本。</param>
    /// <param name="startIndex">起始标记位置。</param>
    /// <param name="marker">开始和结束标记。</param>
    /// <param name="innerText">标记内部的文本。</param>
    /// <param name="endIndex">结束标记之后的字符索引。</param>
    /// <returns>成功读取非空内容时返回 true，否则返回 false。</returns>
    private static bool TryReadDelimited(
        string text,
        int startIndex,
        string marker,
        out string innerText,
        out int endIndex)
    {
        innerText = string.Empty;
        endIndex = startIndex;
        if (!text.AsSpan(startIndex).StartsWith(marker, StringComparison.Ordinal))
        {
            return false;
        }

        int innerStart = startIndex + marker.Length;
        int closingIndex = text.IndexOf(marker, innerStart, StringComparison.Ordinal);
        if (closingIndex <= innerStart)
        {
            return false;
        }

        innerText = text[innerStart..closingIndex];
        endIndex = closingIndex + marker.Length;
        return true;
    }

    /// <summary>
    /// 尝试读取 Markdown 链接或图片标记。
    /// </summary>
    /// <param name="text">待解析文本。</param>
    /// <param name="startIndex">链接或图片标记的起始位置。</param>
    /// <param name="label">链接或图片的显示文本。</param>
    /// <param name="endIndex">结束括号之后的字符索引。</param>
    /// <param name="isImage">当前标记是否为图片时返回 true。</param>
    /// <returns>成功读取标记时返回 true，否则返回 false。</returns>
    private static bool TryReadLinkOrImage(
        string text,
        int startIndex,
        out string label,
        out int endIndex,
        out bool isImage)
    {
        label = string.Empty;
        endIndex = startIndex;
        isImage = startIndex + 1 < text.Length && text[startIndex] == '!' && text[startIndex + 1] == '[';
        int labelStart = startIndex + (isImage ? 2 : 1);
        if (!isImage && text[startIndex] != '[')
        {
            return false;
        }

        int labelEnd = text.IndexOf(']', labelStart);
        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
        {
            return false;
        }

        int urlEnd = text.IndexOf(')', labelEnd + 2);
        if (urlEnd < 0)
        {
            return false;
        }

        label = text[labelStart..labelEnd];
        endIndex = urlEnd + 1;
        return true;
    }

    /// <summary>
    /// 查找下一个需要单独解析的 Markdown 行内标记。
    /// </summary>
    /// <param name="text">待解析文本。</param>
    /// <param name="startIndex">开始查找的字符索引。</param>
    /// <returns>下一个标记的索引；不存在时返回文本长度。</returns>
    private static int FindNextInlineMarker(string text, int startIndex)
    {
        for (int index = startIndex; index < text.Length; index++)
        {
            char current = text[index];
            if (current is '\\' or '`' or '*' or '_' or '~' or '[' or '\n')
            {
                return index;
            }

            if (current == '!' && index + 1 < text.Length && text[index + 1] == '[')
            {
                return index;
            }
        }

        return text.Length;
    }

    /// <summary>
    /// 添加一个按当前格式设置样式的文本运行。
    /// </summary>
    /// <param name="target">接收文本运行的行内集合。</param>
    /// <param name="text">要显示的文本。</param>
    /// <param name="format">文本格式。</param>
    /// <returns>无。</returns>
    private static void AddRun(InlineCollection target, string text, InlineFormat format)
    {
        if (text.Length == 0)
        {
            return;
        }

        var run = new Run(text);
        if (format.Bold)
        {
            run.FontWeight = FontWeights.Bold;
        }

        if (format.Italic)
        {
            run.FontStyle = FontStyles.Italic;
        }

        if (format.Code)
        {
            run.FontFamily = CodeFontFamily;
        }

        if (format.Strikethrough)
        {
            run.TextDecorations = TextDecorations.Strikethrough;
        }
        else if (format.Underline)
        {
            run.TextDecorations = TextDecorations.Underline;
        }

        target.Add(run);
    }

    /// <summary>
    /// 判断字符是否可以通过反斜杠转义为普通文本。
    /// </summary>
    /// <param name="character">待判断字符。</param>
    /// <returns>可以转义时返回 true，否则返回 false。</returns>
    private static bool IsEscapableCharacter(char character)
    {
        return "\\`*_{}[]()#+-.!>~|".Contains(character);
    }

    /// <summary>
    /// 尝试识别围栏代码块的开始或结束标记。
    /// </summary>
    /// <param name="line">当前 Markdown 行。</param>
    /// <param name="marker">识别到的围栏标记。</param>
    /// <returns>识别到三个以上反引号或波浪号时返回 true，否则返回 false。</returns>
    private static bool TryGetFenceMarker(string line, out string marker)
    {
        string trimmed = line.TrimStart();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            marker = "```";
            return true;
        }

        if (trimmed.StartsWith("~~~", StringComparison.Ordinal))
        {
            marker = "~~~";
            return true;
        }

        marker = string.Empty;
        return false;
    }

    /// <summary>
    /// 判断当前行是否关闭指定的围栏代码块。
    /// </summary>
    /// <param name="line">当前 Markdown 行。</param>
    /// <param name="fenceMarker">已打开的围栏标记。</param>
    /// <returns>当前行可关闭围栏时返回 true，否则返回 false。</returns>
    private static bool IsClosingFence(string line, string fenceMarker)
    {
        string trimmed = line.TrimStart();
        return trimmed.StartsWith(fenceMarker, StringComparison.Ordinal);
    }

    /// <summary>
    /// 尝试读取 Markdown 引用行中的正文。
    /// </summary>
    /// <param name="line">当前 Markdown 行。</param>
    /// <param name="quoteText">引用正文。</param>
    /// <returns>当前行为引用行时返回 true，否则返回 false。</returns>
    private static bool TryGetBlockQuoteText(string line, out string quoteText)
    {
        string trimmed = line.TrimStart();
        if (!trimmed.StartsWith('>'))
        {
            quoteText = string.Empty;
            return false;
        }

        quoteText = trimmed[1..].TrimStart();
        return true;
    }

    /// <summary>
    /// 判断当前行是否为 Markdown 分隔线。
    /// </summary>
    /// <param name="line">当前 Markdown 行。</param>
    /// <returns>当前行是分隔线时返回 true，否则返回 false。</returns>
    private static bool IsThematicBreak(string line)
    {
        string compact = line.Replace(" ", string.Empty).Replace("\t", string.Empty);
        return compact.Length >= 3 &&
               (compact.All(character => character == '-') ||
                compact.All(character => character == '*') ||
                compact.All(character => character == '_'));
    }

    /// <summary>
    /// 将不同平台的换行符统一为换行符，便于按行解析 Markdown。
    /// </summary>
    /// <param name="content">需要规范化的文本。</param>
    /// <returns>使用换行符分隔的文本。</returns>
    private static string NormalizeLineEndings(string content)
    {
        return content.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    /// <summary>
    /// 表示 Markdown 行内文本的样式状态。
    /// </summary>
    /// <param name="Bold">是否使用粗体。</param>
    /// <param name="Italic">是否使用斜体。</param>
    /// <param name="Strikethrough">是否使用删除线。</param>
    /// <param name="Code">是否使用等宽代码字体。</param>
    /// <param name="Underline">是否使用下划线。</param>
    private readonly record struct InlineFormat(
        bool Bold = false,
        bool Italic = false,
        bool Strikethrough = false,
        bool Code = false,
        bool Underline = false);
}