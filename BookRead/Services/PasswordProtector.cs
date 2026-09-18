using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace BookRead.Services;

/// <summary>
/// 使用 Windows DPAPI 以当前用户范围加密 OPDS 密码。
/// </summary>
internal static class PasswordProtector
{
    private static readonly byte[] Entropy = [0x42, 0x6F, 0x6F, 0x6B, 0x52, 0x65, 0x61, 0x64];

    /// <summary>
    /// 加密密码字符串。
    /// </summary>
    /// <param name="password">要加密的明文密码；可为空。</param>
    /// <returns>Base64 编码的密文；输入为空时返回空字符串。</returns>
    /// <exception cref="CryptographicException">Windows 数据保护接口加密失败时抛出。</exception>
    internal static string Protect(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return string.Empty;
        }

        byte[] protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(password),
            Entropy,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    /// <summary>
    /// 解密 Base64 编码的 DPAPI 密文。
    /// </summary>
    /// <param name="encryptedPassword">要解密的 Base64 密文；可为空。</param>
    /// <returns>明文密码；输入为空时返回空字符串。</returns>
    /// <exception cref="FormatException">密文不是有效 Base64 字符串时抛出。</exception>
    /// <exception cref="CryptographicException">密文损坏或当前用户无法解密时抛出。</exception>
    internal static string Unprotect(string? encryptedPassword)
    {
        if (string.IsNullOrEmpty(encryptedPassword))
        {
            return string.Empty;
        }

        byte[] encryptedBytes = Convert.FromBase64String(encryptedPassword);
        byte[] passwordBytes = ProtectedData.Unprotect(
            encryptedBytes,
            Entropy,
            DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(passwordBytes);
    }
}
