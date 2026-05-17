using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
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
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        experienceService
            .Setup(service => service.GrantExperienceAsync(userId, 300, "USE_EXP_CARD", "UserInventory", inventoryId, null))
            .ReturnsAsync(true);

        var service = new UserInventoryService(
            mapper.Object,
            inventoryRepository.Object,
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
        inventoryRepository.Verify(repository => repository.UpdateAsync(It.Is<UserInventory>(inventory => inventory.Id == inventoryId && inventory.Quantity == 2)), Times.Once);
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

        var service = new UserInventoryService(
            mapper.Object,
            inventoryRepository.Object,
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
        inventoryRepository.Verify(repository => repository.UpdateAsync(It.Is<UserInventory>(inventory => inventory.Id == inventoryId && inventory.Quantity == 1)), Times.Once);
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
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var service = new UserInventoryService(
            mapper.Object,
            inventoryRepository.Object,
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
