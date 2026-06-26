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
    public async Task GrantBenefitAsync_ShouldReuseConsumableOrderGrant_WhenOrderAlreadyGranted()
    {
        const long userId = 9527;
        const long productId = 7001;
        const long orderId = 8001;
        const long inventoryId = 9001;

        var product = new Product
        {
            Id = productId,
            TenantId = 0,
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.ExpCard,
            BenefitValue = "100",
            Name = "经验卡"
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

        var result = await service.GrantBenefitAsync(userId, product, orderId, 3);

        Assert.Equal(inventoryId, result);
        userInventoryRepository.VerifyAll();
        userBenefitRepository.Verify(repository => repository.AddAsync(It.IsAny<UserBenefit>()), Times.Never);
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
