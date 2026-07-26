using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class UserBlockServiceTest
{
    private const string TargetPublicId = "usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f";

    [Fact]
    public async Task BlockAsync_ShouldUseTenantScopedTargetAndReturnPolicyCapabilities()
    {
        var repository = new Mock<IUserBlockRepository>(MockBehavior.Strict);
        var policyService = new Mock<IUserInteractionPolicyService>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var target = new User
        {
            Id = 2002,
            TenantId = 9,
            PublicId = TargetPublicId,
            UserName = "target",
            IsEnable = true
        };
        userRepository
            .Setup(item => item.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((Expression<Func<User, bool>> predicate) =>
                predicate.Compile()(target) ? target : null);
        repository
            .Setup(item => item.MutateAsync(It.Is<UserBlockMutationCommand>(command =>
                command.TenantId == 9 &&
                command.ActorUserId == 1001 &&
                command.TargetUserId == 2002 &&
                command.OperationType == UserBlockOperationTypes.Block &&
                command.OperationKey == "block-operation-1" &&
                command.OperatorName == "actor")))
            .ReturnsAsync(new UserBlockWriteResult(
                new UserBlock
                {
                    Id = 3003,
                    TenantId = 9,
                    BlockerUserId = 1001,
                    BlockedUserId = 2002,
                    RelationshipVersion = 4
                },
                4,
                true,
                false));
        policyService
            .Setup(item => item.GetSnapshotAsync(9, 1001, 2002))
            .ReturnsAsync(new UserInteractionPolicySnapshot(2002, true, true));
        var service = CreateService(repository, policyService, userRepository);

        var result = await service.BlockAsync(
            9,
            1001,
            TargetPublicId.ToUpperInvariant(),
            " block-operation-1 ",
            "actor");

        Assert.Equal(2002, result.TargetUserId);
        Assert.Equal("4", result.Result.VoRelationshipVersion);
        Assert.True(result.Result.VoChanged);
        Assert.True(result.Result.VoCapabilities.VoInteractionUnavailable);
        Assert.True(result.Result.VoCapabilities.VoIsBlockedByCurrentUser);
        Assert.False(result.Result.VoCapabilities.VoCanInteract);
    }

    [Fact]
    public async Task BlockByUserIdAsync_ShouldRejectCrossTenantTargetWithoutMutating()
    {
        var repository = new Mock<IUserBlockRepository>(MockBehavior.Strict);
        var policyService = new Mock<IUserInteractionPolicyService>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var otherTenantTarget = new User
        {
            Id = 2002,
            TenantId = 10,
            PublicId = TargetPublicId,
            UserName = "target",
            IsEnable = true
        };
        userRepository
            .Setup(item => item.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((Expression<Func<User, bool>> predicate) =>
                predicate.Compile()(otherTenantTarget) ? otherTenantTarget : null);
        var service = CreateService(repository, policyService, userRepository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.BlockByUserIdAsync(9, 1001, 2002, "operation-1", "actor"));

        Assert.Equal(StatusCodes.Status404NotFound, exception.StatusCode);
        Assert.Equal("UserBlock.TargetUnavailable", exception.ErrorCode);
        Assert.Equal("error.user_block.target_unavailable", exception.MessageKey);
        repository.Verify(item => item.MutateAsync(It.IsAny<UserBlockMutationCommand>()), Times.Never);
    }

    [Fact]
    public async Task UnblockAsync_ShouldMapOperationKeyConflictToStableContract()
    {
        var repository = new Mock<IUserBlockRepository>(MockBehavior.Strict);
        var policyService = new Mock<IUserInteractionPolicyService>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var target = new User
        {
            Id = 2002,
            TenantId = 9,
            PublicId = TargetPublicId,
            UserName = "target",
            IsEnable = true
        };
        userRepository
            .Setup(item => item.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(target);
        repository
            .Setup(item => item.MutateAsync(It.IsAny<UserBlockMutationCommand>()))
            .ThrowsAsync(new UserBlockOperationConflictException());
        var service = CreateService(repository, policyService, userRepository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UnblockAsync(9, 1001, TargetPublicId, "reused-operation", "actor"));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("UserBlock.OperationConflict", exception.ErrorCode);
        Assert.Equal("error.user_block.operation_conflict", exception.MessageKey);
        policyService.Verify(
            item => item.GetSnapshotAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task UnblockAsync_ShouldAllowSameTenantUnavailableTargetToRemoveOwnedRelationship()
    {
        var repository = new Mock<IUserBlockRepository>(MockBehavior.Strict);
        var policyService = new Mock<IUserInteractionPolicyService>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var unavailableTarget = new User
        {
            Id = 2002,
            TenantId = 9,
            PublicId = TargetPublicId,
            UserName = "target",
            IsEnable = false,
            IsDeleted = true
        };
        userRepository
            .Setup(item => item.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((Expression<Func<User, bool>> predicate) =>
                predicate.Compile()(unavailableTarget) ? unavailableTarget : null);
        repository
            .Setup(item => item.MutateAsync(It.Is<UserBlockMutationCommand>(command =>
                command.OperationType == UserBlockOperationTypes.Unblock &&
                command.TargetUserId == 2002)))
            .ReturnsAsync(new UserBlockWriteResult(
                new UserBlock
                {
                    Id = 3003,
                    TenantId = 9,
                    BlockerUserId = 1001,
                    BlockedUserId = 2002,
                    RelationshipVersion = 2,
                    IsDeleted = true
                },
                2,
                true,
                false));
        policyService
            .Setup(item => item.GetSnapshotAsync(9, 1001, 2002))
            .ReturnsAsync(new UserInteractionPolicySnapshot(2002, false, false));
        var service = CreateService(repository, policyService, userRepository);

        var result = await service.UnblockAsync(
            9,
            1001,
            TargetPublicId,
            "unblock-unavailable-target",
            "actor");

        Assert.True(result.Result.VoChanged);
        Assert.Equal("2", result.Result.VoRelationshipVersion);
        Assert.True(result.Result.VoCapabilities.VoInteractionUnavailable);
        Assert.False(result.Result.VoCapabilities.VoCanInteract);
    }

    private static UserBlockService CreateService(
        Mock<IUserBlockRepository> repository,
        Mock<IUserInteractionPolicyService> policyService,
        Mock<IBaseRepository<User>> userRepository)
    {
        return new UserBlockService(
            repository.Object,
            policyService.Object,
            userRepository.Object,
            Mock.Of<IBaseRepository<Attachment>>(),
            Mock.Of<IAttachmentUrlResolver>(),
            TimeProvider.System);
    }
}
