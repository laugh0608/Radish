using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public class UserInventoryServiceTest
{
    private const long UserId = 9527;
    private const long InventoryId = 2001;

    [Fact]
    public async Task UseItemAsync_ShouldDeductAndGrantExperienceOnce()
    {
        var fixture = CreateFixture(ConsumableType.ExpCard, quantity: 5, itemValue: "100");
        fixture.InventoryCustomRepository
            .Setup(repository => repository.TryDeductItemAsync(UserId, InventoryId, 3))
            .ReturnsAsync(new UserInventoryDeductPersistenceResult(true, 2));
        fixture.ExperienceService
            .Setup(service => service.GrantExperienceOnceAsync(
                UserId,
                300,
                "USE_EXP_CARD",
                It.Is<string>(key => key.StartsWith("shop-entitlement-use:")),
                OperationIdempotencyResourceTypes.ShopEntitlementOperation,
                It.IsAny<long>(),
                "使用经验卡获得 300 经验值"))
            .ReturnsAsync(ExperienceGrantOnceResult.NewGrant());

        var result = await fixture.Service.UseItemAsync(UserId, new UseItemDto
        {
            InventoryId = InventoryId,
            Quantity = 3,
            IdempotencyKey = "use-exp-1"
        });

        Assert.True(result.Success);
        Assert.True(result.OperationId > 0);
        Assert.Equal(2, result.RemainingQuantity);
        Assert.Equal(ShopEntitlementEffectTypes.Experience, result.EffectType);
        Assert.Equal("300", result.EffectValue);
        fixture.OperationRepository.Verify(repository => repository.AddAsync(
            It.Is<ShopEntitlementOperation>(operation =>
                operation.Id == result.OperationId &&
                operation.Quantity == 3 &&
                operation.EffectType == ShopEntitlementEffectTypes.Experience)), Times.Once);
        fixture.IdempotencyService.Verify(service => service.CompleteSuccessAsync(
            It.Is<OperationIdempotencyCompletionRequest>(request =>
                request.ResourceType == OperationIdempotencyResourceTypes.ShopEntitlementOperation &&
                request.ResourceId == result.OperationId)), Times.Once);
    }

    [Fact]
    public async Task UseItemAsync_ShouldDeductAndGrantCoinOnce()
    {
        var fixture = CreateFixture(ConsumableType.CoinCard, quantity: 4, itemValue: "50");
        fixture.InventoryCustomRepository
            .Setup(repository => repository.TryDeductItemAsync(UserId, InventoryId, 3))
            .ReturnsAsync(new UserInventoryDeductPersistenceResult(true, 1));
        fixture.CoinService
            .Setup(service => service.GrantCoinOnceAsync(
                UserId,
                150,
                "USE_COIN_CARD",
                It.Is<string>(key => key.StartsWith("shop-entitlement-use:")),
                OperationIdempotencyResourceTypes.ShopEntitlementOperation,
                It.IsAny<long>(),
                "使用萝卜币红包获得 150 胡萝卜"))
            .ReturnsAsync(CoinGrantOnceResult.NewGrant("TXN_10001"));

        var result = await fixture.Service.UseItemAsync(UserId, new UseItemDto
        {
            InventoryId = InventoryId,
            Quantity = 3,
            IdempotencyKey = "use-coin-1"
        });

        Assert.True(result.Success);
        Assert.Equal(1, result.RemainingQuantity);
        Assert.Equal(ShopEntitlementEffectTypes.Coin, result.EffectType);
        Assert.Equal("150", result.EffectValue);
        Assert.Equal("TXN_10001", result.EffectResourceNo);
    }

    [Fact]
    public async Task UseItemAsync_ShouldReplayPersistedSuccess_WhenInventoryWasDepleted()
    {
        var fixture = CreateFixture(ConsumableType.ExpCard, quantity: 0, itemValue: "100");
        var replay = new UseItemResultDto
        {
            Success = true,
            OperationId = 7001,
            RemainingQuantity = 0,
            EffectType = ShopEntitlementEffectTypes.Experience,
            EffectValue = "100"
        };
        fixture.OperationRepository
            .Setup(repository => repository.QueryFirstAsync(
                It.IsAny<Expression<Func<ShopEntitlementOperation, bool>>?>()))
            .ReturnsAsync(new ShopEntitlementOperation
            {
                Id = replay.OperationId!.Value,
                TenantId = 1,
                UserId = UserId,
                InventoryId = InventoryId,
                OperationType = ShopEntitlementOperationTypes.Use,
                IdempotencyKey = "use-exp-replay",
                RequestHash = "hash-a",
                ResultPayload = "persisted-result"
            });
        fixture.IdempotencyService
            .Setup(service => service.DeserializeResponse<UseItemResultDto>("persisted-result"))
            .Returns(replay);

        var result = await fixture.Service.UseItemAsync(UserId, new UseItemDto
        {
            InventoryId = InventoryId,
            Quantity = 1,
            IdempotencyKey = "use-exp-replay"
        });

        Assert.True(result.Success);
        Assert.True(result.IsIdempotentReplay);
        Assert.Equal(7001, result.OperationId);
        fixture.InventoryCustomRepository.Verify(repository => repository.TryDeductItemAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>()), Times.Never);
        fixture.IdempotencyService.Verify(service => service.BeginAsync(
            It.IsAny<OperationIdempotencyBeginRequest>()), Times.Never);
    }

    [Fact]
    public async Task UseItemAsync_ShouldRejectSameKeyWithDifferentRequest()
    {
        var fixture = CreateFixture(ConsumableType.ExpCard, quantity: 5, itemValue: "100");
        fixture.OperationRepository
            .Setup(repository => repository.QueryFirstAsync(
                It.IsAny<Expression<Func<ShopEntitlementOperation, bool>>?>()))
            .ReturnsAsync(new ShopEntitlementOperation
            {
                TenantId = 1,
                UserId = UserId,
                OperationType = ShopEntitlementOperationTypes.Use,
                IdempotencyKey = "conflict-key",
                RequestHash = "different-hash",
                ResultPayload = "persisted-result"
            });

        var result = await fixture.Service.UseItemAsync(UserId, new UseItemDto
        {
            InventoryId = InventoryId,
            Quantity = 1,
            IdempotencyKey = "conflict-key"
        });

        Assert.False(result.Success);
        Assert.Equal("幂等键已被不同请求使用", result.ErrorMessage);
        fixture.InventoryCustomRepository.Verify(repository => repository.TryDeductItemAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UseRenameCardAsync_ShouldUseUnifiedDisplayNameService()
    {
        var fixture = CreateFixture(ConsumableType.RenameCard, quantity: 1, itemValue: string.Empty);
        fixture.InventoryCustomRepository
            .Setup(repository => repository.TryDeductItemAsync(UserId, InventoryId, 1))
            .ReturnsAsync(new UserInventoryDeductPersistenceResult(true, 0));
        fixture.UserService
            .Setup(service => service.ChangeDisplayNameAsync(
                UserId,
                "新名字",
                It.Is<UserDisplayNameChangeContext>(context =>
                    context.Source == UserDisplayNameChangeSources.RenameCard &&
                    context.OperatorUserId == UserId)))
            .ReturnsAsync(true);

        var result = await fixture.Service.UseRenameCardAsync(UserId, new UseRenameCardDto
        {
            InventoryId = InventoryId,
            NewDisplayName = "新名字",
            IdempotencyKey = "rename-1"
        });

        Assert.True(result.Success);
        Assert.Equal(ShopEntitlementEffectTypes.DisplayName, result.EffectType);
        Assert.Equal("新名字", result.EffectValue);
        fixture.UserService.VerifyAll();
    }

    [Fact]
    public async Task UseItemAsync_ShouldThrow_WhenConditionalDeductLosesRace()
    {
        var fixture = CreateFixture(ConsumableType.ExpCard, quantity: 5, itemValue: "100");
        fixture.InventoryCustomRepository
            .Setup(repository => repository.TryDeductItemAsync(UserId, InventoryId, 2))
            .ReturnsAsync(new UserInventoryDeductPersistenceResult(false, 0));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.UseItemAsync(
            UserId,
            new UseItemDto
            {
                InventoryId = InventoryId,
                Quantity = 2,
                IdempotencyKey = "race-1"
            }));

        Assert.Equal("道具数量不足", exception.Message);
        fixture.ExperienceService.Verify(service => service.GrantExperienceOnceAsync(
            It.IsAny<long>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<long?>(),
            It.IsAny<string?>()), Times.Never);
        fixture.OperationRepository.Verify(repository => repository.AddAsync(
            It.IsAny<ShopEntitlementOperation>()), Times.Never);
    }

    [Fact]
    public async Task UseItemAsync_ShouldThrow_WhenExperienceGrantIsSkipped()
    {
        var fixture = CreateFixture(ConsumableType.ExpCard, quantity: 5, itemValue: "100");
        fixture.InventoryCustomRepository
            .Setup(repository => repository.TryDeductItemAsync(UserId, InventoryId, 2))
            .ReturnsAsync(new UserInventoryDeductPersistenceResult(true, 3));
        fixture.ExperienceService
            .Setup(service => service.GrantExperienceOnceAsync(
                UserId,
                200,
                "USE_EXP_CARD",
                It.IsAny<string>(),
                OperationIdempotencyResourceTypes.ShopEntitlementOperation,
                It.IsAny<long>(),
                "使用经验卡获得 200 经验值"))
            .ReturnsAsync(ExperienceGrantOnceResult.Skip("经验发放受限"));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.UseItemAsync(
            UserId,
            new UseItemDto
            {
                InventoryId = InventoryId,
                Quantity = 2,
                IdempotencyKey = "grant-failed-1"
            }));

        Assert.Equal("经验发放受限", exception.Message);
        fixture.OperationRepository.Verify(repository => repository.AddAsync(
            It.IsAny<ShopEntitlementOperation>()), Times.Never);
    }

#pragma warning disable CS0618
    [Fact]
    public async Task UseItemAsync_ShouldRejectUnavailableLotteryTicket()
    {
        var fixture = CreateFixture(ConsumableType.LotteryTicket, quantity: 1, itemValue: "1");

        var result = await fixture.Service.UseItemAsync(UserId, new UseItemDto
        {
            InventoryId = InventoryId,
            Quantity = 1,
            IdempotencyKey = "lottery-1"
        });

        Assert.False(result.Success);
        Assert.Equal("抽奖券暂未开放，当前不可使用", result.ErrorMessage);
        fixture.InventoryCustomRepository.Verify(repository => repository.TryDeductItemAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>()), Times.Never);
    }
#pragma warning restore CS0618

    [Fact]
    public async Task GetOperationsForAdminAsync_ShouldReturnReadonlyUseHistory()
    {
        var fixture = CreateFixture(ConsumableType.ExpCard, quantity: 1, itemValue: "100");
        fixture.OperationRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<ShopEntitlementOperation, bool>>?>(),
                1,
                10,
                It.IsAny<Expression<Func<ShopEntitlementOperation, object>>?>(),
                SqlSugar.OrderByType.Desc))
            .ReturnsAsync((
                new List<ShopEntitlementOperation>
                {
                    new()
                    {
                        Id = 7101,
                        TenantId = 1,
                        UserId = UserId,
                        InventoryId = InventoryId,
                        OperationType = ShopEntitlementOperationTypes.Use,
                        ConsumableType = ConsumableType.ExpCard,
                        Quantity = 2,
                        EffectType = ShopEntitlementEffectTypes.Experience,
                        EffectValue = "200",
                        CreateTime = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc)
                    }
                },
                1));

        var result = await fixture.Service.GetOperationsForAdminAsync(
            UserId,
            ConsumableType.ExpCard,
            pageIndex: 1,
            pageSize: 10);

        var operation = Assert.Single(result.Data);
        Assert.Equal(1, result.DataCount);
        Assert.Equal(7101, operation.VoId);
        Assert.Equal("经验卡", operation.VoConsumableTypeDisplay);
        Assert.Equal("200", operation.VoEffectValue);
    }

    private static TestFixture CreateFixture(
        ConsumableType consumableType,
        int quantity,
        string itemValue)
    {
        var item = new UserInventory
        {
            Id = InventoryId,
            TenantId = 1,
            UserId = UserId,
            ConsumableType = consumableType,
            Quantity = quantity,
            ItemValue = itemValue
        };
        var inventoryRepository = new Mock<IBaseRepository<UserInventory>>(MockBehavior.Loose);
        inventoryRepository
            .Setup(repository => repository.QueryFirstAsync(
                It.IsAny<Expression<Func<UserInventory, bool>>?>()))
            .ReturnsAsync((Expression<Func<UserInventory, bool>>? expression) =>
                expression == null || expression.Compile()(item) ? item : null);

        var operationRepository = new Mock<IBaseRepository<ShopEntitlementOperation>>(MockBehavior.Loose);
        operationRepository
            .Setup(repository => repository.QueryFirstAsync(
                It.IsAny<Expression<Func<ShopEntitlementOperation, bool>>?>()))
            .ReturnsAsync((ShopEntitlementOperation?)null);
        operationRepository
            .Setup(repository => repository.AddAsync(It.IsAny<ShopEntitlementOperation>()))
            .ReturnsAsync((ShopEntitlementOperation operation) => operation.Id);

        var idempotencyService = new Mock<IOperationIdempotencyService>(MockBehavior.Loose);
        idempotencyService
            .Setup(service => service.NormalizeKey(It.IsAny<string?>()))
            .Returns((string? key) => string.IsNullOrWhiteSpace(key) ? null : key.Trim());
        idempotencyService
            .Setup(service => service.CreateRequestSnapshot(
                It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Returns(new OperationIdempotencyRequestSnapshot
            {
                RequestHash = "hash-a",
                RequestSummary = "request-summary"
            });
        idempotencyService
            .Setup(service => service.BeginAsync(It.IsAny<OperationIdempotencyBeginRequest>()))
            .ReturnsAsync(new OperationIdempotencyBeginResult
            {
                Status = OperationIdempotencyBeginStatus.Started,
                RecordId = 8001
            });
        idempotencyService
            .Setup(service => service.SerializeResponse(It.IsAny<UseItemResultDto>()))
            .Returns("serialized-result");
        idempotencyService
            .Setup(service => service.CompleteSuccessAsync(
                It.IsAny<OperationIdempotencyCompletionRequest>()))
            .Returns(Task.CompletedTask);

        var mapper = new Mock<IMapper>(MockBehavior.Loose);
        var inventoryCustomRepository = new Mock<IUserInventoryRepository>(MockBehavior.Loose);
        var userService = new Mock<IUserService>(MockBehavior.Loose);
        var coinService = new Mock<ICoinService>(MockBehavior.Loose);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Loose);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Loose);
        var service = new UserInventoryService(
            mapper.Object,
            inventoryRepository.Object,
            inventoryCustomRepository.Object,
            operationRepository.Object,
            userService.Object,
            coinService.Object,
            idempotencyService.Object,
            attachmentUrlResolver.Object,
            experienceService.Object);

        return new TestFixture(
            service,
            inventoryCustomRepository,
            operationRepository,
            userService,
            coinService,
            idempotencyService,
            experienceService);
    }

    private sealed record TestFixture(
        UserInventoryService Service,
        Mock<IUserInventoryRepository> InventoryCustomRepository,
        Mock<IBaseRepository<ShopEntitlementOperation>> OperationRepository,
        Mock<IUserService> UserService,
        Mock<ICoinService> CoinService,
        Mock<IOperationIdempotencyService> IdempotencyService,
        Mock<IExperienceService> ExperienceService);
}
