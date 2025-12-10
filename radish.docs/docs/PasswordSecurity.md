# 密码安全策略

本文档详细说明 Radish 项目的密码存储与验证策略，包括加密算法选择、实现细节和最佳实践。

## 1. 核心原则

### 1.1 前端无秘密

**重要**：在 Web 应用中，前端代码完全暴露给用户，任何前端加密都可以被逆向。因此：

- ❌ **不使用前端 RSA 加密**：旧框架中的 RSA 前端加密方案已废弃
- ✅ **依赖 HTTPS 传输加密**：所有敏感数据（包括密码）通过 HTTPS 传输，由 TLS 层提供加密保护
- ✅ **后端强加密存储**：密码在后端使用现代密码哈希算法（Argon2id）进行单向加密存储

### 1.2 为什么不使用 RSA 前端加密？

1. **安全假象**：前端 JavaScript 代码可被任何人查看和修改，攻击者可以直接绕过加密逻辑
2. **重复工作**：HTTPS 已经提供了传输层加密，前端加密是多余的
3. **增加复杂度**：需要管理密钥对、处理密钥轮换、增加前后端协调成本
4. **性能开销**：RSA 加密/解密计算量大，影响用户体验

**正确的安全模型**：
```
用户浏览器 --[HTTPS 加密]--> 后端服务器 --[Argon2id 哈希]--> 数据库
```

## 2. 密码哈希算法：Argon2id

### 2.1 为什么选择 Argon2id？

Argon2id 是 2015 年密码哈希竞赛（Password Hashing Competition）的冠军算法，被 OWASP 推荐为首选密码哈希方案。

**优势**：
- **抗 GPU/ASIC 攻击**：通过内存硬度（memory-hard）设计，使暴力破解成本极高
- **抗侧信道攻击**：Argon2id 结合了 Argon2d（数据依赖）和 Argon2i（数据独立）的优点
- **可配置安全参数**：可根据硬件性能调整时间成本、内存成本和并行度
- **内置盐值管理**：自动生成和存储盐值，无需手动管理
- **行业标准**：被 OWASP、NIST 等权威机构推荐

**与旧算法对比**：

| 算法 | 安全性 | 抗暴力破解 | 推荐使用 |
|------|--------|-----------|---------|
| MD5 | ❌ 极低 | ❌ 无抵抗力 | ❌ 已废弃 |
| SHA-256 | ⚠️ 低 | ⚠️ 易被 GPU 破解 | ❌ 不推荐 |
| bcrypt | ✅ 中 | ✅ 较好 | ⚠️ 可用但不是最佳 |
| Argon2id | ✅ 高 | ✅ 最强 | ✅ **首选** |

### 2.2 Argon2id 参数配置

Radish 使用以下参数（基于 OWASP 2023 推荐）：

```csharp
// 推荐配置（适用于大多数 Web 应用）
var config = new Argon2Config
{
    Type = Argon2Type.HybridAddressing,  // Argon2id
    Version = Argon2Version.Nineteen,    // 最新版本
    TimeCost = 2,                        // 迭代次数（2-3 次）
    MemoryCost = 19456,                  // 内存使用量（19 MB）
    Lanes = 1,                           // 并行度（1 线程）
    Threads = 1,                         // 线程数
    HashLength = 32                      // 哈希输出长度（256 位）
};
```

**参数说明**：
- **TimeCost**：迭代次数，增加计算时间。推荐 2-3 次（约 0.5-1 秒）
- **MemoryCost**：内存使用量（KB），增加内存成本。推荐 19-64 MB
- **Lanes/Threads**：并行度，通常设为 1（避免并发问题）
- **HashLength**：输出哈希长度，32 字节（256 位）足够安全

**性能调优**：
- 开发环境：可降低参数以加快测试速度
- 生产环境：根据服务器性能调整，目标是单次哈希耗时 0.5-1 秒
- 负载均衡：如果登录请求量大，可适当降低参数或使用缓存策略

## 3. 实现细节

### 3.1 密码哈希工具类

位置：`Radish.Common/HelpTool/PasswordHasher.cs`

```csharp
using Isopoh.Cryptography.Argon2;

public static class PasswordHasher
{
    /// <summary>
    /// 使用 Argon2id 对密码进行哈希
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>Argon2id 哈希字符串（包含盐值和参数）</returns>
    public static string HashPassword(string password)
    {
        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing,
            Version = Argon2Version.Nineteen,
            TimeCost = 2,
            MemoryCost = 19456,
            Lanes = 1,
            Threads = 1,
            HashLength = 32
        };

        var argon2 = new Argon2(config);
        using (argon2)
        {
            return argon2.Hash(password);
        }
    }

    /// <summary>
    /// 验证密码是否匹配
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <param name="hash">存储的哈希值</param>
    /// <returns>是否匹配</returns>
    public static bool VerifyPassword(string password, string hash)
    {
        return Argon2.Verify(hash, password);
    }
}
```

**哈希输出格式**：
```
$argon2id$v=19$m=19456,t=2,p=1$saltBase64$hashBase64
```

这个字符串包含：
- 算法类型（argon2id）
- 版本号（v=19）
- 参数（内存、时间、并行度）
- 盐值（Base64 编码）
- 哈希值（Base64 编码）

### 3.2 数据库存储

**User 表字段**：
- `LoginPassword`：存储 Argon2id 哈希字符串（约 100 字符）
- 确保字段长度至少 `VARCHAR(200)` 以容纳完整哈希

**种子数据示例**：
```csharp
// InitialDataSeeder.cs
var systemUser = new User(new UserInitializationOptions(
    "system",
    PasswordHasher.HashPassword("system123456")  // 使用 Argon2id
));
```

### 3.3 登录验证流程

```csharp
// LoginController.cs / AccountController.cs
public async Task<IActionResult> Login(string username, string password)
{
    // 1. 查询用户（不再需要对密码进行哈希）
    var user = await _userService.QueryAsync(u =>
        u.LoginName == username &&
        u.IsDeleted == false);

    if (user.Count == 0)
    {
        return Unauthorized("用户名或密码错误");
    }

    var firstUser = user.FirstOrDefault();

    // 2. 验证密码（使用 Argon2.Verify）
    if (!PasswordHasher.VerifyPassword(password, firstUser.LoginPassword))
    {
        return Unauthorized("用户名或密码错误");
    }

    // 3. 生成 Token...
}
```

**关键变化**：
- ❌ 旧方式：`WHERE LoginPassword = MD5(password)` - 在数据库层比对
- ✅ 新方式：先查询用户，再在应用层使用 `Argon2.Verify` 验证

**为什么不在数据库层比对？**
- Argon2 哈希每次生成的盐值不同，无法直接用 `WHERE` 比对
- 必须使用 `Argon2.Verify` 函数，它会解析哈希字符串并验证

## 4. 迁移指南

### 4.1 从 MD5 迁移到 Argon2id

**开发阶段（当前）**：
1. ✅ 直接替换所有 MD5 代码为 Argon2id
2. ✅ 删除旧数据库，重新运行 DbMigrate 生成新种子数据
3. ✅ 更新所有登录验证逻辑

**生产环境（未来）**：
如果已有生产数据，需要渐进式迁移：

```csharp
public bool VerifyPasswordWithMigration(string password, string storedHash)
{
    // 检测是否为旧 MD5 哈希（32 字符十六进制）
    if (storedHash.Length == 32 && IsHexString(storedHash))
    {
        // 验证 MD5
        var md5Hash = Md5Helper.Md5Encrypt32(password);
        if (md5Hash == storedHash)
        {
            // 验证成功，立即升级为 Argon2id
            var newHash = PasswordHasher.HashPassword(password);
            UpdateUserPassword(userId, newHash);
            return true;
        }
        return false;
    }

    // 新 Argon2id 哈希
    return PasswordHasher.VerifyPassword(password, storedHash);
}
```

### 4.2 清理清单

- [x] 删除 `Radish.Common/HelpTool/Md5Helper.cs`
- [x] 移除 `LoginController.cs` 中的 RSA 加密注释（第 50 行）
- [x] 移除 `LoginController.cs` 中的 MD5 加密注释（第 55 行）
- [x] 更新 `InitialDataSeeder.cs` 中的密码哈希方式
- [x] 更新 `LoginController.cs` 和 `AccountController.cs` 的验证逻辑
- [x] 更新 `AuthenticationGuide.md` 中的密码安全说明
- [x] 更新 `CLAUDE.md` 中的前后端通信说明

## 5. 安全最佳实践

### 5.1 密码策略

**推荐的密码要求**：
- 最小长度：8 字符（推荐 12+）
- 复杂度：包含大小写字母、数字、特殊字符
- 禁止常见密码：使用密码黑名单（如 "123456", "password"）
- 密码历史：禁止重复使用最近 5 次密码

**实现示例**：
```csharp
public class PasswordValidator
{
    public static bool IsStrongPassword(string password)
    {
        if (password.Length < 8) return false;
        if (!password.Any(char.IsUpper)) return false;
        if (!password.Any(char.IsLower)) return false;
        if (!password.Any(char.IsDigit)) return false;
        if (CommonPasswords.Contains(password)) return false;
        return true;
    }
}
```

### 5.2 登录保护

**防暴力破解**：
- 登录失败限制：5 次失败后锁定账户 15 分钟
- IP 限流：同一 IP 每分钟最多 10 次登录尝试
- CAPTCHA：3 次失败后要求验证码

**实现建议**：
```csharp
// 使用 Redis 记录失败次数
var failKey = $"login:fail:{username}";
var failCount = await _cache.GetAsync<int>(failKey);

if (failCount >= 5)
{
    return Unauthorized("账户已锁定，请 15 分钟后重试");
}

// 验证失败时增加计数
await _cache.SetAsync(failKey, failCount + 1, TimeSpan.FromMinutes(15));
```

### 5.3 密码重置

**安全流程**：
1. 用户请求重置 → 发送邮件/短信验证码
2. 验证码有效期：15 分钟
3. 验证成功 → 允许设置新密码
4. 新密码必须通过强度验证
5. 重置成功 → 使所有现有 Token 失效

**禁止的做法**：
- ❌ 通过邮件直接发送新密码
- ❌ 使用安全问题（容易被社工）
- ❌ 允许通过用户名直接重置（需验证身份）

### 5.4 会话管理

**Token 安全**：
- JWT Token 有效期：12 小时（可配置）
- Refresh Token：7 天，存储在 HttpOnly Cookie
- 密码修改后立即撤销所有 Token
- 支持用户主动登出所有设备

## 6. 性能考虑

### 6.1 登录性能

Argon2id 的计算成本是有意设计的（防暴力破解），但需要平衡用户体验：

**基准测试**（参考值）：
- TimeCost=2, MemoryCost=19456：约 0.5-1 秒/次
- 对于正常登录流程，这个延迟是可接受的
- 如果服务器性能较差，可适当降低参数

**优化策略**：
```csharp
// 1. 异步处理
await Task.Run(() => PasswordHasher.VerifyPassword(password, hash));

// 2. 登录成功后缓存会话（避免重复验证）
await _cache.SetAsync($"session:{userId}", userInfo, TimeSpan.FromHours(12));

// 3. 使用 Redis 限流（避免恶意请求消耗资源）
```

### 6.2 数据库查询优化

```csharp
// ✅ 推荐：先按用户名查询（有索引），再验证密码
var user = await _db.Queryable<User>()
    .Where(u => u.LoginName == username && !u.IsDeleted)
    .FirstAsync();

if (user != null && PasswordHasher.VerifyPassword(password, user.LoginPassword))
{
    // 登录成功
}

// ❌ 避免：全表扫描后再验证
var allUsers = await _db.Queryable<User>().ToListAsync();
var user = allUsers.FirstOrDefault(u =>
    PasswordHasher.VerifyPassword(password, u.LoginPassword));
```

**索引建议**：
```sql
CREATE INDEX idx_user_loginname ON User(LoginName) WHERE IsDeleted = false;
```

## 7. 常见问题

### Q1: 为什么不继续使用 MD5？
**A**: MD5 已被证明不安全，可在秒级时间内被彩虹表破解。即使加盐，GPU 暴力破解速度仍然极快（每秒数十亿次）。

### Q2: Argon2id 会不会太慢影响用户体验？
**A**: 0.5-1 秒的延迟对登录操作是可接受的，且只在登录时发生一次。这个成本是为了防止攻击者暴力破解，是必要的安全投资。

### Q3: 如果忘记密码怎么办？
**A**: 使用密码重置流程（邮件/短信验证码）。管理员也无法"查看"用户密码，因为存储的是单向哈希。

### Q4: 可以降低 Argon2id 参数以提高性能吗？
**A**: 可以，但需要权衡安全性。最低建议：TimeCost=2, MemoryCost=15360（15 MB）。不要低于 OWASP 最低推荐值。

### Q5: 前端需要做什么改动？
**A**: 无需改动。前端直接通过 HTTPS POST 发送明文密码即可，后端负责所有加密逻辑。

## 8. 参考资料

- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- [Argon2 RFC 9106](https://datatracker.ietf.org/doc/html/rfc9106)
- [Password Hashing Competition](https://www.password-hashing.net/)
- [Isopoh.Cryptography.Argon2 (NuGet)](https://www.nuget.org/packages/Isopoh.Cryptography.Argon2/)

## 9. 更新日志

| 日期 | 版本 | 变更内容 |
|------|------|---------|
| 2025-12-10 | 1.0 | 初始版本：从 MD5 迁移到 Argon2id，移除 RSA 前端加密 |

---

**相关文档**：
- [AuthenticationGuide.md](AuthenticationGuide.md) - 完整的认证架构说明
- [ConfigurationGuide.md](ConfigurationGuide.md) - 配置管理指南
- [DevelopmentSpecifications.md](DevelopmentSpecifications.md) - 开发规范
