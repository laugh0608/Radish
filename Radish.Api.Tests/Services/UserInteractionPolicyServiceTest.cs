using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.Model;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class UserInteractionPolicyServiceTest
{
    [Fact]
    public async Task GetSnapshotsAsync_ShouldExposeSymmetricBarrierWithoutDisclosingInboundDirection()
    {
        var repository = new Mock<IUserBlockRepository>(MockBehavior.Strict);
        repository
            .Setup(item => item.QueryActiveBetweenAsync(9, 1001, It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync(
            [
                new UserBlock
                {
                    TenantId = 9,
                    BlockerUserId = 2002,
                    BlockedUserId = 1001
                },
                new UserBlock
                {
                    TenantId = 9,
                    BlockerUserId = 1001,
                    BlockedUserId = 3003
                }
            ]);
        var service = new UserInteractionPolicyService(repository.Object);

        var result = await service.GetSnapshotsAsync(9, 1001, [2002, 3003, 4004]);

        Assert.True(result[2002].HasInteractionBarrier);
        Assert.False(result[2002].IsBlockedByCurrentUser);
        Assert.True(result[3003].HasInteractionBarrier);
        Assert.True(result[3003].IsBlockedByCurrentUser);
        Assert.False(result[4004].HasInteractionBarrier);
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.EnsureCanInteractAsync(9, 1001, 2002));
    }

    [Fact]
    public async Task PolicyReadFailure_ShouldFailClosedWithStableServiceUnavailableContract()
    {
        var repository = new Mock<IUserBlockRepository>(MockBehavior.Strict);
        repository
            .Setup(item => item.QueryActiveBetweenAsync(9, 1001, It.IsAny<IReadOnlyCollection<long>>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var service = new UserInteractionPolicyService(repository.Object);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetSnapshotAsync(9, 1001, 2002));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, exception.StatusCode);
        Assert.Equal("UserBlock.RelationshipTemporarilyUnavailable", exception.ErrorCode);
        Assert.Equal("error.user_block.relationship_temporarily_unavailable", exception.MessageKey);
    }
}
