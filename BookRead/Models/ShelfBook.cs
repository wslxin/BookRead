namespace BookRead.Models;

/// <summary>
/// 表示持久化在本地书架中的一本书。
/// </summary>
/// <param name="FilePath">原始书籍文件的绝对路径。</param>
/// <param name="Title">书籍显示名称。</param>
/// <param name="ImportedAt">首次导入时间。</param>
/// <param name="LastOpenedAt">最近打开时间。</param>
/// <param name="ChapterIndex">最近阅读的章节索引，从 0 开始。</param>
/// <param name="PageIndex">最近阅读的章节内分页索引，从 0 开始。</param>
internal sealed record ShelfBook(
    string FilePath,
    string Title,
    DateTimeOffset ImportedAt,
    DateTimeOffset LastOpenedAt,
    int ChapterIndex = 0,
    int PageIndex = 0);
