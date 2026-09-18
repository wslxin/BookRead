namespace BookRead.Models;

/// <summary>
/// 表示用户配置的一个 OPDS 1.x 书源。
/// </summary>
internal sealed class OpdsSource
{
    /// <summary>书源唯一标识。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>书源显示名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>OPDS 根目录地址。</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>HTTP Basic 认证用户名；公开书源为空。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>使用当前 Windows 用户 DPAPI 加密后的密码；公开书源为空。</summary>
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>是否忽略 HTTPS 证书校验错误；仅用于自建服务器。</summary>
    public bool IgnoreCertificateErrors { get; set; }

    /// <summary>书源添加时间。</summary>
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
}
