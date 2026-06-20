using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public class UserInventoryServiceTest
{
    [Fact]
    public async Task UseItemAsync_ShouldApplyRequestedQuantityForExpCard()
    {
        const long userId = 9527;
        const long inventoryId = 2001;

        var item = new UserInventory
        {
            Id = inventoryId,
            UserId = userId,
            ConsumableType = ConsumableType.ExpCard,
            Quantity = 5,
            ItemValue = "100"
        };

        var inventoryRepository = CreateInventoryRepository(item);
        var inventoryCustomRepository = new Mock<IUserInventoryRepository>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        experienceService
            .Setup(service => service.GrantExperienceAsync(userId, 300, "USE_EXP_CARD", "UserInventory", inventoryId, null))
            .ReturnsAsync(true);
        inventoryCustomRepository
            .Setup(repository => repository.TryDeductItemAsync(userId, inventoryId, 3))
            .ReturnsAsync(new UserInventoryDeductPersistenceResult(true, 2));

        var service = new UserInventoryService(
            mapper.Object,
            inventoryRepository.Object,
            inventoryCustomRepository.Object,
            userRepository.Object,
            coinService.Object,
            attachmentUrlResolver.Object,
            experienceService.Object);

        var result = await service.UseItemAsync(userId, new UseItemDto
        {
            InventoryId = inventoryId,
            Quantity = 3
        });

        Assert.True(result.Success);
        Assert.Equal(2, result.RemainingQuantity);
        Assert.Equal("获得 300 经验值", result.EffectDescription);

        experienceService.VerifyAll();
        inventoryCustomRepository.VerifyAll();
        inventoryRepository.Verify(repository => repository.UpdateAsync(It.IsAny<UserInventory>()), Times.Never);
    }

    [Fact]
    public async Task UseItemAsync_ShouldApplyRequestedQuantityForCoinCard()
    {
        const long userId = 9527;
        const long inventoryId = 2002;

        var item = new UserInventory
        {
            Id = inventoryId,
            UserId = userId,
            ConsumableType = ConsumableType.CoinCard,
            Quantity = 4,
            ItemValue = "50"
        };

        var inventoryRepository = CreateInventoryRepository(item);
        var inventoryCustomRepository = new Mock<IUserInventoryRepository>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        coinService
            .Setup(service => service.GrantCoinAsync(
                userId,
                150,
                "USE_COIN_CARD",
                "UserInventory",
                inventoryId,
                "使用萝卜币红包获得 150 胡萝卜"))
            .ReturnsAsync("TXN_10001");
        inventoryCustomRepository
            .Setup(repository => repository.TryDeductItemAsync(userId, inventoryId, 3))
            .ReturnsAsync(new UserInventoryDeductPersistenceResult(true, 1));

        var service = new UserInventoryService(
            mapper.Object,
            inventoryRepository.Object,
            inventoryCustomRepository.Object,
            userRepository.Object,
            coinService.Object,
            attachmentUrlResolver.Object,
            experienceService.Object);

        var result = await service.UseItemAsync(userId, new UseItemDto
        {
            InventoryId = inventoryId,
            Quantity = 3
        });

        Assert.True(result.Success);
        Assert.Equal(1, result.RemainingQuantity);
        Assert.Equal("获得 150 胡萝卜", result.EffectDescription);

        coinService.VerifyAll();
        inventoryCustomRepository.VerifyAll();
        inventoryRepository.Verify(repository => repository.UpdateAsync(It.IsAny<UserInventory>()), Times.Never);
    }

    [Fact]
    public async Task UseItemAsync_ShouldRejectLotteryTicketAsUnavailable()
    {
        const long userId = 9527;
        const long inventoryId = 2099;

        var item = new UserInventory
        {
            Id = inventoryId,
            UserId = userId,
            ConsumableType = ConsumableType.LotteryTicket,
            Quantity = 1,
            ItemValue = "1"
        };

        var inventoryRepository = CreateInventoryRepository(item);
        var inventoryCustomRepository = new Mock<IUserInventoryRepository>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var service = new UserInventoryService(
            mapper.Object,
            inventoryRepository.Object,
            inventoryCustomRepository.Object,
            userRepository.Object,
            coinService.Object,
            attachmentUrlResolver.Object,
            experienceService.Object);

        var result = await service.UseItemAsync(userId, new UseItemDto
        {
            InventoryId = inventoryId,
            Quantity = 1
        });

        Assert.False(result.Success);
        Assert.Equal("抽奖券暂未开放，当前不可使用", result.ErrorMessage);

        inventoryRepository.Verify(repository => repository.UpdateAsync(It.IsAny<UserInventory>()), Times.Never);
        inventoryCustomRepository.Verify(repository => repository.TryDeductItemAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UseItemAsync_ShouldNotGrantExp_WhenConditionalDeductFails()
    {
        const long userId = 9527;
        const long inventoryId = 2003;

        var item = new UserInventory
        {
            Id = inventoryId,
            UserId = userId,
            ConsumableType = ConsumableType.ExpCard,
            Quantity = 5,
            ItemValue = "100"
        };

        var inventoryRepository = CreateInventoryRepository(item);
        var inventoryCustomRepository = new Mock<IUserInventoryRepository>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        inventoryCustomRepository
            .Setup(repository => repository.TryDeductItemAsync(userId, inventoryId, 3))
            .ReturnsAsync(new UserInventoryDeductPersistenceResult(false, 0));

        var service = new UserInventoryService(
            mapper.Object,
            inventoryRepository.Object,
            inventoryCustomRepository.Object,
            userRepository.Object,
            coinService.Object,
            attachmentUrlResolver.Object,
            experienceService.Object);

        var result = await service.UseItemAsync(userId, new UseItemDto
        {
            InventoryId = inventoryId,
            Quantity = 3
        });

        Assert.False(result.Success);
        Assert.Equal("经验卡数量不足", result.ErrorMessage);

        inventoryCustomRepository.VerifyAll();
        experienceService.Verify(
            service => service.GrantExperienceAsync(
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task UseItemAsync_ShouldThrowBusinessException_WhenExpGrantFailsAfterDeduct()
    {
        const long userId = 9527;
        const long inventoryId = 2004;

        var item = new UserInventory
        {
            Id = inventoryId,
            UserId = userId,
            ConsumableType = ConsumableType.ExpCard,
            Quantity = 5,
            ItemValue = "100"
        };

        var inventoryRepository = CreateInventoryRepository(item);
        var inventoryCustomRepository = new Mock<IUserInventoryRepository>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        inventoryCustomRepository
            .Setup(repository => repository.TryDeductItemAsync(userId, inventoryId, 2))
            .ReturnsAsync(new UserInventoryDeductPersistenceResult(true, 3));
        experienceService
            .Setup(service => service.GrantExperienceAsync(userId, 200, "USE_EXP_CARD", "UserInventory", inventoryId, null))
            .ReturnsAsync(false);

        var service = new UserInventoryService(
            mapper.Object,
            inventoryRepository.Object,
            inventoryCustomRepository.Object,
            userRepository.Object,
            coinService.Object,
            attachmentUrlResolver.Object,
            experienceService.Object);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UseItemAsync(userId, new UseItemDto
        {
            InventoryId = inventoryId,
            Quantity = 2
        }));

        Assert.Equal("经验值发放失败，请稍后再试", exception.Message);
        inventoryCustomRepository.VerifyAll();
        experienceService.VerifyAll();
    }

    private static Mock<IBaseRepository<UserInventory>> CreateInventoryRepository(UserInventory item)
    {
        var repository = new Mock<IBaseRepository<UserInventory>>(MockBehavior.Strict);
        repository
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserInventory, bool>>?>()))
            .ReturnsAsync((Expression<Func<UserInventory, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return item;
                }

                var predicate = expression.Compile();
                return predicate(item) ? item : null;
            });
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<UserInventory>()))
            .ReturnsAsync(true);
        return repository;
    }
}
