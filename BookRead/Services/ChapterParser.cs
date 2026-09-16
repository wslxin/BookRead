using System.Text.RegularExpressions;
using BookRead.Models;

namespace BookRead.Services;

/// <summary>
/// 从纯文本内容中识别并拆分章节。
/// </summary>
internal static class ChapterParser
{
    private static readonly Regex ChapterHeadingRegex = new(
        @"^[\t \u3000]*(?<title>(?:第[\t \u3000]*[0-9０-９一二三四五六七八九十百千万两〇零]+[\t \u3000]*(?:章|节|卷|回|部|篇|集)|(?:卷|篇|部)[\t \u3000]*[0-9０-９一二三四五六七八九十百千万两〇零]+|序章|楔子|引子|序言|前言|后记|尾声|终章|番外(?:[\t \u3000]*[0-9０-９一二三四五六七八九十百千万两〇零]+)?|(?:chapter|chap\.?)\s+[0-9]+)[^\r\n]{0,60})[\t \u3000]*\r?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex MarkdownAtxHeadingRegex = new(
        @"^[ \t]{0,3}(?<marker>#{1,6})[ \t]+(?<title>.+?)[ \t]*#*[ \t]*$",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownFenceRegex = new(
        @"^[ \t]{0,3}(?:`{3,}|~{3,})",
        RegexOptions.Compiled);

    /// <summary>
    /// 识别文本中的章节标题，并按标题所在行拆分正文。
    /// </summary>
    /// <param name="content">完整的书籍文本。</param>
    /// <returns>按原文顺序排列的章节；没有识别到标题时返回一个名为“正文”的章节。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> 为 <see langword="null"/> 时抛出。</exception>
    internal static IReadOnlyList<BookChapter> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        MatchCollection matches = ChapterHeadingRegex.Matches(content);
        if (matches.Count == 0)
        {
            return [new BookChapter("正文", content.Trim())];
        }

        var chapters = new List<BookChapter>(matches.Count + 1);
        string preface = content[..matches[0].Index].Trim();
        if (preface.Length > 0)
        {
            chapters.Add(new BookChapter("前言", preface));
        }

        for (var index = 0; index < matches.Count; index++)
        {
            Match heading = matches[index];
            int bodyStart = SkipLineBreak(content, heading.Index + heading.Length);
            int bodyEnd = index + 1 < matches.Count ? matches[index + 1].Index : content.Length;
            string title = heading.Groups["title"].Value.Trim();
            string body = content[bodyStart..bodyEnd].Trim();
            chapters.Add(new BookChapter(title, body));
        }

        return chapters;
    }

    /// <summary>
    /// 识别 Markdown 标题行，并按标题所在行拆分正文。
    /// </summary>
    /// <param name="markdown">完整的 Markdown 文本。</param>
    /// <returns>按原文顺序排列的章节；没有识别到标题时返回一个名为“正文”的章节。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> 为 <see langword="null"/> 时抛出。</exception>
    internal static IReadOnlyList<BookChapter> ParseMarkdown(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        string content = NormalizeLineEndings(markdown);
        IReadOnlyList<MarkdownHeading> headings = FindMarkdownHeadings(content);
        if (headings.Count == 0)
        {
            return Parse(content);
        }

        var chapters = new List<BookChapter>(headings.Count + 1);
        string preface = content[..headings[0].Start].Trim();
        if (preface.Length > 0)
        {
            chapters.Add(new BookChapter("前言", preface));
        }

        for (var index = 0; index < headings.Count; index++)
        {
            MarkdownHeading heading = headings[index];
            int bodyEnd = index + 1 < headings.Count
                ? headings[index + 1].Start
                : content.Length;
            string body = content[heading.BodyStart..bodyEnd].Trim();
            chapters.Add(new BookChapter(heading.Title, body, heading.Level));
        }

        return chapters;
    }

    /// <summary>
    /// 查找 Markdown 中不在代码围栏内的 ATX 标题行。
    /// </summary>
    /// <param name="markdown">行尾已统一为换行符的 Markdown 文本。</param>
    /// <returns>按出现顺序排列的 Markdown 标题信息。</returns>
    private static IReadOnlyList<MarkdownHeading> FindMarkdownHeadings(string markdown)
    {
        var headings = new List<MarkdownHeading>();
        var inFence = false;
        var lineStart = 0;

        while (lineStart <= markdown.Length)
        {
            int newlineIndex = markdown.IndexOf('\n', lineStart);
            int lineEnd = newlineIndex >= 0 ? newlineIndex : markdown.Length;
            string line = markdown[lineStart..lineEnd];

            if (MarkdownFenceRegex.IsMatch(line))
            {
                inFence = !inFence;
            }
            else if (!inFence)
            {
                Match headingMatch = MarkdownAtxHeadingRegex.Match(line);
                if (headingMatch.Success)
                {
                    string title = headingMatch.Groups["title"].Value.Trim();
                    if (title.Length > 0)
                    {
                        headings.Add(
                            new MarkdownHeading(
                                lineStart,
                                lineEnd + 1,
                                title,
                                headingMatch.Groups["marker"].Value.Length));
                    }
                }
            }

            if (newlineIndex < 0)
            {
                break;
            }

            lineStart = newlineIndex + 1;
        }

        return headings;
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
    /// 跳过章节标题后的单个换行符序列，避免正文以空行开头。
    /// </summary>
    /// <param name="content">完整的书籍文本。</param>
    /// <param name="index">章节标题匹配结束位置。</param>
    /// <returns>章节正文的起始字符索引。</returns>
    private static int SkipLineBreak(string content, int index)
    {
        if (index < content.Length && content[index] == '\r')
        {
            index++;
        }

        if (index < content.Length && content[index] == '\n')
        {
            index++;
        }

        return index;
    }
}

/// <summary>
/// 表示 Markdown 文本中的一个 ATX 标题位置。
/// </summary>
/// <param name="Start">标题所在行的起始字符索引。</param>
/// <param name="BodyStart">标题正文的起始字符索引。</param>
/// <param name="Title">标题文本。</param>
/// <param name="Level">Markdown ATX 标题级别，范围为 1 到 6。</param>
internal sealed record MarkdownHeading(int Start, int BodyStart, string Title, int Level);
