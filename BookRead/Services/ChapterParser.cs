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
