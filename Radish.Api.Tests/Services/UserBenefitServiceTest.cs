using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.Extension.AutoMapperExtension.CustomProfiles;
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
    public async Task GetUserBenefitsAsync_ShouldUseServerUtcAndUniqueSelectionAsStatusAuthority()
    {
        var nowUtc = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);
        var benefits = new List<UserBenefit>
        {
            CreateBenefit(6101, BenefitType.Title, null),
            CreateBenefit(6102, BenefitType.Badge, null),
            CreateBenefit(6103, BenefitType.Theme, nowUtc),
            CreateBenefit(6104, BenefitType.AvatarFrame, null, nowUtc.AddMinutes(-1))
        };
        var repository = new Mock<IBaseRepository<UserBenefit>>(MockBehavior.Strict);
        repository
            .Setup(item => item.QueryAsync(It.IsAny<Expression<Func<UserBenefit, bool>>?>()))
            .ReturnsAsync(benefits);
        var customRepository = new Mock<IUserBenefitRepository>(MockBehavior.Strict);
        customRepository
            .Setup(item => item.GetActiveSelectionsAsync(9527))
            .ReturnsAsync([
                new UserActiveBenefit { UserId = 9527, BenefitType = BenefitType.Badge, BenefitId = 6102 },
                new UserActiveBenefit { UserId = 9527, BenefitType = BenefitType.Theme, BenefitId = 6103 }
            ]);
        var mapperConfiguration = new MapperConfiguration(
            config => config.AddProfile<ShopProfile>(),
            NullLoggerFactory.Instance);
        var service = new UserBenefitService(
            mapperConfiguration.CreateMapper(),
            repository.Object,
            Mock.Of<IBaseRepository<ShopEntitlementOperation>>(),
            customRepository.Object,
            Mock.Of<IUserInventoryRepository>(),
            Mock.Of<IAttachmentUrlResolver>(),
            timeProvider: new FixedTimeProvider(nowUtc));

        var result = await service.GetUserBenefitsAsync(9527, includeExpired: true);

        Assert.Equal(UserBenefitStatus.Available, result.Single(item => item.VoId == 6101).VoStatus);
        Assert.Equal(UserBenefitStatus.Active, result.Single(item => item.VoId == 6102).VoStatus);
        var expired = result.Single(item => item.VoId == 6103);
        Assert.Equal(UserBenefitStatus.Expired, expired.VoStatus);
        Assert.False(expired.VoIsActive);
        Assert.Equal(UserBenefitStatus.Revoked, result.Single(item => item.VoId == 6104).VoStatus);
        Assert.All(result, item => Assert.False(item.VoCanActivate));
        Assert.True(result.Single(item => item.VoId == 6102).VoCanDeactivate);

        var usable = await service.GetUserBenefitsAsync(9527);
        Assert.Equal([6101L, 6102L], usable.Select(item => item.VoId).OrderBy(id => id).ToArray());

        var active = await service.GetActiveBenefitsAsync(9527);
        Assert.Equal(6102, Assert.Single(active).VoId);
    }

    [Fact]
    public async Task GetOperationsForAdminAsync_ShouldReturnUnifiedBenefitAndConsumableHistory()
    {
        var operationRepository = new Mock<IBaseRepository<ShopEntitlementOperation>>(MockBehavior.Strict);
        operationRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<ShopEntitlementOperation, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<ShopEntitlementOperation, object>>?>(),
                SqlSugar.OrderByType.Desc))
            .ReturnsAsync((
                new List<ShopEntitlementOperation>
                {
                    new()
                    {
                        Id = 7201,
                        UserId = 9527,
                        BenefitId = 6102,
                        OperationType = "Revoke",
                        BenefitType = BenefitType.Badge,
                        Reason = "治理撤销",
                        EffectType = "BenefitRevocation",
                        EffectResourceType = "UserBenefit",
                        EffectResourceId = 6102,
                        CreateTime = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc),
                        CreateBy = "Admin"
                    },
                    new()
                    {
                        Id = 7202,
                        UserId = 9527,
                        InventoryId = 7001,
                        OperationType = "Use",
                        ConsumableType = ConsumableType.ExpCard,
                        Quantity = 1,
                        EffectType = "ExperienceGrant",
                        EffectValue = "100",
                        CreateTime = new DateTime(2026, 7, 13, 7, 0, 0, DateTimeKind.Utc),
                        CreateBy = "User"
                    }
                },
                2));
        var service = new UserBenefitService(
            Mock.Of<IMapper>(),
            Mock.Of<IBaseRepository<UserBenefit>>(),
            operationRepository.Object,
            Mock.Of<IUserBenefitRepository>(),
            Mock.Of<IUserInventoryRepository>(),
            Mock.Of<IAttachmentUrlResolver>());

        var result = await service.GetOperationsForAdminAsync(9527);

        Assert.Equal(2, result.DataCount);
        Assert.Equal("治理撤销", result.Data[0].VoReason);
        Assert.Equal(BenefitType.Badge, result.Data[0].VoBenefitType);
        Assert.Equal(ConsumableType.ExpCard, result.Data[1].VoConsumableType);
        operationRepository.VerifyAll();
    }

    [Fact]
    public async Task ExpireBenefitAsync_ShouldRequestReliableNotificationOnlyForChangedFact()
    {
        var nowUtc = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);
        var benefit = CreateBenefit(6301, BenefitType.Title, nowUtc);
        benefit.TenantId = 7;
        benefit.BenefitName = "限时称号";
        var customRepository = new Mock<IUserBenefitRepository>(MockBehavior.Strict);
        customRepository
            .SetupSequence(repository => repository.ExpireAsync(6301, nowUtc))
            .ReturnsAsync(new UserBenefitPersistenceResult(benefit, true, null, benefit.Id))
            .ReturnsAsync((UserBenefitPersistenceResult?)null);
        var outboxService = new Mock<IReliableOutboxService>(MockBehavior.Strict);
        outboxService
            .Setup(service => service.AddAsync(
                ReliableOutboxSources.Main,
                7,
                ReliableTaskTypes.NotificationRequested,
                "task:notification:benefit-expired:6301",
                "UserBenefit",
                "6301",
                It.Is<NotificationRequestedTaskPayload>(payload =>
                    payload.Notification.BusinessKey == "notification:benefit-expired:6301" &&
                    payload.Notification.ReceiverUserIds.SequenceEqual(new[] { 9527L })),
                nowUtc,
                1))
            .ReturnsAsync(7301);
        var service = new UserBenefitService(
            Mock.Of<IMapper>(),
            Mock.Of<IBaseRepository<UserBenefit>>(),
            Mock.Of<IBaseRepository<ShopEntitlementOperation>>(),
            customRepository.Object,
            Mock.Of<IUserInventoryRepository>(),
            Mock.Of<IAttachmentUrlResolver>(),
            outboxService.Object,
            new FixedTimeProvider(nowUtc));

        Assert.True(await service.ExpireBenefitAsync(6301));
        Assert.False(await service.ExpireBenefitAsync(6301));

        customRepository.VerifyAll();
        outboxService.VerifyAll();
    }

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
            Mock.Of<IBaseRepository<ShopEntitlementOperation>>(),
            Mock.Of<IUserBenefitRepository>(),
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
            Mock.Of<IBaseRepository<ShopEntitlementOperation>>(),
            Mock.Of<IUserBenefitRepository>(),
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
            Mock.Of<IBaseRepository<ShopEntitlementOperation>>(),
            Mock.Of<IUserBenefitRepository>(),
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

    private static UserBenefit CreateBenefit(
        long id,
        BenefitType benefitType,
        DateTime? expiresAt,
        DateTime? revokedAt = null)
    {
        return new UserBenefit
        {
            Id = id,
            UserId = 9527,
            BenefitType = benefitType,
            BenefitValue = $"benefit-{id}",
            EffectiveAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
            RevocationReason = revokedAt.HasValue ? "管理员撤销" : null,
            CreateTime = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            CreateBy = "System"
        };
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(utcNow);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
