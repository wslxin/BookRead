namespace BookRead.Models;

/// <summary>
/// 提供阅读位置变化后的章节和分页信息。
/// </summary>
/// <param name="chapterIndex">当前章节索引，从 0 开始。</param>
/// <param name="pageIndex">当前章节内分页索引，从 0 开始。</param>
internal sealed class ReaderProgressChangedEventArgs(int chapterIndex, int pageIndex) : EventArgs
{
    /// <summary>获取当前章节索引。</summary>
    internal int ChapterIndex { get; } = chapterIndex;

    /// <summary>获取当前章节内分页索引。</summary>
    internal int PageIndex { get; } = pageIndex;
}
