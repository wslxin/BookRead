namespace BookRead.Models;

/// <summary>
/// 表示 OPDS 条目中的一个导航或下载链接。
/// </summary>
/// <param name="Href">绝对或相对于当前 Feed 的链接地址。</param>
/// <param name="Relation">Atom link 的 rel 属性。</param>
/// <param name="MediaType">Atom link 的 type 属性；可能为空。</param>
/// <param name="Title">链接显示标题；可能为空。</param>
/// <param name="Length">服务端声明的文件字节数；未知时为 null。</param>
internal sealed record OpdsLink(
    string Href,
    string? Relation,
    string? MediaType,
    string? Title = null,
    long? Length = null);
