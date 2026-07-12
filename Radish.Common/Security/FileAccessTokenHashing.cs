using System.Security.Cryptography;

namespace Radish.Common.Security;

/// <summary>
/// 文件访问令牌的生成、哈希与历史格式识别。
/// </summary>
public static class FileAccessTokenHashing
{
    public const int LegacyTokenLength = 32;
    public const int StoredHashLength = 43;

    public static string GenerateRawToken()
    {
        return EncodeBase64Url(RandomNumberGenerator.GetBytes(32));
    }

    public static string HashToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        return EncodeBase64Url(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
    }

    public static bool IsLegacyRawToken(string? value)
    {
        return value?.Length == LegacyTokenLength && value.All(Uri.IsHexDigit);
    }

    public static bool IsStoredHash(string? value)
    {
        if (value?.Length != StoredHashLength)
        {
            return false;
        }

        return value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }

    private static string EncodeBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
