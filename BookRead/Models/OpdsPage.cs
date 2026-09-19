namespace BookRead.Models;

/// <summary>
/// 表示一次 OPDS 目录请求解析后的结果页。
/// </summary>
/// <param name="Title">目录标题。</param>
/// <param name="Entries">当前目录条目。</param>
/// <param name="PreviousPageUrl">上一页地址；没有上一页时为空。</param>
/// <param name="NextPageUrl">下一页地址；没有更多结果时为空。</param>
/// <param name="SearchTemplateUrl">OpenSearch 搜索模板；书源不支持搜索时为空。</param>
internal sealed record OpdsPage(
    string Title,
    IReadOnlyList<OpdsEntry> Entries,
    string? PreviousPageUrl,
    string? NextPageUrl,
    string? SearchTemplateUrl);
