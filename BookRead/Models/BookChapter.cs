namespace BookRead.Models;

/// <summary>
/// 表示从书籍中识别出的一个章节。
/// </summary>
/// <param name="Title">章节标题。</param>
/// <param name="Content">不包含章节标题行的正文内容。</param>
/// <param name="MarkdownHeadingLevel">Markdown 标题级别；非 Markdown 章节或无法获取层级时为 0。</param>
internal sealed record BookChapter(
    string Title,
    string Content,
    int MarkdownHeadingLevel = 0);
