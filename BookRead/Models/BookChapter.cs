namespace BookRead.Models;

/// <summary>
/// 表示从文本书籍中识别出的一个章节。
/// </summary>
/// <param name="Title">章节标题。</param>
/// <param name="Content">不包含章节标题行的正文内容。</param>
internal sealed record BookChapter(string Title, string Content);
