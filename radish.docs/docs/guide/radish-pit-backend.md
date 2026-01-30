# 萝卜坑应用后端设计

> 版本：v1.0 | 最后更新：2026-01-24 | 状态：设计中

本文档详细描述萝卜坑应用的后端技术实现方案。

---

## 6. 数据库设计

### 6.1 现有表结构扩展

基于现有的萝卜币系统，萝卜坑应用主要复用以下表结构：

#### UserBalance (用户余额表) - 已存在
```sql
CREATE TABLE UserBalance (
    Id BIGINT PRIMARY KEY,              -- 雪花ID
    UserId BIGINT NOT NULL UNIQUE,      -- 用户ID，外键关联User表
    Balance BIGINT NOT NULL DEFAULT 0,  -- 可用余额(胡萝卜)
    FrozenBalance BIGINT NOT NULL DEFAULT 0, -- 冻结余额(胡萝卜)
    TotalEarned BIGINT NOT NULL DEFAULT 0,   -- 累计获得(胡萝卜)
    TotalSpent BIGINT NOT NULL DEFAULT 0,    -- 累计消费(胡萝卜)
    TotalTransferredIn BIGINT NOT NULL DEFAULT 0,  -- 累计转入(胡萝卜)
    TotalTransferredOut BIGINT NOT NULL DEFAULT 0, -- 累计转出(胡萝卜)
    Version INT NOT NULL DEFAULT 1,     -- 乐观锁版本号
    CreateTime DATETIME NOT NULL,       -- 创建时间
    CreateBy BIGINT,                    -- 创建人
    ModifyTime DATETIME,                -- 修改时间
    ModifyBy BIGINT,                    -- 修改人
    IsDeleted BOOLEAN NOT NULL DEFAULT FALSE -- 软删除标记
);

-- 索引
CREATE UNIQUE INDEX IX_UserBalance_UserId ON UserBalance(UserId);
CREATE INDEX IX_UserBalance_Balance ON UserBalance(Balance DESC);
```

#### CoinTransaction (交易记录表) - 已存在
```sql
CREATE TABLE CoinTransaction (
    Id BIGINT PRIMARY KEY,              -- 雪花ID
    TransactionNo VARCHAR(64) NOT NULL UNIQUE, -- 交易流水号
    FromUserId BIGINT,                  -- 发起方用户ID
    ToUserId BIGINT,                    -- 接收方用户ID
    Amount BIGINT NOT NULL,             -- 交易金额(胡萝卜)
    Fee BIGINT NOT NULL DEFAULT 0,      -- 手续费(胡萝卜)
    TheoreticalAmount DECIMAL(18,6),    -- 理论金额(白萝卜)
    RoundingDiff DECIMAL(18,6),         -- 舍入差额(白萝卜)
    TransactionType VARCHAR(50) NOT NULL, -- 交易类型
    Status VARCHAR(20) NOT NULL,        -- 交易状态
    BusinessType VARCHAR(50),           -- 业务类型
    BusinessId BIGINT,                  -- 业务ID
    Remark VARCHAR(500),                -- 备注
    CreateTime DATETIME NOT NULL,       -- 创建时间
    CreateBy BIGINT,                    -- 创建人
    ModifyTime DATETIME,                -- 修改时间
    ModifyBy BIGINT                     -- 修改人
);

-- 索引
CREATE UNIQUE INDEX IX_CoinTransaction_TransactionNo ON CoinTransaction(TransactionNo);
CREATE INDEX IX_CoinTransaction_FromUserId_CreateTime ON CoinTransaction(FromUserId, CreateTime DESC);
CREATE INDEX IX_CoinTransaction_ToUserId_CreateTime ON CoinTransaction(ToUserId, CreateTime DESC);
CREATE INDEX IX_CoinTransaction_Type_Status ON CoinTransaction(TransactionType, Status);
```

### 6.2 新增表结构

#### UserPaymentPassword (用户支付密码表) - 新增
```sql
CREATE TABLE UserPaymentPassword (
    Id BIGINT PRIMARY KEY,              -- 雪花ID
    UserId BIGINT NOT NULL UNIQUE,      -- 用户ID，外键关联User表
    PasswordHash VARCHAR(255) NOT NULL, -- 密码哈希值(BCrypt)
    Salt VARCHAR(255) NOT NULL,         -- 盐值
    FailedAttempts INT NOT NULL DEFAULT 0, -- 失败尝试次数
    LockedUntil DATETIME,               -- 锁定到期时间
    LastUsedTime DATETIME,              -- 最后使用时间
    ExpiryTime DATETIME,                -- 密码过期时间
    CreateTime DATETIME NOT NULL,       -- 创建时间
    CreateBy BIGINT,                    -- 创建人
    ModifyTime DATETIME,                -- 修改时间
    ModifyBy BIGINT,                    -- 修改人
    IsDeleted BOOLEAN NOT NULL DEFAULT FALSE -- 软删除标记
);

-- 索引
CREATE UNIQUE INDEX IX_UserPaymentPassword_UserId ON UserPaymentPassword(UserId);
CREATE INDEX IX_UserPaymentPassword_LockedUntil ON UserPaymentPassword(LockedUntil);
```

#### UserSecurityLog (用户安全日志表) - 新增
```sql
CREATE TABLE UserSecurityLog (
    Id BIGINT PRIMARY KEY,              -- 雪花ID
    UserId BIGINT NOT NULL,             -- 用户ID
    ActionType VARCHAR(50) NOT NULL,    -- 操作类型
    ActionDescription VARCHAR(500),     -- 操作描述
    IpAddress VARCHAR(45),              -- IP地址
    UserAgent VARCHAR(1000),            -- 用户代理
    DeviceInfo VARCHAR(500),            -- 设备信息
    Result VARCHAR(20) NOT NULL,        -- 操作结果(SUCCESS/FAILED)
    RiskLevel VARCHAR(20) DEFAULT 'LOW', -- 风险等级(LOW/MEDIUM/HIGH)
    CreateTime DATETIME NOT NULL        -- 创建时间
);

-- 索引
CREATE INDEX IX_UserSecurityLog_UserId_CreateTime ON UserSecurityLog(UserId, CreateTime DESC);
CREATE INDEX IX_UserSecurityLog_ActionType ON UserSecurityLog(ActionType);
CREATE INDEX IX_UserSecurityLog_RiskLevel ON UserSecurityLog(RiskLevel);
```

#### TransferLimit (转账限制表) - 新增
```sql
CREATE TABLE TransferLimit (
    Id BIGINT PRIMARY KEY,              -- 雪花ID
    UserId BIGINT NOT NULL,             -- 用户ID
    LimitType VARCHAR(20) NOT NULL,     -- 限制类型(DAILY/MONTHLY)
    MaxAmount BIGINT NOT NULL,          -- 最大金额(胡萝卜)
    UsedAmount BIGINT NOT NULL DEFAULT 0, -- 已使用金额(胡萝卜)
    ResetTime DATETIME NOT NULL,        -- 重置时间
    CreateTime DATETIME NOT NULL,       -- 创建时间
    ModifyTime DATETIME                 -- 修改时间
);

-- 索引
CREATE INDEX IX_TransferLimit_UserId_LimitType ON TransferLimit(UserId, LimitType);
CREATE INDEX IX_TransferLimit_ResetTime ON TransferLimit(ResetTime);
```

---

## 7. Service层设计

### 7.1 现有Service扩展

#### CoinService 扩展 - 新增转账方法
```csharp
public class CoinService : BaseService<UserBalance, UserBalanceVo>, ICoinService
{
    // 现有方法保持不变...

    /// <summary>
    /// 用户间转账
    /// </summary>
    /// <param name="fromUserId">发起方用户ID</param>
    /// <param name="toUserId">接收方用户ID</param>
    /// <param name="amount">转账金额(胡萝卜)</param>
    /// <param name="remark">备注</param>
    /// <param name="paymentPassword">支付密码</param>
    /// <returns>转账结果</returns>
    public async Task<TransferResultVo> TransferAsync(long fromUserId, long toUserId,
        long amount, string remark, string paymentPassword)
    {
        // 1. 验证支付密码
        var passwordValid = await _paymentPasswordService.ValidatePasswordAsync(fromUserId, paymentPassword);
        if (!passwordValid)
        {
            throw new BusinessException("支付密码错误");
        }

        // 2. 检查转账限制
        await CheckTransferLimitAsync(fromUserId, amount);

        // 3. 执行转账事务
        return await ExecuteTransferAsync(fromUserId, toUserId, amount, remark);
    }

    /// <summary>
    /// 检查转账限制
    /// </summary>
    private async Task CheckTransferLimitAsync(long userId, long amount)
    {
        // 检查单笔限额
        if (amount < 1 || amount > 1000000)
        {
            throw new BusinessException("转账金额超出限制");
        }

        // 检查日限额
        var dailyLimit = await _transferLimitService.GetDailyLimitAsync(userId);
        if (dailyLimit.UsedAmount + amount > dailyLimit.MaxAmount)
        {
            throw new BusinessException("超出日转账限额");
        }

        // 检查余额
        var balance = await GetBalanceAsync(userId);
        if (balance.VoBalance < amount)
        {
            throw new BusinessException("余额不足");
        }
    }

    /// <summary>
    /// 执行转账事务
    /// </summary>
    private async Task<TransferResultVo> ExecuteTransferAsync(long fromUserId, long toUserId,
        long amount, string remark)
    {
        using var transaction = await _repository.BeginTransactionAsync();
        try
        {
            var transactionNo = GenerateTransactionNo();

            // 1. 扣减发起方余额
            await DeductBalanceAsync(fromUserId, amount, transactionNo);

            // 2. 增加接收方余额
            await AddBalanceAsync(toUserId, amount, transactionNo);

            // 3. 记录交易
            var coinTransaction = new CoinTransaction
            {
                TransactionNo = transactionNo,
                FromUserId = fromUserId,
                ToUserId = toUserId,
                Amount = amount,
                TransactionType = "transfer",
                Status = "success",
                Remark = remark
            };
            await _transactionRepository.InsertAsync(coinTransaction);

            // 4. 更新转账限额
            await _transferLimitService.UpdateUsedAmountAsync(fromUserId, amount);

            await transaction.CommitAsync();

            // 5. 发送通知
            await _notificationService.SendTransferNotificationAsync(fromUserId, toUserId, amount);

            return new TransferResultVo
            {
                VoTransactionNo = transactionNo,
                VoAmount = amount,
                VoStatus = "success"
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### 7.2 新增Service

#### PaymentPasswordService - 支付密码服务
```csharp
public interface IPaymentPasswordService
{
    Task<bool> HasPasswordAsync(long userId);
    Task SetPasswordAsync(long userId, string password);
    Task<bool> ValidatePasswordAsync(long userId, string password);
    Task ChangePasswordAsync(long userId, string oldPassword, string newPassword);
    Task ResetPasswordAsync(long userId, string newPassword, string securityAnswer);
    Task UnlockAccountAsync(long userId);
}

public class PaymentPasswordService : IPaymentPasswordService
{
    private readonly IBaseRepository<UserPaymentPassword> _repository;
    private readonly IUserSecurityLogService _securityLogService;

    public async Task<bool> ValidatePasswordAsync(long userId, string password)
    {
        var userPassword = await _repository.GetFirstAsync(x => x.UserId == userId);
        if (userPassword == null)
        {
            throw new BusinessException("未设置支付密码");
        }

        // 检查是否被锁定
        if (userPassword.LockedUntil.HasValue && userPassword.LockedUntil > DateTime.Now)
        {
            throw new BusinessException($"账户已锁定，请于{userPassword.LockedUntil:yyyy-MM-dd HH:mm}后重试");
        }

        // 验证密码
        var isValid = BCrypt.Net.BCrypt.Verify(password + userPassword.Salt, userPassword.PasswordHash);

        if (isValid)
        {
            // 重置失败次数
            userPassword.FailedAttempts = 0;
            userPassword.LastUsedTime = DateTime.Now;
            await _repository.UpdateAsync(userPassword);

            // 记录成功日志
            await _securityLogService.LogAsync(userId, "PAYMENT_PASSWORD_VERIFY", "支付密码验证成功", "SUCCESS");
        }
        else
        {
            // 增加失败次数
            userPassword.FailedAttempts++;

            // 检查是否需要锁定
            if (userPassword.FailedAttempts >= 5)
            {
                userPassword.LockedUntil = DateTime.Now.AddMinutes(30);
            }

            await _repository.UpdateAsync(userPassword);

            // 记录失败日志
            await _securityLogService.LogAsync(userId, "PAYMENT_PASSWORD_VERIFY",
                $"支付密码验证失败，失败次数：{userPassword.FailedAttempts}", "FAILED");
        }

        return isValid;
    }
}
```

#### TransferLimitService - 转账限制服务
```csharp
public interface ITransferLimitService
{
    Task<TransferLimitVo> GetDailyLimitAsync(long userId);
    Task UpdateUsedAmountAsync(long userId, long amount);
    Task ResetLimitAsync(long userId, string limitType);
}

public class TransferLimitService : ITransferLimitService
{
    private readonly IBaseRepository<TransferLimit> _repository;

    public async Task<TransferLimitVo> GetDailyLimitAsync(long userId)
    {
        var limit = await _repository.GetFirstAsync(x => x.UserId == userId && x.LimitType == "DAILY");

        if (limit == null || limit.ResetTime <= DateTime.Now)
        {
            // 创建或重置日限额
            limit = new TransferLimit
            {
                UserId = userId,
                LimitType = "DAILY",
                MaxAmount = 5000000, // 500万胡萝卜日限额
                UsedAmount = 0,
                ResetTime = DateTime.Today.AddDays(1)
            };

            if (limit.Id == 0)
            {
                await _repository.InsertAsync(limit);
            }
            else
            {
                limit.UsedAmount = 0;
                limit.ResetTime = DateTime.Today.AddDays(1);
                await _repository.UpdateAsync(limit);
            }
        }

        return new TransferLimitVo
        {
            VoMaxAmount = limit.MaxAmount,
            VoUsedAmount = limit.UsedAmount,
            VoRemainingAmount = limit.MaxAmount - limit.UsedAmount,
            VoResetTime = limit.ResetTime
        };
    }
}
```

---

## 8. API接口设计

### 8.1 CoinController 扩展

```csharp
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "Client")]
public class CoinController : BaseController
{
    // 现有接口保持不变...

    /// <summary>
    /// 用户间转账
    /// </summary>
    [HttpPost("Transfer")]
    public async Task<ApiResult<TransferResultVo>> Transfer([FromBody] TransferDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _coinService.TransferAsync(userId, dto.ToUserId, dto.Amount, dto.Remark, dto.PaymentPassword);
        return Success(result);
    }

    /// <summary>
    /// 获取转账限制信息
    /// </summary>
    [HttpGet("GetTransferLimit")]
    public async Task<ApiResult<TransferLimitVo>> GetTransferLimit()
    {
        var userId = GetCurrentUserId();
        var result = await _transferLimitService.GetDailyLimitAsync(userId);
        return Success(result);
    }

    /// <summary>
    /// 验证转账参数
    /// </summary>
    [HttpPost("ValidateTransfer")]
    public async Task<ApiResult<TransferValidationVo>> ValidateTransfer([FromBody] ValidateTransferDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _coinService.ValidateTransferAsync(userId, dto.ToUserId, dto.Amount);
        return Success(result);
    }
}
```

### 8.2 新增PaymentPasswordController

```csharp
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "Client")]
public class PaymentPasswordController : BaseController
{
    private readonly IPaymentPasswordService _paymentPasswordService;

    /// <summary>
    /// 检查是否已设置支付密码
    /// </summary>
    [HttpGet("HasPassword")]
    public async Task<ApiResult<bool>> HasPassword()
    {
        var userId = GetCurrentUserId();
        var result = await _paymentPasswordService.HasPasswordAsync(userId);
        return Success(result);
    }

    /// <summary>
    /// 设置支付密码
    /// </summary>
    [HttpPost("SetPassword")]
    public async Task<ApiResult> SetPassword([FromBody] SetPasswordDto dto)
    {
        var userId = GetCurrentUserId();
        await _paymentPasswordService.SetPasswordAsync(userId, dto.Password);
        return Success();
    }

    /// <summary>
    /// 修改支付密码
    /// </summary>
    [HttpPost("ChangePassword")]
    public async Task<ApiResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = GetCurrentUserId();
        await _paymentPasswordService.ChangePasswordAsync(userId, dto.OldPassword, dto.NewPassword);
        return Success();
    }

    /// <summary>
    /// 验证支付密码
    /// </summary>
    [HttpPost("ValidatePassword")]
    public async Task<ApiResult<bool>> ValidatePassword([FromBody] ValidatePasswordDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _paymentPasswordService.ValidatePasswordAsync(userId, dto.Password);
        return Success(result);
    }
}
```

### 8.3 DTO定义

```csharp
/// <summary>
/// 转账请求DTO
/// </summary>
public class TransferDto
{
    [Required]
    public long ToUserId { get; set; }

    [Required]
    [Range(1, 1000000)]
    public long Amount { get; set; }

    [MaxLength(200)]
    public string Remark { get; set; }

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string PaymentPassword { get; set; }
}

/// <summary>
/// 设置支付密码DTO
/// </summary>
public class SetPasswordDto
{
    [Required]
    [StringLength(6, MinimumLength = 6)]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "支付密码必须是6位数字")]
    public string Password { get; set; }

    [Required]
    public string ConfirmPassword { get; set; }
}
```

---

## 相关文档
- [萝卜坑应用总体设计](/guide/radish-pit-system)
- [萝卜坑核心概念](/guide/radish-pit-core-concepts)
- [萝卜坑前端设计](/guide/radish-pit-frontend)

---

> 本文档随萝卜坑应用开发持续更新，如有变更请同步修改 [更新日志](/changelog/)。