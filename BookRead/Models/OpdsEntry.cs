namespace BookRead.Models;

/// <summary>
/// 表示从 OPDS 目录解析出的单个条目；导航条目有 NavigationUrl，书籍条目有下载链接。
/// </summary>
/// <param name="BookId">服务端条目标识。</param>
/// <param name="Title">条目标题。</param>
/// <param name="Author">作者名称；可能为空。</param>
/// <param name="Summary">条目简介纯文本；可能为空。</param>
/// <param name="CoverThumbnailUrl">封面缩略图地址或 data URI；可能为空。</param>
/// <param name="Acquisitions">可下载链接列表。</param>
/// <param name="NavigationUrl">子目录链接；非导航条目为空。</param>
internal sealed record OpdsEntry(
    string BookId,
    string Title,
    string? Author,
    string? Summary,
    string? CoverThumbnailUrl,
    IReadOnlyList<OpdsLink> Acquisitions,
    string? NavigationUrl = null)
{
    /// <summary>获取条目是否为导航到子目录的条目。</summary>
    public bool IsNavigation => !string.IsNullOrWhiteSpace(NavigationUrl);
}
