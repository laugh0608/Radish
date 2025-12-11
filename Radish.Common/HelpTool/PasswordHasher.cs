using Isopoh.Cryptography.Argon2;

namespace Radish.Common.HelpTool;

/// <summary>
/// 密码哈希工具类，使用 Argon2id 算法
/// </summary>
/// <remarks>
/// Argon2id 是 2015 年密码哈希竞赛的冠军算法，被 OWASP 推荐为首选密码哈希方案。
/// 相比 MD5/SHA-256，Argon2id 具有以下优势：
/// - 抗 GPU/ASIC 攻击（内存硬度设计）
/// - 抗侧信道攻击
/// - 可配置安全参数
/// - 内置盐值管理
///
/// 详细说明请参阅：radish.docs/docs/PasswordSecurity.md
/// </remarks>
public static class PasswordHasher
{
    /// <summary>
    /// 使用 Argon2id 对密码进行哈希
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>Argon2id 哈希字符串（包含盐值和参数，格式：$argon2id$v=19$m=19456,t=2,p=1$salt$hash）</returns>
    /// <exception cref="ArgumentException">密码为空或仅包含空白字符时抛出</exception>
    /// <remarks>
    /// 参数配置（基于 OWASP 2023 推荐）：
    /// - TimeCost: 2（迭代次数，约 0.5-1 秒）
    /// - MemoryCost: 19456 KB（约 19 MB）
    /// - Lanes: 1（并行度）
    /// - HashLength: 32 字节（256 位）
    ///
    /// 性能说明：
    /// - 单次哈希耗时约 0.5-1 秒（取决于服务器性能）
    /// - 这个延迟是有意设计的，用于防止暴力破解攻击
    /// - 对于正常登录流程，这个延迟是可接受的
    /// </remarks>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("密码不能为空或仅包含空白字符", nameof(password));
        }

        // 生成 16 字节（128 位）的随机盐值
        var salt = new byte[16];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing,  // Argon2id（混合模式，兼顾安全性和性能）
            Version = Argon2Version.Nineteen,    // 最新版本（v1.3）
            TimeCost = 2,                        // 迭代次数（2-3 次为推荐值）
            MemoryCost = 19456,                  // 内存使用量（19 MB，OWASP 推荐）
            Lanes = 1,                           // 并行度（1 线程，避免并发问题）
            Threads = 1,                         // 线程数
            Password = System.Text.Encoding.UTF8.GetBytes(password),
            Salt = salt,                         // 随机盐值（必须设置）
            HashLength = 32                      // 哈希输出长度（256 位）
        };

        using var argon2 = new Argon2(config);
        using var hash = argon2.Hash();
        return config.EncodeString(hash.Buffer);
    }

    /// <summary>
    /// 验证密码是否匹配存储的哈希值
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <param name="hash">存储的 Argon2id 哈希字符串</param>
    /// <returns>密码匹配返回 true，否则返回 false</returns>
    /// <remarks>
    /// 此方法会自动解析哈希字符串中的参数（盐值、迭代次数等），
    /// 然后使用相同参数对输入密码进行哈希并比对。
    ///
    /// 性能说明：
    /// - 验证耗时与哈希生成相同（约 0.5-1 秒）
    /// - 这是必要的安全成本，无法绕过
    /// </remarks>
    public static bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        try
        {
            return Argon2.Verify(hash, password);
        }
        catch
        {
            // 哈希格式错误或其他异常，返回 false
            return false;
        }
    }
}
