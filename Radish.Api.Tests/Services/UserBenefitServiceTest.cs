using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public class UserBenefitServiceTest
{
    [Fact]
    public async Task ActivateBenefitAsync_ShouldRejectUnsupportedBadgeBenefit()
    {
        const long userId = 9527;
        const long benefitId = 6001;

        var benefit = new UserBenefit
        {
            Id = benefitId,
            UserId = userId,
            BenefitType = BenefitType.Badge,
            BenefitValue = "badge-veteran",
            IsActive = false,
            IsExpired = false,
            IsDeleted = false,
            CreateTime = DateTime.Now
        };

        var userBenefitRepository = CreateUserBenefitRepository(benefit);
        var userInventoryRepository = new Mock<IUserInventoryRepository>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var service = new UserBenefitService(
            mapper.Object,
            userBenefitRepository.Object,
            userInventoryRepository.Object,
            attachmentUrlResolver.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ActivateBenefitAsync(userId, benefitId));

        Assert.Equal("徽章暂未开放，当前不可激活", exception.Message);
        userBenefitRepository.Verify(repository => repository.UpdateAsync(It.IsAny<UserBenefit>()), Times.Never);
        userBenefitRepository.Verify(repository => repository.QueryAsync(It.IsAny<Expression<Func<UserBenefit, bool>>?>()), Times.Never);
    }

    [Fact]
    public async Task GrantOrderFulfillmentAsync_ShouldReuseConsumableOrderGrant_WhenOrderAlreadyGranted()
    {
        const long userId = 9527;
        const long productId = 7001;
        const long orderId = 8001;
        const long inventoryId = 9001;

        var order = new Order
        {
            Id = orderId,
            UserId = userId,
            ProductId = productId,
            TenantId = 0,
            Status = OrderStatus.Paid,
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.ExpCard,
            BenefitValue = "100",
            ProductName = "经验卡",
            Quantity = 3,
            CreateTime = DateTime.Now,
            CreateBy = "User"
        };

        var userBenefitRepository = new Mock<IBaseRepository<UserBenefit>>(MockBehavior.Strict);
        var userInventoryRepository = new Mock<IUserInventoryRepository>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        userInventoryRepository
            .Setup(repository => repository.GrantConsumableForOrderAsync(
                0,
                userId,
                ConsumableType.ExpCard,
                "100",
                "经验卡",
                null,
                3,
                orderId,
                productId))
            .ReturnsAsync(new UserInventoryGrantPersistenceResult(inventoryId, false, 0, 5));

        var service = new UserBenefitService(
            mapper.Object,
            userBenefitRepository.Object,
            userInventoryRepository.Object,
            attachmentUrlResolver.Object);

        var result = await service.GrantOrderFulfillmentAsync(order);

        Assert.Equal(inventoryId, result.GrantedInventoryId);
        Assert.Null(result.GrantedBenefitId);
        userInventoryRepository.VerifyAll();
        userBenefitRepository.Verify(repository => repository.AddAsync(It.IsAny<UserBenefit>()), Times.Never);
    }

    [Fact]
    public async Task GrantOrderFulfillmentAsync_ShouldUseFixedExpiryFromOrderSnapshot()
    {
        var fixedExpiresAt = DateTime.UtcNow.AddDays(30);
        var order = new Order
        {
            Id = 8101,
            TenantId = 3,
            UserId = 9527,
            ProductId = 7101,
            ProductName = "订单快照称号",
            ProductType = ProductType.Benefit,
            BenefitType = BenefitType.Title,
            BenefitValue = "title-snapshot",
            DurationType = DurationType.FixedDate,
            FixedExpiresAt = fixedExpiresAt,
            Quantity = 1,
            Status = OrderStatus.Paid,
            CreateTime = DateTime.UtcNow,
            CreateBy = "User"
        };
        UserBenefit? createdBenefit = null;
        var userBenefitRepository = new Mock<IBaseRepository<UserBenefit>>(MockBehavior.Strict);
        userBenefitRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<UserBenefit, bool>>?>()))
            .ReturnsAsync((UserBenefit?)null);
        userBenefitRepository
            .Setup(repository => repository.AddAsync(It.IsAny<UserBenefit>()))
            .Callback<UserBenefit>(benefit => createdBenefit = benefit)
            .ReturnsAsync(9101);
        var service = new UserBenefitService(
            Mock.Of<IMapper>(),
            userBenefitRepository.Object,
            Mock.Of<IUserInventoryRepository>(),
            Mock.Of<IAttachmentUrlResolver>());

        var result = await service.GrantOrderFulfillmentAsync(order);

        Assert.Equal(9101, result.GrantedBenefitId);
        Assert.Equal(fixedExpiresAt, result.ExpiresAt);
        Assert.NotNull(createdBenefit);
        Assert.Equal(fixedExpiresAt, createdBenefit!.ExpiresAt);
        Assert.Equal(order.ProductName, createdBenefit.BenefitName);
        Assert.Equal(order.ProductId, createdBenefit.SourceProductId);
    }

    private static Mock<IBaseRepository<UserBenefit>> CreateUserBenefitRepository(UserBenefit benefit)
    {
        var repository = new Mock<IBaseRepository<UserBenefit>>(MockBehavior.Strict);
        repository
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBenefit, bool>>?>()))
            .ReturnsAsync((Expression<Func<UserBenefit, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return benefit;
                }

                var predicate = expression.Compile();
                return predicate(benefit) ? benefit : null;
            });
        repository
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserBenefit, bool>>?>()))
            .ReturnsAsync(new List<UserBenefit>());
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<UserBenefit>()))
            .ReturnsAsync(true);
        return repository;
    }
}
