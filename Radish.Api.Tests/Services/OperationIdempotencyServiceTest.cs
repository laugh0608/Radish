using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Services;

public class OperationIdempotencyServiceTest
{
    private static readonly DateTime FixedNow = new(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BeginAsync_ShouldReturnStarted_WhenRecordDoesNotExist()
    {
        var repository = new Mock<IBaseRepository<OperationIdempotencyRecord>>(MockBehavior.Strict);
        repository
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<OperationIdempotencyRecord, bool>>?>()))
            .ReturnsAsync((OperationIdempotencyRecord?)null);
        repository
            .Setup(r => r.AddAsync(It.IsAny<OperationIdempotencyRecord>()))
            .ReturnsAsync(9001);

        var service = CreateService(repository);
        var snapshot = service.CreateRequestSnapshot(new Dictionary<string, object?>
        {
            ["productId"] = 1001,
            ["quantity"] = 1,
            ["userRemark"] = null
        });

        var result = await service.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.ShopPurchase,
            IdempotencyKey = "shop:abc",
            RequestHash = snapshot.RequestHash,
            RequestSummary = snapshot.RequestSummary
        });

        Assert.Equal(OperationIdempotencyBeginStatus.Started, result.Status);
        Assert.Equal(9001, result.RecordId);
        repository.Verify(r => r.AddAsync(It.Is<OperationIdempotencyRecord>(record =>
            record.TenantId == 0 &&
            record.UserId == 9527 &&
            record.OperationType == OperationIdempotencyOperationTypes.ShopPurchase &&
            record.IdempotencyKey == "shop:abc" &&
            record.Status == OperationIdempotencyStatuses.Processing &&
            record.CreateTime == FixedNow &&
            record.ExpiresAt == FixedNow.AddHours(24))), Times.Once);
    }

    [Fact]
    public async Task BeginAsync_ShouldRecoverConcurrentUniqueConflictThroughSavepoint()
    {
        var existing = new OperationIdempotencyRecord
        {
            Id = 9006,
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.CoinAdminAdjustment,
            IdempotencyKey = "coin-admin-adjust:concurrent",
            RequestHash = "hash-a",
            RequestSummary = "{}",
            Status = OperationIdempotencyStatuses.Processing,
            ExpiresAt = FixedNow.AddHours(1)
        };
        var repository = new Mock<IBaseRepository<OperationIdempotencyRecord>>(MockBehavior.Strict);
        var queryCount = 0;
        repository
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<OperationIdempotencyRecord, bool>>?>()))
            .Returns(() => Task.FromResult(queryCount++ == 0
                ? (OperationIdempotencyRecord?)null
                : existing));
        repository
            .Setup(r => r.AddAsync(It.IsAny<OperationIdempotencyRecord>()))
            .ThrowsAsync(new InvalidOperationException("23505 duplicate key value violates unique constraint"));
        var unitOfWork = CreateUnitOfWorkMock();
        var service = CreateService(repository, unitOfWork);

        var result = await service.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.CoinAdminAdjustment,
            IdempotencyKey = "coin-admin-adjust:concurrent",
            RequestHash = "hash-a",
            RequestSummary = "{}",
            AllowExpiredProcessingReset = false
        });

        Assert.Equal(OperationIdempotencyBeginStatus.Processing, result.Status);
        Assert.Equal(existing.Id, result.RecordId);
        Assert.Equal(2, queryCount);
        unitOfWork.Verify(
            uow => uow.ExecuteInSavepointAsync(It.IsAny<Func<Task<long>>>()),
            Times.Once);
    }

    [Fact]
    public async Task BeginAsync_ShouldReplaySucceededRecord_WhenHashMatches()
    {
        var existing = new OperationIdempotencyRecord
        {
            Id = 9002,
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.ShopPurchase,
            IdempotencyKey = "shop:abc",
            RequestHash = "hash-a",
            RequestSummary = "{}",
            Status = OperationIdempotencyStatuses.Succeeded,
            ResponsePayload = "{\"success\":true}",
            ExpiresAt = FixedNow.AddHours(1)
        };

        var repository = CreateRepositoryReturning(existing);
        var service = CreateService(repository);

        var result = await service.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.ShopPurchase,
            IdempotencyKey = "shop:abc",
            RequestHash = "hash-a",
            RequestSummary = "{}"
        });

        Assert.Equal(OperationIdempotencyBeginStatus.Succeeded, result.Status);
        Assert.Equal(9002, result.RecordId);
        Assert.Equal("{\"success\":true}", result.ResponsePayload);
    }

    [Fact]
    public async Task BeginAsync_ShouldRejectSameKeyWithDifferentRequestHash()
    {
        var existing = new OperationIdempotencyRecord
        {
            Id = 9003,
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.ShopPurchase,
            IdempotencyKey = "shop:abc",
            RequestHash = "hash-a",
            RequestSummary = "{}",
            Status = OperationIdempotencyStatuses.Processing,
            ExpiresAt = FixedNow.AddHours(1)
        };

        var repository = CreateRepositoryReturning(existing);
        var service = CreateService(repository);

        var result = await service.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.ShopPurchase,
            IdempotencyKey = "shop:abc",
            RequestHash = "hash-b",
            RequestSummary = "{}"
        });

        Assert.Equal(OperationIdempotencyBeginStatus.Conflict, result.Status);
        Assert.Equal("幂等键已被不同请求使用", result.Message);
        repository.Verify(r => r.UpdateAsync(It.IsAny<OperationIdempotencyRecord>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_ShouldResetFailedRecord_WhenHashMatches()
    {
        var existing = new OperationIdempotencyRecord
        {
            Id = 9004,
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.CoinTransfer,
            IdempotencyKey = "coin-transfer:abc",
            RequestHash = "hash-a",
            RequestSummary = "{}",
            Status = OperationIdempotencyStatuses.Failed,
            ErrorMessage = "余额不足",
            ExpiresAt = FixedNow.AddHours(1)
        };

        var repository = CreateRepositoryReturning(existing);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<OperationIdempotencyRecord>()))
            .ReturnsAsync(true);
        var service = CreateService(repository);

        var result = await service.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.CoinTransfer,
            IdempotencyKey = "coin-transfer:abc",
            RequestHash = "hash-a",
            RequestSummary = "{}"
        });

        Assert.Equal(OperationIdempotencyBeginStatus.Started, result.Status);
        Assert.Equal(9004, result.RecordId);
        repository.Verify(r => r.UpdateAsync(It.Is<OperationIdempotencyRecord>(record =>
            record.Status == OperationIdempotencyStatuses.Processing &&
            record.ErrorMessage == null &&
            record.CompleteTime == null)), Times.Once);
    }

    [Fact]
    public async Task BeginAsync_ShouldExposeExpiredProcessingWithoutReset_WhenCallerRequiresRecovery()
    {
        var existing = new OperationIdempotencyRecord
        {
            Id = 9005,
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.ContentReward,
            IdempotencyKey = "content-reward:abc",
            RequestHash = "hash-a",
            RequestSummary = "{}",
            Status = OperationIdempotencyStatuses.Processing,
            ExpiresAt = FixedNow.AddMinutes(-1)
        };
        var repository = CreateRepositoryReturning(existing);
        var service = CreateService(repository);

        var result = await service.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.ContentReward,
            IdempotencyKey = "content-reward:abc",
            RequestHash = "hash-a",
            RequestSummary = "{}",
            AllowExpiredProcessingReset = false
        });

        Assert.Equal(OperationIdempotencyBeginStatus.Processing, result.Status);
        Assert.True(result.IsExpiredProcessing);
        Assert.Equal(9005, result.RecordId);
        repository.Verify(r => r.UpdateAsync(It.IsAny<OperationIdempotencyRecord>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_ShouldRejectInvalidKey()
    {
        var repository = new Mock<IBaseRepository<OperationIdempotencyRecord>>(MockBehavior.Strict);
        var service = CreateService(repository);

        var result = await service.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = 0,
            UserId = 9527,
            OperationType = OperationIdempotencyOperationTypes.ShopPurchase,
            IdempotencyKey = "shop abc",
            RequestHash = "hash-a",
            RequestSummary = "{}"
        });

        Assert.Equal(OperationIdempotencyBeginStatus.InvalidKey, result.Status);
        repository.VerifyNoOtherCalls();
    }

    private static Mock<IBaseRepository<OperationIdempotencyRecord>> CreateRepositoryReturning(
        OperationIdempotencyRecord record)
    {
        var repository = new Mock<IBaseRepository<OperationIdempotencyRecord>>(MockBehavior.Strict);
        repository
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<OperationIdempotencyRecord, bool>>?>()))
            .ReturnsAsync(record);
        return repository;
    }

    private static OperationIdempotencyService CreateService(
        Mock<IBaseRepository<OperationIdempotencyRecord>> repository,
        Mock<IUnitOfWorkManage>? unitOfWork = null)
    {
        unitOfWork ??= CreateUnitOfWorkMock();
        return new OperationIdempotencyService(
            repository.Object,
            unitOfWork.Object,
            new FixedTimeProvider(FixedNow));
    }

    private static Mock<IUnitOfWorkManage> CreateUnitOfWorkMock()
    {
        var unitOfWork = new Mock<IUnitOfWorkManage>(MockBehavior.Strict);
        unitOfWork
            .Setup(uow => uow.ExecuteInSavepointAsync(It.IsAny<Func<Task<long>>>()))
            .Returns((Func<Task<long>> operation) => operation());
        return unitOfWork;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(utcNow);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
