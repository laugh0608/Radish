using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Services;

public class OperationIdempotencyServiceTest
{
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

        var service = new OperationIdempotencyService(repository.Object);
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
            record.Status == OperationIdempotencyStatuses.Processing)), Times.Once);
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
            ExpiresAt = DateTime.Now.AddHours(1)
        };

        var repository = CreateRepositoryReturning(existing);
        var service = new OperationIdempotencyService(repository.Object);

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
            ExpiresAt = DateTime.Now.AddHours(1)
        };

        var repository = CreateRepositoryReturning(existing);
        var service = new OperationIdempotencyService(repository.Object);

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
            ExpiresAt = DateTime.Now.AddHours(1)
        };

        var repository = CreateRepositoryReturning(existing);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<OperationIdempotencyRecord>()))
            .ReturnsAsync(true);
        var service = new OperationIdempotencyService(repository.Object);

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
    public async Task BeginAsync_ShouldRejectInvalidKey()
    {
        var repository = new Mock<IBaseRepository<OperationIdempotencyRecord>>(MockBehavior.Strict);
        var service = new OperationIdempotencyService(repository.Object);

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
}
