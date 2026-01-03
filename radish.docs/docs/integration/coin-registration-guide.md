# 萝卜币系统集成指南 - 用户注册奖励

## 概述

本文档说明如何在用户注册流程中集成萝卜币初始奖励发放功能。

## 奖励规则

- **奖励金额**：50 胡萝卜（0.05 白萝卜）
- **交易类型**：`SYSTEM_GRANT`（系统赠送）
- **业务类型**：`UserRegistration`
- **备注**：`新用户注册奖励`

## 集成示例代码

### 方案 1：在 Auth 项目注册接口中集成（推荐）

如果在 `Radish.Auth` 项目的 `AccountController` 中实现注册功能：

```csharp
// Radish.Auth/Controllers/AccountController.cs

using Radish.IService;
using Radish.Model;
using Radish.Common.HelpTool;

[HttpPost]
[ValidateAntiForgeryToken]
[EnableRateLimiting("register")]
public async Task<IActionResult> Register([FromForm] RegisterViewModel model)
{
    // 1. 参数验证
    if (!ModelState.IsValid)
    {
        TempData["RegisterError"] = "注册信息不完整";
        return View(model);
    }

    // 2. 检查用户名是否已存在
    var existingUsers = await _userService.QueryAsync(u => u.LoginName == model.Username);
    if (existingUsers.Any())
    {
        TempData["RegisterError"] = "用户名已存在";
        return View(model);
    }

    // 3. 创建用户（使用 Argon2id 哈希密码）
    var hashedPassword = PasswordHasher.HashPassword(model.Password);
    var newUser = new User
    {
        LoginName = model.Username,
        LoginPwd = hashedPassword,
        UserEmail = model.Email,
        TenantId = 0,  // 默认租户
        IsEnable = true,
        IsDeleted = false,
        CreateTime = DateTime.Now,
        CreateBy = "System",
        CreateId = 0
    };

    var userId = await _userService.AddAsync(newUser);

    // 4. 发放注册奖励（50 胡萝卜）
    try
    {
        // 注入 ICoinService（需要在 Auth 项目的 Program.cs 中注册）
        var coinService = HttpContext.RequestServices.GetRequiredService<ICoinService>();

        await coinService.GrantCoinAsync(
            userId: userId,
            amount: 50000,  // 50 白萝卜 = 50000 胡萝卜
            transactionType: "SYSTEM_GRANT",
            businessType: "UserRegistration",
            businessId: userId,
            remark: "新用户注册奖励"
        );

        Log.Information("用户 {UserId} 注册成功，已发放 50 胡萝卜奖励", userId);
    }
    catch (Exception ex)
    {
        // 注册奖励发放失败不应影响注册流程
        // 记录错误日志，后续可通过对账任务补发
        Log.Error(ex, "用户 {UserId} 注册奖励发放失败", userId);
    }

    // 5. 自动登录或跳转到登录页
    TempData["RegisterSuccess"] = "注册成功！已赠送 50 胡萝卜，请登录。";
    return RedirectToAction("Login", new { username = model.Username });
}
```

### 方案 2：在 API 项目注册接口中集成

如果在 `Radish.Api` 项目的 `UserController` 中实现注册功能：

```csharp
// Radish.Api/Controllers/UserController.cs 或 Radish.Api/Controllers/v2/AccountController.cs

using Radish.IService;
using Radish.Model;
using Radish.Common.HelpTool;
using Microsoft.AspNetCore.Mvc;
using Radish.Shared;
using Radish.Shared.CustomEnum;

/// <summary>
/// 用户注册
/// </summary>
/// <param name="request">注册请求参数</param>
/// <returns>注册结果</returns>
/// <response code="200">注册成功</response>
/// <response code="400">参数错误或用户名已存在</response>
[HttpPost]
[AllowAnonymous]
[ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
public async Task<MessageModel> Register([FromBody] RegisterRequest request)
{
    try
    {
        // 1. 参数验证
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "用户名和密码不能为空"
            };
        }

        // 2. 检查用户名是否已存在
        var existingUsers = await _userService.QueryAsync(u => u.LoginName == request.Username);
        if (existingUsers.Any())
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "用户名已存在"
            };
        }

        // 3. 创建用户
        var hashedPassword = PasswordHasher.HashPassword(request.Password);
        var newUser = new User
        {
            LoginName = request.Username,
            LoginPwd = hashedPassword,
            UserEmail = request.Email,
            TenantId = 0,
            IsEnable = true,
            IsDeleted = false,
            CreateTime = DateTime.Now,
            CreateBy = "System",
            CreateId = 0
        };

        var userId = await _userService.AddAsync(newUser);

        // 4. 发放注册奖励
        var transactionNo = await _coinService.GrantCoinAsync(
            userId: userId,
            amount: 50000,  // 50 白萝卜 = 50000 胡萝卜
            transactionType: "SYSTEM_GRANT",
            businessType: "UserRegistration",
            businessId: userId,
            remark: "新用户注册奖励"
        );

        Log.Information("用户 {UserId} 注册成功，已发放 50 胡萝卜，交易流水号：{TransactionNo}",
            userId, transactionNo);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "注册成功！已赠送 50 胡萝卜",
            ResponseData = new
            {
                UserId = userId,
                Username = request.Username,
                InitialCoinBonus = 50000,
                TransactionNo = transactionNo
            }
        };
    }
    catch (Exception ex)
    {
        Log.Error(ex, "用户注册失败：{Username}", request.Username);
        return new MessageModel
        {
            IsSuccess = false,
            StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
            MessageInfo = "注册失败，请稍后重试"
        };
    }
}

/// <summary>
/// 用户注册请求参数
/// </summary>
public class RegisterRequest
{
    /// <summary>用户名</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>密码</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>邮箱（可选）</summary>
    public string? Email { get; set; }
}
```

## 依赖注册

### Auth 项目集成（如使用方案 1）

需要在 `Radish.Auth/Program.cs` 中添加对 `CoinService` 的引用：

```csharp
// 1. 引用 Radish.IService 和 Radish.Service 项目
// 2. 注册 SqlSugar（如果 Auth 项目还没有）
builder.Services.AddSqlSugarSetup();

// 3. Autofac 会自动注册 ICoinService（如果使用 AutofacModuleRegister）
// 或手动注册：
builder.Services.AddScoped<ICoinService, CoinService>();
```

### API 项目集成（如使用方案 2）

API 项目已经通过 Autofac 自动注册了 `ICoinService`，无需额外配置。

只需在 Controller 构造函数中注入：

```csharp
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICoinService _coinService;  // 添加这行

    public UserController(
        IUserService userService,
        ICoinService coinService)  // 添加这行
    {
        _userService = userService;
        _coinService = coinService;  // 添加这行
    }
}
```

## 错误处理建议

### 1. 事务一致性策略

**推荐**：注册奖励发放失败 **不应影响** 用户注册流程。

- **原因**：用户注册是核心功能，币奖励是附加功能
- **处理**：记录日志，后续可通过对账任务或管理员手动补发
- **示例**：使用 try-catch 包裹 `GrantCoinAsync`，失败时仅记录日志

### 2. 幂等性保证

如果需要严格保证"每个新用户只发放一次注册奖励"，可添加检查逻辑：

```csharp
// 在发放奖励前检查是否已发放过
var existingGrantTransactions = await _coinService.GetTransactionsAsync(
    userId: userId,
    pageIndex: 1,
    pageSize: 1,
    transactionType: "SYSTEM_GRANT",
    status: "SUCCESS"
);

if (existingGrantTransactions.Data.Any(t => t.BusinessType == "UserRegistration"))
{
    Log.Warning("用户 {UserId} 已领取过注册奖励，跳过发放", userId);
    // 跳过发放，避免重复
}
else
{
    // 发放奖励
    await _coinService.GrantCoinAsync(...);
}
```

### 3. 分布式环境注意事项

如果部署在多实例环境，建议使用 Redis 分布式锁防止并发重复发放：

```csharp
var lockKey = $"register_coin_grant:{userId}";
using var redisLock = await _redisLock.AcquireAsync(lockKey, TimeSpan.FromSeconds(10));

if (redisLock.IsAcquired)
{
    await _coinService.GrantCoinAsync(...);
}
else
{
    Log.Warning("获取注册奖励发放锁失败，可能已被其他实例处理：{UserId}", userId);
}
```

## 测试验证

### 1. 功能测试

注册新用户后，验证：
- ✅ 用户创建成功
- ✅ 余额记录已初始化（50000 胡萝卜）
- ✅ 交易记录存在（类型 `SYSTEM_GRANT`，状态 `SUCCESS`）
- ✅ 余额变动日志已记录

### 2. 测试脚本

```bash
# 1. 注册新用户（假设注册接口为 /api/v2/Account/Register）
curl -X POST "http://localhost:5100/api/v2/Account/Register" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser001",
    "password": "Test@123456",
    "email": "testuser001@example.com"
  }'

# 2. 登录获取 Token（参考 Radish.Api.AuthFlow.http）
# ...

# 3. 查询余额
curl -X GET "http://localhost:5100/api/v2/Coin/GetBalance" \
  -H "Authorization: Bearer {access_token}"

# 预期响应：
# {
#   "isSuccess": true,
#   "responseData": {
#     "balance": 50000,
#     "balanceDisplay": "50.000"
#   }
# }

# 4. 查询交易记录
curl -X GET "http://localhost:5100/api/v2/Coin/GetTransactions?pageIndex=1&pageSize=10" \
  -H "Authorization: Bearer {access_token}"

# 预期响应：
# {
#   "data": [
#     {
#       "transactionType": "SYSTEM_GRANT",
#       "transactionTypeDisplay": "系统赠送",
#       "amount": 50000,
#       "amountDisplay": "50.000",
#       "businessType": "UserRegistration",
#       "remark": "新用户注册奖励",
#       "status": "SUCCESS"
#     }
#   ]
# }
```

## 后续优化

### 1. 新手任务奖励

参考设计文档 3.3 节，可添加额外的新手任务奖励：

- 完善个人资料：+10 胡萝卜
- 首次发帖：+20 胡萝卜
- 首次评论：+10 胡萝卜
- 设置头像：+10 胡萝卜

### 2. 奖励配置化

将注册奖励金额提取到配置文件：

```json
{
  "CoinSystem": {
    "Rewards": {
      "UserRegistration": 50000,
      "CompleteProfile": 10000,
      "FirstPost": 20000,
      "FirstComment": 10000,
      "SetAvatar": 10000
    }
  }
}
```

### 3. 监控与告警

- 监控每日注册奖励总发放量
- 检测异常注册行为（批量注册刷币）
- 对账任务验证注册奖励数据一致性

## 参考文档

- [萝卜币系统设计文档](radish-coin-system.md) - 第 3.1 节：初始赠送
- [用户认证流程](authentication.md) - 登录与注册
- [萝卜币 API 文档](../../Radish.Api/Controllers/v2/CoinController.cs) - CoinController 接口说明
