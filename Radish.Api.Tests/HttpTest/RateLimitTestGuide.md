# 速率限制中间件测试指南

本目录包含 Radish 速率限制中间件的完整测试套件，用于验证 ASP.NET Core Rate Limiting 的 4 种限流算法。

## 📁 文件说明

- **Radish.Api.RateLimit.http** - HTTP 测试文件，包含所有测试用例和详细说明
- **test-rate-limit.ps1** - PowerShell 自动化测试脚本（Windows/Linux/macOS）
- **test-rate-limit.sh** - Bash 自动化测试脚本（Linux/macOS）
- **README-RateLimit.md** - 本文档

## 🎯 测试目标

验证以下速率限制功能：

1. **全局限流**（Fixed Window 算法）- 每个 IP 每分钟 200 个请求
2. **登录限流**（Sliding Window 算法）- 每个 IP 15 分钟 10 次登录尝试
3. **敏感操作限流**（Token Bucket 算法）- 令牌桶容量 20，每 60 秒补充
4. **并发限流**（Concurrency Limiter）- 每个 IP 最多 100 个并发请求
5. **黑名单功能** - IP 黑名单与自动封禁
6. **白名单功能** - 白名单 IP 不受限流限制
7. **限流日志** - 验证限流事件是否正确记录

## 🚀 快速开始

### 前置条件

1. 确保 Radish.Api 和 Radish.Auth 服务正在运行：
   ```bash
   # 启动 API 服务
   dotnet run --project Radish.Api/Radish.Api.csproj

   # 启动 Auth 服务
   dotnet run --project Radish.Auth/Radish.Auth.csproj
   ```

2. 确保 `appsettings.json` 中速率限制已启用：
   ```json
   {
     "RateLimit": {
       "Enable": true,
       "EnableLogging": true
     }
   }
   ```

### 方法 1：使用自动化测试脚本（推荐）

#### Windows (PowerShell)

```powershell
# 运行完整测试
./test-rate-limit.ps1

# 显示详细输出
./test-rate-limit.ps1 -Verbose

# 跳过某些测试
./test-rate-limit.ps1 -SkipGlobal -SkipLogin

# 自定义服务地址
./test-rate-limit.ps1 -ApiUrl "http://localhost:5100" -AuthUrl "http://localhost:5200"
```

#### Linux/macOS (Bash)

```bash
# 运行完整测试
./test-rate-limit.sh

# 显示详细输出
./test-rate-limit.sh --verbose

# 跳过某些测试
./test-rate-limit.sh --skip-global --skip-login

# 自定义服务地址
./test-rate-limit.sh --api-url http://localhost:5100 --auth-url http://localhost:5200
```

### 方法 2：使用 HTTP 文件手动测试

1. 安装 VS Code 的 REST Client 扩展
2. 打开 `Radish.Api.RateLimit.http`
3. 点击测试用例上方的 "Send Request" 按钮
4. 查看响应结果

### 方法 3：使用命令行工具

#### 测试全局限流

```bash
# PowerShell
for ($i = 1; $i -le 201; $i++) {
    $response = Invoke-WebRequest -Uri "http://localhost:5100/health" -Method GET
    Write-Host "Request $i : $($response.StatusCode)"
}

# Bash
for i in {1..201}; do
    echo "Request $i"
    curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5100/health
done
```

#### 测试登录限流

```bash
# PowerShell
$body = @{ username = "testuser"; password = "wrongpassword" }
for ($i = 1; $i -le 11; $i++) {
    Invoke-WebRequest -Uri "http://localhost:5200/Account/Login" -Method POST -Body $body
}

# Bash
for i in {1..11}; do
    curl -X POST http://localhost:5200/Account/Login \
        -d "username=testuser&password=wrongpassword"
done
```

## 📊 预期结果

### 1. 全局限流测试

- **预期**：第 201 个请求返回 `429 Too Many Requests`
- **响应格式**：
  ```json
  {
    "status": 429,
    "message": "请求过于频繁，请稍后再试",
    "success": false,
    "retryAfter": 60
  }
  ```
- **响应头**：`Retry-After: 60`

### 2. 登录限流测试

- **预期**：第 11 次登录尝试返回 `429 Too Many Requests`
- **时间窗口**：15 分钟（900 秒）
- **算法**：Sliding Window（滑动窗口，8 个分段）

### 3. 敏感操作限流测试

- **预期**：快速消耗 20 个令牌后返回 `429`
- **令牌补充**：每 60 秒补充 20 个令牌
- **算法**：Token Bucket（令牌桶）

### 4. 并发限流测试

- **预期**：50 个并发请求全部成功
- **预期**：超过 150 个并发请求时部分返回 `429`
- **配置**：最多 100 个并发，队列限制 50

### 5. 黑名单测试

- **预期**：黑名单 IP 返回 `403 Forbidden`
- **响应格式**：
  ```json
  {
    "status": 403,
    "message": "您的 IP 地址已被封禁",
    "success": false
  }
  ```

### 6. 白名单测试

- **预期**：白名单 IP 不受限流限制
- **验证**：快速发送 300 个请求，全部成功

## 🔧 配置说明

### 启用/禁用限流

在 `appsettings.json` 或 `appsettings.Local.json` 中：

```json
{
  "RateLimit": {
    "Enable": true,  // 总开关
    "EnableLogging": true,  // 限流日志
    "Global": {
      "Enable": true  // 全局限流开关
    },
    "Login": {
      "Enable": true  // 登录限流开关
    }
  }
}
```

### 配置黑名单

```json
{
  "RateLimit": {
    "Blacklist": {
      "Enable": true,
      "IpAddresses": ["192.168.1.100", "10.0.0.0/8"],
      "AutoBlockAfterRejections": 5,  // 触发 5 次限流后自动封禁
      "AutoBlockDurationSeconds": 3600  // 封禁 1 小时
    }
  }
}
```

### 配置白名单

```json
{
  "RateLimit": {
    "Whitelist": {
      "Enable": true,
      "IpAddresses": ["127.0.0.1", "::1", "10.0.0.0/8"]
    }
  }
}
```

## 📝 日志验证

### 查看限流日志

限流事件会记录在以下位置：

- **API 日志**：`Log/Radish.Api/Log.txt`
- **Auth 日志**：`Log/Radish.Auth/Log.txt`

### 日志格式示例

```
[2025-12-20 10:30:45 INF] Rate limit triggered for IP 127.0.0.1 on endpoint /health (Policy: global)
[2025-12-20 10:31:12 INF] Rate limit triggered for IP 127.0.0.1 on endpoint /Account/Login (Policy: login)
```

## 🐛 故障排查

### 问题 1：限流未触发

**可能原因**：
1. `RateLimit.Enable = false` - 限流未启用
2. IP 在白名单中
3. 限流配置过高（如 `PermitLimit` 设置为 10000）
4. 端点添加了 `[DisableRateLimiting]` 特性

**解决方案**：
- 检查 `appsettings.json` 配置
- 检查白名单配置
- 查看日志确认限流中间件是否加载

### 问题 2：所有请求返回 403

**可能原因**：
- IP 在黑名单中
- 自动封禁功能触发

**解决方案**：
- 检查 `Blacklist.IpAddresses` 配置
- 重启服务清除内存中的黑名单
- 禁用自动封禁功能（`AutoBlockAfterRejections = 0`）

### 问题 3：登录限流不生效

**可能原因**：
- `AccountController.Login` 方法未添加 `[EnableRateLimiting("login")]` 特性
- CSRF 验证失败导致请求未到达限流中间件

**解决方案**：
- 检查控制器代码
- 使用正确的 CSRF Token
- 查看 Auth 服务日志

### 问题 4：并发测试失败

**可能原因**：
- 并发工具未正确配置
- 服务器性能限制
- 网络延迟导致请求未真正并发

**解决方案**：
- 使用专业并发测试工具（Apache Bench、wrk）
- 降低并发数量
- 在本地环境测试

## 📈 性能测试

### 使用 Apache Bench

```bash
# 安装 Apache Bench
# Ubuntu/Debian: sudo apt-get install apache2-utils
# macOS: brew install httpd

# 基准测试（无限流）
ab -n 10000 -c 100 http://localhost:5100/health

# 限流性能测试
ab -n 10000 -c 100 http://localhost:5100/health
```

### 使用 wrk

```bash
# 安装 wrk
# Ubuntu/Debian: sudo apt-get install wrk
# macOS: brew install wrk

# 性能测试
wrk -t4 -c100 -d30s http://localhost:5100/health
```

## 🔒 安全建议

1. **生产环境配置**：
   - 根据实际流量调整限流参数
   - 启用黑名单和自动封禁功能
   - 配置白名单（内网 IP、管理员 IP）
   - 启用限流日志并定期审计

2. **监控告警**：
   - 监控 429 响应数量
   - 设置告警阈值（如 429 响应超过 10%）
   - 监控黑名单大小

3. **反向代理场景**：
   - 确保正确配置 `X-Forwarded-For` 头
   - 验证真实 IP 识别逻辑
   - 考虑在 Gateway 层实施限流

## 📚 相关文档

- [ASP.NET Core Rate Limiting 官方文档](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [Radish 开发日志 - 2025.12.19](../../Docs/radish.docs/docs/changelog/2025-12.md)
- [Radish 配置指南](../../Docs/radish.docs/docs/guide/configuration.md)

## 🤝 贡献

如果发现测试用例不完整或有改进建议，请：

1. 在 `Radish.Api.RateLimit.http` 中添加新的测试用例
2. 更新自动化测试脚本
3. 更新本文档
4. 提交 Pull Request

## 📄 许可证

本测试套件遵循 Radish 项目的许可证。
