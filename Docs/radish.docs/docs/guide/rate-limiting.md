# 速率限制指南

本文档介绍 Radish 项目中的速率限制（Rate Limiting）功能，包括配置、使用方式和最佳实践。

## 概述

速率限制是一种保护 API 免受滥用和 DDoS 攻击的重要安全机制。Radish 使用 ASP.NET Core 内置的 Rate Limiting 功能，提供了灵活且高性能的限流方案。

### 核心特性

- ✅ **4 种限流算法**：Fixed Window、Sliding Window、Token Bucket、Concurrency Limiter
- ✅ **IP 黑名单/白名单**：支持 CIDR 格式，灵活控制访问
- ✅ **限流日志**：记录触发限流的 IP 和端点，便于审计
- ✅ **智能 IP 识别**：自动识别反向代理场景（X-Forwarded-For、X-Real-IP）
- ✅ **零第三方依赖**：使用 .NET 内置功能，性能最优
- ✅ **完全可配置**：所有参数可通过 `appsettings.json` 配置

## 限流策略

### 1. 全局限流（Global）

**算法**：Fixed Window（固定窗口）

**用途**：适用于所有 API 端点的基础限流

**默认配置**：
- 每个 IP 每分钟最多 200 个请求
- 时间窗口：60 秒

**特点**：
- 简单高效，适合大多数场景
- 在时间窗口结束时重置计数器

### 2. 登录限流（Login）

**算法**：Sliding Window（滑动窗口）

**用途**：防止暴力破解登录

**默认配置**：
- 每个 IP 15 分钟最多 10 次登录尝试
- 时间窗口：900 秒（15 分钟）
- 滑动窗口分段数：8

**特点**：
- 比固定窗口更平滑，避免突发流量
- 适合需要严格控制的场景

**应用位置**：
- `Radish.Auth/Controllers/AccountController.cs` 的 `Login` 方法
- `/connect/token` 端点（OIDC Token 端点）

### 3. 敏感操作限流（Sensitive）

**算法**：Token Bucket（令牌桶）

**用途**：适用于删除、权限变更等敏感操作

**默认配置**：
- 令牌桶容量：20
- 每 60 秒补充 20 个令牌
- 队列限制：0（不排队，直接拒绝）

**特点**：
- 允许短时间内的突发流量
- 长期平均速率受限
- 适合需要灵活控制的场景

**推荐应用**：
- 删除操作（DELETE 请求）
- 权限变更（角色分配、权限修改）
- 客户端管理（创建/删除 OIDC 客户端）

### 4. 并发限流（Concurrency）

**算法**：Concurrency Limiter（并发限制器）

**用途**：限制同时处理的请求数

**默认配置**：
- 每个 IP 最多 100 个并发请求
- 队列限制：最多排队 50 个请求

**特点**：
- 控制服务器资源占用
- 防止单个 IP 占用过多连接
- 适合资源密集型操作

**推荐应用**：
- 文件上传/下载
- 数据导出
- 复杂查询

## 配置说明

### 基础配置

在 `appsettings.json` 中添加 `RateLimit` 配置节：

```json
{
  "RateLimit": {
    // 是否启用速率限制
    "Enable": true,

    // 是否记录限流日志
    "EnableLogging": true,

    // 全局限流配置
    "Global": {
      "Enable": true,
      "PermitLimit": 200,      // 允许的请求数
      "WindowSeconds": 60      // 时间窗口（秒）
    },

    // 登录限流配置
    "Login": {
      "Enable": true,
      "PermitLimit": 10,       // 允许的请求数
      "WindowSeconds": 900,    // 时间窗口（秒）
      "SegmentsPerWindow": 8   // 滑动窗口分段数
    },

    // 敏感操作限流配置
    "Sensitive": {
      "Enable": true,
      "TokenLimit": 20,                    // 令牌桶容量
      "TokensPerPeriod": 20,               // 每个周期补充的令牌数
      "ReplenishmentPeriodSeconds": 60,    // 补充周期（秒）
      "QueueLimit": 0                      // 队列限制（0 表示不排队）
    },

    // 并发限流配置
    "Concurrency": {
      "Enable": true,
      "PermitLimit": 100,      // 允许的并发请求数
      "QueueLimit": 50         // 队列限制
    }
  }
}
```

### IP 黑名单配置

黑名单用于封禁恶意 IP，支持手动配置和自动封禁。

```json
{
  "RateLimit": {
    "Blacklist": {
      // 是否启用黑名单
      "Enable": false,

      // 黑名单 IP 列表（支持 CIDR 格式）
      "IpAddresses": [
        "192.168.1.100",        // 单个 IP
        "10.0.0.0/8",           // CIDR 网段
        "172.16.0.0/12"
      ],

      // 触发限流多少次后自动加入黑名单（0 表示不自动加入）
      "AutoBlockAfterRejections": 0,

      // 自动封禁时长（秒，0 表示永久）
      "AutoBlockDurationSeconds": 3600
    }
  }
}
```

**使用场景**：
- 封禁已知的恶意 IP
- 封禁爬虫或扫描器
- 临时封禁触发限流过多的 IP

**注意事项**：
- 黑名单默认禁用，需要在 `appsettings.Local.json` 中启用
- 黑名单 IP 会收到 403 Forbidden 响应
- 支持 IPv4 和 IPv6
- CIDR 格式示例：`192.168.1.0/24` 表示 `192.168.1.0` 到 `192.168.1.255`

### IP 白名单配置

白名单中的 IP 不受限流限制，适用于内网 IP、管理员 IP 等。

```json
{
  "RateLimit": {
    "Whitelist": {
      // 是否启用白名单
      "Enable": false,

      // 白名单 IP 列表（支持 CIDR 格式）
      "IpAddresses": [
        "127.0.0.1",            // 本地回环
        "::1",                  // IPv6 本地回环
        "10.0.0.0/8",           // 内网 A 类地址
        "172.16.0.0/12",        // 内网 B 类地址
        "192.168.0.0/16"        // 内网 C 类地址
      ]
    }
  }
}
```

**使用场景**：
- 内网 IP 不受限制
- 管理员 IP 不受限制
- 监控系统 IP 不受限制
- CI/CD 系统 IP 不受限制

**注意事项**：
- 白名单优先级高于黑名单
- 白名单默认禁用，需要在 `appsettings.Local.json` 中启用
- 谨慎配置白名单，避免安全风险

## 使用方式

### 在控制器上应用限流

```csharp
using Microsoft.AspNetCore.RateLimiting;

// 在整个控制器上应用限流
[EnableRateLimiting("sensitive")]
public class ClientController : ControllerBase
{
    // 所有端点都应用敏感操作限流
}
```

### 在端点上应用限流

```csharp
// 在特定端点上应用限流
[HttpPost("Login")]
[EnableRateLimiting("login")]
public async Task<IActionResult> Login(string username, string password)
{
    // 登录逻辑
}

[HttpDelete("{id}")]
[EnableRateLimiting("sensitive")]
public async Task<IActionResult> Delete(long id)
{
    // 删除逻辑
}

[HttpPost("Upload")]
[EnableRateLimiting("concurrency")]
public async Task<IActionResult> Upload(IFormFile file)
{
    // 文件上传逻辑
}
```

### 禁用限流

某些端点（如健康检查）不需要限流：

```csharp
[HttpGet("Health")]
[DisableRateLimiting]
public IActionResult Health()
{
    return Ok("Healthy");
}
```

## 响应格式

### 429 Too Many Requests

当触发限流时，返回 429 状态码：

```json
{
  "status": 429,
  "message": "请求过于频繁，请稍后再试",
  "success": false,
  "retryAfter": 60
}
```

**响应头**：
- `Retry-After`: 60（秒）

### 403 Forbidden（黑名单）

当 IP 在黑名单中时，返回 403 状态码：

```json
{
  "status": 403,
  "message": "您的 IP 地址已被封禁",
  "success": false
}
```

## 限流日志

当启用限流日志时（`EnableLogging: true`），触发限流会记录以下信息：

```
[Warning] [RateLimit] 请求被限流 - IP: 192.168.1.100, 端点: POST /Account/Login, Lease: Rejected
```

日志包含：
- IP 地址
- HTTP 方法和端点
- 限流结果（Rejected）

日志使用 Serilog 记录，输出到 `Log/{ProjectName}/Log.txt`。

## 最佳实践

### 1. 根据业务调整参数

默认配置适合大多数场景，但您可以根据实际业务调整：

- **高流量 API**：增加 `PermitLimit`
- **敏感操作**：减少 `PermitLimit` 和 `WindowSeconds`
- **公开 API**：启用全局限流
- **内部 API**：可以禁用限流或使用白名单

### 2. 使用白名单保护内网

如果您的 API 同时对外网和内网提供服务，建议启用白名单：

```json
{
  "RateLimit": {
    "Whitelist": {
      "Enable": true,
      "IpAddresses": ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"]
    }
  }
}
```

### 3. 监控限流日志

定期检查限流日志，识别异常流量：

```bash
# 查看限流日志
grep "RateLimit" Log/Radish.Api/Log*.txt

# 统计触发限流最多的 IP
grep "RateLimit" Log/Radish.Api/Log*.txt | grep -oP 'IP: \K[0-9.]+' | sort | uniq -c | sort -rn
```

### 4. 渐进式启用黑名单

不建议一开始就启用自动封禁，建议先观察：

1. 启用限流日志，观察 1-2 周
2. 识别恶意 IP，手动添加到黑名单
3. 确认无误后，启用自动封禁

### 5. 为不同端点使用不同策略

根据端点的重要性和敏感度选择合适的策略：

| 端点类型 | 推荐策略 | 原因 |
|---------|---------|------|
| 登录/注册 | `login` | 防止暴力破解 |
| 删除操作 | `sensitive` | 防止误操作和恶意删除 |
| 权限变更 | `sensitive` | 保护敏感操作 |
| 文件上传 | `concurrency` | 控制资源占用 |
| 查询接口 | `global` | 基础限流即可 |
| 健康检查 | 禁用 | 不需要限流 |

### 6. 反向代理场景

如果您使用 Nginx、Gateway 等反向代理，确保正确传递客户端 IP：

**Nginx 配置**：
```nginx
location / {
    proxy_pass http://backend;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Real-IP $remote_addr;
}
```

**Gateway 配置**：
```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

Radish 会自动从 `X-Forwarded-For` 和 `X-Real-IP` 头中提取客户端 IP。

## 性能考虑

### 内存使用

速率限制使用内存存储限流状态，内存占用取决于：
- 活跃 IP 数量
- 时间窗口大小
- 启用的策略数量

**估算**：
- 每个 IP 约占用 100-200 字节
- 1000 个活跃 IP 约占用 100-200 KB
- 对于大多数应用，内存占用可以忽略不计

### 性能影响

ASP.NET Core 内置 Rate Limiting 性能极高：
- 每个请求增加约 0.1-0.5 毫秒延迟
- 对吞吐量影响小于 1%
- 适合高并发场景

### 扩展到 Redis

如果您需要在多实例部署中共享限流状态，可以扩展到 Redis：

```csharp
// 未来可以实现 Redis 存储
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});
```

目前版本使用内存存储，适合单实例部署。

## 故障排查

### 限流不生效

**检查清单**：
1. 确认 `RateLimit.Enable` 为 `true`
2. 确认中间件已注册：`app.UseRateLimitSetup()`
3. 确认中间件位置正确（在授权之后，路由之前）
4. 检查端点是否应用了 `[EnableRateLimiting]` 特性
5. 检查日志中是否有 `[RateLimit]` 相关信息

### 误封正常用户

**可能原因**：
1. 限流参数设置过严
2. 前端重复请求
3. 自动化测试触发限流

**解决方案**：
1. 调整限流参数（增加 `PermitLimit`）
2. 将测试 IP 添加到白名单
3. 检查前端代码，避免重复请求

### IP 识别错误

**可能原因**：
1. 反向代理未正确传递 `X-Forwarded-For` 头
2. 多层代理导致 IP 链过长

**解决方案**：
1. 检查反向代理配置
2. 确认 `X-Forwarded-For` 头格式正确
3. 查看日志中记录的 IP 是否正确

## 相关文档

- [配置指南](./configuration.md) - 配置文件管理
- [认证指南](./authentication.md) - OIDC 认证流程
- [开发日志](/changelog/2025-12#2025-12-19) - 速率限制实现详情

## 总结

速率限制是保护 API 安全的重要手段。Radish 提供了灵活且高性能的限流方案，支持多种算法和高级特性。通过合理配置和使用，可以有效防止 API 滥用和 DDoS 攻击。

**关键要点**：
- ✅ 默认配置适合大多数场景
- ✅ 根据业务调整参数
- ✅ 使用白名单保护内网
- ✅ 监控限流日志
- ✅ 渐进式启用黑名单
- ✅ 为不同端点使用不同策略
