using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.Infrastructure;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>萝卜币服务实现</summary>
public class CoinService : BaseService<UserBalance, UserBalanceVo>, ICoinService
{
    private readonly IBaseRepository<UserBalance> _userBalanceRepository;
    private readonly IBaseRepository<CoinTransaction> _coinTransactionRepository;
    private readonly IBaseRepository<BalanceChangeLog> _balanceChangeLogRepository;

    /// <summary>乐观锁冲突重试次数</summary>
    private const int MaxRetryCount = 3;

    /// <summary>重试基础延迟（毫秒）</summary>
    private const int BaseRetryDelayMs = 100;

    public CoinService(
        IMapper mapper,
        IBaseRepository<UserBalance> userBalanceRepository,
        IBaseRepository<CoinTransaction> coinTransactionRepository,
        IBaseRepository<BalanceChangeLog> balanceChangeLogRepository)
        : base(mapper, userBalanceRepository)
    {
        _userBalanceRepository = userBalanceRepository;
        _coinTransactionRepository = coinTransactionRepository;
        _balanceChangeLogRepository = balanceChangeLogRepository;
    }

    #region 余额查询

    /// <summary>
    /// 获取用户余额信息
    /// </summary>
    public async Task<UserBalanceVo?> GetBalanceAsync(long userId)
    {
        try
        {
            var userBalance = await _userBalanceRepository.QueryFirstAsync(b => b.UserId == userId);

            if (userBalance == null)
            {
                // 如果用户余额记录不存在，自动创建初始记录
                Log.Information("用户 {UserId} 余额记录不存在，自动创建初始记录", userId);
                userBalance = await InitializeUserBalanceAsync(userId);
            }

            return Mapper.Map<UserBalanceVo>(userBalance);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 余额信息失败", userId);
            throw;
        }
    }

    /// <summary>
    /// 批量获取用户余额信息
    /// </summary>
    public async Task<Dictionary<long, UserBalanceVo>> GetBalancesAsync(List<long> userIds)
    {
        try
        {
            var userBalances = await _userBalanceRepository.QueryAsync(
                u => userIds.Contains(u.UserId)
            );

            return userBalances.ToDictionary(
                u => u.UserId,
                u => Mapper.Map<UserBalanceVo>(u)
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "批量获取用户余额信息失败");
            throw;
        }
    }

    /// <summary>
    /// 初始化用户余额记录
    /// </summary>
    private async Task<UserBalance> InitializeUserBalanceAsync(long userId)
    {
        var userBalance = new UserBalance
        {
            UserId = userId,
            Balance = 0,
            FrozenBalance = 0,
            TotalEarned = 0,
            TotalSpent = 0,
            TotalTransferredIn = 0,
            TotalTransferredOut = 0,
            Version = 0,
            CreateTime = DateTime.Now,
            CreateBy = "System",
            CreateId = 0
        };

        await _userBalanceRepository.AddAsync(userBalance);
        return userBalance;
    }

    #endregion

    #region 萝卜币发放

    /// <summary>
    /// 发放萝卜币（系统赠送、奖励等）
    /// </summary>
    public async Task<string> GrantCoinAsync(
        long userId,
        long amount,
        string transactionType,
        string? businessType = null,
        long? businessId = null,
        string? remark = null)
    {
        try
        {
            // 1. 参数校验
            if (amount <= 0)
            {
                throw new ArgumentException("发放金额必须大于 0", nameof(amount));
            }

            if (string.IsNullOrWhiteSpace(transactionType))
            {
                throw new ArgumentException("交易类型不能为空", nameof(transactionType));
            }

            Log.Information("开始发放萝卜币：用户={UserId}, 金额={Amount}, 类型={TransactionType}",
                userId, amount, transactionType);

            // 2. 使用乐观锁重试策略执行发放操作（最多重试 3 次，指数退避）
            var transactionNo = await ExecuteWithRetryAsync(async () =>
                await GrantCoinInternalAsync(userId, amount, transactionType, businessType, businessId, remark)
            );

            Log.Information("萝卜币发放成功：用户={UserId}, 金额={Amount}, 流水号={TransactionNo}",
                userId, amount, transactionNo);

            return transactionNo;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放萝卜币失败：用户={UserId}, 金额={Amount}, 类型={TransactionType}",
                userId, amount, transactionType);
            throw;
        }
    }

    /// <summary>
    /// 发放萝卜币的内部实现（包含事务和乐观锁）
    /// </summary>
    [UseTran(Propagation = Propagation.Required)]
    private async Task<string> GrantCoinInternalAsync(
        long userId,
        long amount,
        string transactionType,
        string? businessType,
        long? businessId,
        string? remark)
    {
        // 1. 确保用户余额记录存在
        var userBalance = await _userBalanceRepository.QueryFirstAsync(b => b.UserId == userId);
        if (userBalance == null)
        {
            userBalance = await InitializeUserBalanceAsync(userId);
        }

        // 2. 生成交易流水号
        var transactionNo = $"TXN_{SnowFlakeSingle.Instance.NextId()}";

        // 3. 创建交易记录（状态为 PENDING）
        var transaction = new CoinTransaction
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TransactionNo = transactionNo,
            FromUserId = null, // 系统发起
            ToUserId = userId,
            Amount = amount,
            Fee = 0,
            TransactionType = transactionType,
            Status = "PENDING",
            BusinessType = businessType,
            BusinessId = businessId,
            Remark = remark,
            CreateTime = DateTime.Now,
            CreateBy = "System",
            CreateId = 0
        };

        await _coinTransactionRepository.AddAsync(transaction);

        // 4. 记录变动前余额
        var balanceBefore = userBalance.Balance;

        // 5. 更新用户余额（使用乐观锁）
        var updatedRows = await _userBalanceRepository.UpdateColumnsAsync(
            u => new UserBalance
            {
                Balance = u.Balance + amount,
                TotalEarned = u.TotalEarned + amount,
                Version = u.Version + 1,
                ModifyTime = DateTime.Now,
                ModifyBy = "System",
                ModifyId = 0
            },
            u => u.UserId == userId && u.Version == userBalance.Version
        );

        if (updatedRows == 0)
        {
            throw new ConcurrencyException("乐观锁冲突：余额已被其他操作修改");
        }

        // 6. 创建余额变动日志
        var balanceChangeLog = new BalanceChangeLog
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            UserId = userId,
            TransactionId = transaction.Id,
            ChangeAmount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceBefore + amount,
            ChangeType = "EARN",
            CreateTime = DateTime.Now,
            CreateBy = "System",
            CreateId = 0
        };

        await _balanceChangeLogRepository.AddAsync(balanceChangeLog);

        // 7. 更新交易状态为 SUCCESS
        await _coinTransactionRepository.UpdateColumnsAsync(
            t => new CoinTransaction
            {
                Status = "SUCCESS",
                ModifyTime = DateTime.Now
            },
            t => t.Id == transaction.Id
        );

        return transactionNo;
    }

    /// <summary>
    /// 批量发放萝卜币
    /// </summary>
    public async Task<List<string>> BatchGrantCoinAsync(List<CoinGrantInfo> grantInfos)
    {
        var transactionNos = new List<string>();

        foreach (var grantInfo in grantInfos)
        {
            try
            {
                var transactionNo = await GrantCoinAsync(
                    grantInfo.UserId,
                    grantInfo.Amount,
                    grantInfo.TransactionType,
                    grantInfo.BusinessType,
                    grantInfo.BusinessId,
                    grantInfo.Remark
                );

                transactionNos.Add(transactionNo);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "批量发放萝卜币失败：用户={UserId}, 金额={Amount}",
                    grantInfo.UserId, grantInfo.Amount);
            }
        }

        return transactionNos;
    }

    #endregion

    #region 交易记录查询

    /// <summary>
    /// 获取用户交易记录（分页）
    /// </summary>
    public async Task<PageModel<CoinTransactionVo>> GetTransactionsAsync(
        long userId,
        int pageIndex,
        int pageSize,
        string? transactionType = null,
        string? status = null)
    {
        try
        {
            // 1. 构建查询条件
            var whereExpression = Expressionable.Create<CoinTransaction>()
                .And(t => t.FromUserId == userId || t.ToUserId == userId);

            if (!string.IsNullOrWhiteSpace(transactionType))
            {
                whereExpression.And(t => t.TransactionType == transactionType);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                whereExpression.And(t => t.Status == status);
            }

            // 2. 查询分页数据
            var (transactions, totalCount) = await _coinTransactionRepository.QueryPageAsync(
                whereExpression.ToExpression(),
                pageIndex,
                pageSize,
                t => t.CreateTime,
                OrderByType.Desc
            );

            // 3. 映射为 ViewModel
            var transactionVos = Mapper.Map<List<CoinTransactionVo>>(transactions);

            // 4. 补充用户名信息（需要在 Service 层单独设置）
            // TODO: 后续优化可以批量查询用户名，避免 N+1 查询
            foreach (var vo in transactionVos)
            {
                vo.VoFromUserName = vo.VoFromUserId.HasValue ? "用户" + vo.VoFromUserId : "系统";
                vo.VoToUserName = vo.VoToUserId.HasValue ? "用户" + vo.VoToUserId : "系统";
            }

            return new PageModel<CoinTransactionVo>
            {
                Page = pageIndex,
                PageSize = pageSize,
                DataCount = totalCount,
                PageCount = (int)Math.Ceiling(totalCount / (double)pageSize),
                Data = transactionVos
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 交易记录失败", userId);
            throw;
        }
    }

    /// <summary>
    /// 根据交易流水号获取交易详情
    /// </summary>
    public async Task<CoinTransactionVo?> GetTransactionByNoAsync(string transactionNo)
    {
        try
        {
            var transaction = await _coinTransactionRepository.QueryFirstAsync(
                t => t.TransactionNo == transactionNo
            );

            if (transaction == null)
            {
                return null;
            }

            var transactionVo = Mapper.Map<CoinTransactionVo>(transaction);

            // 补充用户名信息
            transactionVo.VoFromUserName = transactionVo.VoFromUserId.HasValue ? "用户" + transactionVo.VoFromUserId : "系统";
            transactionVo.VoToUserName = transactionVo.VoToUserId.HasValue ? "用户" + transactionVo.VoToUserId : "系统";

            return transactionVo;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "根据交易流水号 {TransactionNo} 获取交易详情失败", transactionNo);
            throw;
        }
    }

    #endregion

    #region 管理员操作

    /// <summary>
    /// 管理员调整用户余额
    /// </summary>
    public async Task<string> AdminAdjustBalanceAsync(
        long userId,
        long deltaAmount,
        string reason,
        long operatorId,
        string operatorName)
    {
        try
        {
            // 1. 参数校验
            if (deltaAmount == 0)
            {
                throw new ArgumentException("调整金额不能为 0", nameof(deltaAmount));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("调整原因不能为空", nameof(reason));
            }

            Log.Information("管理员调整余额：用户={UserId}, 金额={DeltaAmount}, 操作员={OperatorName}, 原因={Reason}",
                userId, deltaAmount, operatorName, reason);

            // 2. 使用乐观锁重试策略执行调整操作（最多重试 3 次，指数退避）
            var transactionNo = await ExecuteWithRetryAsync(async () =>
                await AdminAdjustBalanceInternalAsync(userId, deltaAmount, reason, operatorId, operatorName)
            );

            Log.Information("管理员调整余额成功：用户={UserId}, 金额={DeltaAmount}, 流水号={TransactionNo}",
                userId, deltaAmount, transactionNo);

            return transactionNo;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "管理员调整余额失败：用户={UserId}, 金额={DeltaAmount}",
                userId, deltaAmount);
            throw;
        }
    }

    /// <summary>
    /// 管理员调整余额的内部实现（包含事务和乐观锁）
    /// </summary>
    [UseTran(Propagation = Propagation.Required)]
    private async Task<string> AdminAdjustBalanceInternalAsync(
        long userId,
        long deltaAmount,
        string reason,
        long operatorId,
        string operatorName)
    {
        // 1. 确保用户余额记录存在
        var userBalance = await _userBalanceRepository.QueryFirstAsync(b => b.UserId == userId);
        if (userBalance == null)
        {
            userBalance = await InitializeUserBalanceAsync(userId);
        }

        // 2. 检查余额是否充足（如果是扣款）
        if (deltaAmount < 0 && userBalance.Balance < Math.Abs(deltaAmount))
        {
            throw new InvalidOperationException($"余额不足：当前余额={userBalance.Balance}, 扣除金额={Math.Abs(deltaAmount)}");
        }

        // 3. 生成交易流水号
        var transactionNo = $"TXN_{SnowFlakeSingle.Instance.NextId()}";

        // 4. 创建交易记录
        var transaction = new CoinTransaction
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TransactionNo = transactionNo,
            FromUserId = deltaAmount < 0 ? userId : null,
            ToUserId = deltaAmount > 0 ? userId : null,
            Amount = Math.Abs(deltaAmount),
            Fee = 0,
            TransactionType = "ADMIN_ADJUST",
            Status = "PENDING",
            BusinessType = "Admin",
            BusinessId = operatorId,
            Remark = reason,
            CreateTime = DateTime.Now,
            CreateBy = operatorName,
            CreateId = operatorId
        };

        await _coinTransactionRepository.AddAsync(transaction);

        // 5. 记录变动前余额
        var balanceBefore = userBalance.Balance;

        // 6. 更新用户余额（使用乐观锁）
        var updatedRows = await _userBalanceRepository.UpdateColumnsAsync(
            u => new UserBalance
            {
                Balance = u.Balance + deltaAmount,
                TotalEarned = deltaAmount > 0 ? u.TotalEarned + deltaAmount : u.TotalEarned,
                TotalSpent = deltaAmount < 0 ? u.TotalSpent + Math.Abs(deltaAmount) : u.TotalSpent,
                Version = u.Version + 1,
                ModifyTime = DateTime.Now,
                ModifyBy = operatorName,
                ModifyId = operatorId
            },
            u => u.UserId == userId && u.Version == userBalance.Version
        );

        if (updatedRows == 0)
        {
            throw new ConcurrencyException("乐观锁冲突：余额已被其他操作修改");
        }

        // 7. 创建余额变动日志
        var changeType = deltaAmount > 0 ? "ADMIN_ADJUST" : "ADMIN_ADJUST";
        var balanceChangeLog = new BalanceChangeLog
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            UserId = userId,
            TransactionId = transaction.Id,
            ChangeAmount = deltaAmount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceBefore + deltaAmount,
            ChangeType = changeType,
            CreateTime = DateTime.Now,
            CreateBy = operatorName,
            CreateId = operatorId
        };

        await _balanceChangeLogRepository.AddAsync(balanceChangeLog);

        // 8. 更新交易状态为 SUCCESS
        await _coinTransactionRepository.UpdateColumnsAsync(
            t => new CoinTransaction
            {
                Status = "SUCCESS",
                ModifyTime = DateTime.Now
            },
            t => t.Id == transaction.Id
        );

        return transactionNo;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 执行带重试的异步操作（乐观锁冲突时自动重试）
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="action">要执行的异步操作</param>
    /// <returns>操作结果</returns>
    /// <remarks>
    /// 重试策略：
    /// - 最多重试 3 次
    /// - 指数退避：100ms, 200ms, 400ms
    /// - 仅捕获 ConcurrencyException 进行重试
    /// </remarks>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
    {
        var retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= MaxRetryCount)
        {
            try
            {
                return await action();
            }
            catch (ConcurrencyException ex)
            {
                lastException = ex;
                retryCount++;

                if (retryCount > MaxRetryCount)
                {
                    Log.Error(ex, "乐观锁冲突重试 {MaxRetryCount} 次后仍然失败", MaxRetryCount);
                    throw;
                }

                // 指数退避：100ms * 2^(retryCount-1)
                var delayMs = BaseRetryDelayMs * (int)Math.Pow(2, retryCount - 1);
                Log.Warning("乐观锁冲突，第 {RetryCount} 次重试（延迟 {DelayMs}ms）: {Message}",
                    retryCount, delayMs, ex.Message);

                await Task.Delay(delayMs);
            }
        }

        // 理论上不会执行到这里，但为了类型安全
        throw lastException ?? new ConcurrencyException("重试失败");
    }

    #endregion
}
