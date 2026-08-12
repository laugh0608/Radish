using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Serilog;
using SqlSugar;

namespace Radish.Service;

public partial class CoinService
{
    /// <summary>管理员按权威余额版本调整用户余额。</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<string> AdminAdjustBalanceAsync(
        long userId,
        long deltaAmount,
        string reason,
        long operatorId,
        string operatorName,
        int expectedVersion,
        string idempotencyKey)
    {
        try
        {
            if (deltaAmount == 0)
            {
                throw new ArgumentException("调整金额不能为 0", nameof(deltaAmount));
            }

            if (deltaAmount == long.MinValue)
            {
                throw new ArgumentException("调整金额超出有效范围", nameof(deltaAmount));
            }

            var normalizedReason = reason?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedReason))
            {
                throw new ArgumentException("调整原因不能为空", nameof(reason));
            }

            if (normalizedReason.Length > 200)
            {
                throw new ArgumentException("调整原因不能超过 200 个字符", nameof(reason));
            }

            if (operatorId <= 0)
            {
                throw new ArgumentException("操作员 ID 无效", nameof(operatorId));
            }

            if (expectedVersion < 0)
            {
                throw new ArgumentException("余额版本号无效", nameof(expectedVersion));
            }

            await EnsureUserExistsAsync(userId);
            if (_operationIdempotencyService == null)
            {
                throw new InvalidOperationException("管理员调账幂等服务不可用");
            }

            var normalizedOperatorName = string.IsNullOrWhiteSpace(operatorName)
                ? $"User_{operatorId}"
                : operatorName.Trim();
            var tenantId = GetCurrentTenantId();
            var idempotencyResult = await BeginAdminAdjustmentIdempotencyAsync(
                tenantId,
                operatorId,
                userId,
                deltaAmount,
                normalizedReason,
                expectedVersion,
                idempotencyKey);
            var replayTransactionNo = ResolveAdminAdjustmentIdempotencyResult(idempotencyResult);
            if (replayTransactionNo != null)
            {
                return replayTransactionNo;
            }

            Log.Information("管理员调整余额：用户={UserId}, 金额={DeltaAmount}, 操作员={OperatorName}, 原因={Reason}",
                userId, deltaAmount, normalizedOperatorName, normalizedReason);

            var result = await AdminAdjustBalanceInternalAsync(
                tenantId,
                userId,
                deltaAmount,
                normalizedReason,
                operatorId,
                normalizedOperatorName,
                expectedVersion);
            await CompleteAdminAdjustmentIdempotencyAsync(idempotencyResult, result.transactionId, result.transactionNo);

            Log.Information("管理员调整余额成功：用户={UserId}, 金额={DeltaAmount}, 流水号={TransactionNo}",
                userId, deltaAmount, result.transactionNo);
            return result.transactionNo;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "管理员调整余额失败：用户={UserId}, 金额={DeltaAmount}", userId, deltaAmount);
            throw;
        }
    }

    private async Task<(long transactionId, string transactionNo)> AdminAdjustBalanceInternalAsync(
        long tenantId,
        long userId,
        long deltaAmount,
        string reason,
        long operatorId,
        string operatorName,
        int expectedVersion)
    {
        var userBalance = await _userBalanceRepository.QueryFirstAsync(
            balance => balance.UserId == userId && balance.TenantId == tenantId && !balance.IsDeleted);
        if (userBalance == null)
        {
            userBalance = await InitializeUserBalanceAsync(userId);
        }

        if (userBalance.Version != expectedVersion)
        {
            throw CreateAdminAdjustmentVersionConflictException();
        }

        var absoluteAmount = Math.Abs(deltaAmount);
        if (deltaAmount < 0 && userBalance.Balance < absoluteAmount)
        {
            throw new InvalidOperationException($"余额不足：当前余额={userBalance.Balance}, 扣除金额={absoluteAmount}");
        }

        if (deltaAmount > 0 && userBalance.Balance > long.MaxValue - deltaAmount)
        {
            throw new ArgumentException("调整后余额超出有效范围", nameof(deltaAmount));
        }

        var transactionNo = $"TXN_{SnowFlakeSingle.Instance.NextId()}";
        var transaction = new CoinTransaction
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TransactionNo = transactionNo,
            FromUserId = deltaAmount < 0 ? userId : null,
            ToUserId = deltaAmount > 0 ? userId : null,
            Amount = absoluteAmount,
            Fee = 0,
            TransactionType = "ADMIN_ADJUST",
            Status = "PENDING",
            BusinessType = "Admin",
            BusinessId = operatorId,
            Remark = reason,
            TenantId = tenantId,
            CreateTime = DateTime.UtcNow,
            CreateBy = operatorName,
            CreateId = operatorId
        };
        await _coinTransactionRepository.AddAsync(transaction);

        var balanceBefore = userBalance.Balance;
        var updatedRows = await _userBalanceRepository.UpdateColumnsAsync(
            balance => new UserBalance
            {
                Balance = balance.Balance + deltaAmount,
                TotalEarned = deltaAmount > 0 ? balance.TotalEarned + deltaAmount : balance.TotalEarned,
                TotalSpent = deltaAmount < 0 ? balance.TotalSpent + absoluteAmount : balance.TotalSpent,
                Version = balance.Version + 1,
                ModifyTime = DateTime.UtcNow,
                ModifyBy = operatorName,
                ModifyId = operatorId
            },
            balance => balance.UserId == userId &&
                       balance.TenantId == tenantId &&
                       !balance.IsDeleted &&
                       balance.Version == expectedVersion);
        if (updatedRows != 1)
        {
            throw CreateAdminAdjustmentVersionConflictException();
        }

        await _balanceChangeLogRepository.AddAsync(new BalanceChangeLog
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            UserId = userId,
            TransactionId = transaction.Id,
            ChangeAmount = deltaAmount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceBefore + deltaAmount,
            ChangeType = "ADMIN_ADJUST",
            SourceEventKey = $"coin-admin-adjust:{transaction.Id}:{userId}",
            TenantId = tenantId,
            CreateTime = DateTime.UtcNow,
            CreateBy = operatorName,
            CreateId = operatorId
        });

        var transactionUpdatedRows = await _coinTransactionRepository.UpdateColumnsAsync(
            stored => new CoinTransaction
            {
                Status = "SUCCESS",
                ModifyTime = DateTime.UtcNow,
                ModifyBy = operatorName,
                ModifyId = operatorId
            },
            stored => stored.Id == transaction.Id && stored.TenantId == tenantId && stored.Status == "PENDING");
        if (transactionUpdatedRows != 1)
        {
            throw new InvalidOperationException("管理员调账流水状态写入失败");
        }

        return (transaction.Id, transactionNo);
    }

    private async Task<OperationIdempotencyBeginResult> BeginAdminAdjustmentIdempotencyAsync(
        long tenantId,
        long operatorId,
        long targetUserId,
        long deltaAmount,
        string reason,
        int expectedVersion,
        string idempotencyKey)
    {
        var key = _operationIdempotencyService!.NormalizeKey(idempotencyKey);
        var snapshot = _operationIdempotencyService.CreateRequestSnapshot(
            new Dictionary<string, object?>
            {
                ["targetUserId"] = targetUserId,
                ["deltaAmount"] = deltaAmount,
                ["reason"] = reason,
                ["expectedVersion"] = expectedVersion
            });

        return await _operationIdempotencyService.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = tenantId,
            UserId = operatorId,
            OperationType = OperationIdempotencyOperationTypes.CoinAdminAdjustment,
            IdempotencyKey = key,
            RequestHash = snapshot.RequestHash,
            RequestSummary = snapshot.RequestSummary,
            AllowExpiredProcessingReset = false
        });
    }

    private string? ResolveAdminAdjustmentIdempotencyResult(OperationIdempotencyBeginResult idempotencyResult)
    {
        switch (idempotencyResult.Status)
        {
            case OperationIdempotencyBeginStatus.Started:
                return null;
            case OperationIdempotencyBeginStatus.Succeeded:
            {
                var replayResult = _operationIdempotencyService?.DeserializeResponse<TransactionResultVo>(
                    idempotencyResult.ResponsePayload);
                if (string.IsNullOrWhiteSpace(replayResult?.VoTransactionNo))
                {
                    throw new BusinessException(
                        "幂等记录缺少调账结果，请先刷新目标余额与流水",
                        409,
                        CoinErrorCodes.AdminAdjustReplayUnavailable,
                        "error.coin.admin_adjust_replay_unavailable");
                }

                return replayResult.VoTransactionNo;
            }
            case OperationIdempotencyBeginStatus.Processing:
                throw new BusinessException(
                    idempotencyResult.Message ?? "相同调账请求仍在处理中",
                    409,
                    CoinErrorCodes.AdminAdjustProcessing,
                    "error.coin.admin_adjust_processing");
            case OperationIdempotencyBeginStatus.Conflict:
                throw new BusinessException(
                    idempotencyResult.Message ?? "幂等键已被其他调账内容使用",
                    409,
                    CoinErrorCodes.AdminAdjustIdempotencyConflict,
                    "error.coin.admin_adjust_idempotency_conflict");
            case OperationIdempotencyBeginStatus.InvalidKey:
                throw new BusinessException(
                    idempotencyResult.Message ?? "调账请求标识格式无效",
                    400,
                    CoinErrorCodes.AdminAdjustIdempotencyInvalid,
                    "error.coin.admin_adjust_idempotency_invalid");
            default:
                throw new InvalidOperationException("管理员调账幂等记录状态无效");
        }
    }

    private async Task CompleteAdminAdjustmentIdempotencyAsync(
        OperationIdempotencyBeginResult idempotencyResult,
        long transactionId,
        string transactionNo)
    {
        if (idempotencyResult.Status != OperationIdempotencyBeginStatus.Started ||
            !idempotencyResult.RecordId.HasValue)
        {
            return;
        }

        await _operationIdempotencyService!.CompleteSuccessAsync(new OperationIdempotencyCompletionRequest
        {
            RecordId = idempotencyResult.RecordId.Value,
            ResourceType = OperationIdempotencyResourceTypes.CoinTransaction,
            ResourceId = transactionId,
            ResourceNo = transactionNo,
            ResponsePayload = _operationIdempotencyService.SerializeResponse(new TransactionResultVo
            {
                VoTransactionNo = transactionNo
            })
        });
    }

    private static BusinessException CreateAdminAdjustmentVersionConflictException()
    {
        return new BusinessException(
            "余额版本已变化，请刷新后重新确认调账",
            409,
            CoinErrorCodes.AdminAdjustVersionConflict,
            "error.coin.admin_adjust_version_conflict");
    }
}
