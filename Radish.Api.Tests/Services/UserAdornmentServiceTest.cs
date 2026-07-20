using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class UserAdornmentServiceTest
{
    private static readonly DateTime NowUtc = new(2026, 7, 14, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetUserAdornmentsAsync_ShouldBatchAndOnlyReturnCurrentPublicValidAdornment()
    {
        var customRepository = new Mock<IUserBenefitRepository>(MockBehavior.Strict);
        customRepository
            .Setup(repository => repository.GetActiveSelectionsAsync(
                It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<IReadOnlyCollection<BenefitType>>()))
            .ReturnsAsync([
                new UserActiveBenefit { UserId = 11, BenefitType = BenefitType.Badge, BenefitId = 101 },
                new UserActiveBenefit { UserId = 11, BenefitType = BenefitType.Title, BenefitId = 102 },
                new UserActiveBenefit { UserId = 12, BenefitType = BenefitType.Badge, BenefitId = 103 },
                new UserActiveBenefit { UserId = 13, BenefitType = BenefitType.Badge, BenefitId = 104 },
                new UserActiveBenefit { UserId = 14, BenefitType = BenefitType.Title, BenefitId = 105 }
            ]);

        var benefitRepository = new Mock<IBaseRepository<UserBenefit>>(MockBehavior.Strict);
        benefitRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<UserBenefit, bool>>?>()))
            .ReturnsAsync([
                CreateBenefit(101, 11, BenefitType.Badge, "badge-veteran", 9001),
                CreateBenefit(102, 11, BenefitType.Title, "长期贡献者", null),
                CreateBenefit(103, 12, BenefitType.Badge, "badge-expired", 9002, NowUtc),
                CreateBenefit(104, 13, BenefitType.Badge, "badge-private", 9003),
                CreateBenefit(105, 99, BenefitType.Title, "错误归属", null)
            ]);

        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        attachmentRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>?>()))
            .ReturnsAsync([
                new Attachment { Id = 9001, IsPublic = true, IsEnabled = true, IsDeleted = false }
            ]);

        var urlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        urlResolver.Setup(resolver => resolver.ResolveAttachmentUrl(9001)).Returns("/_assets/attachments/9001");

        var service = new UserAdornmentService(
            customRepository.Object,
            benefitRepository.Object,
            attachmentRepository.Object,
            urlResolver.Object,
            new FixedTimeProvider(NowUtc));

        var result = await service.GetUserAdornmentsAsync([11, 12, 13, 14]);

        var adornment = Assert.Single(result).Value;
        Assert.Equal("badge-veteran", adornment.VoBadge?.VoResourceKey);
        Assert.Equal("元老徽章", adornment.VoBadge?.VoName);
        Assert.Equal("/_assets/attachments/9001", adornment.VoBadge?.VoImageUrl);
        Assert.Equal("长期贡献者", adornment.VoTitle?.VoName);
        Assert.Null(adornment.VoTitle?.VoImageUrl);
        customRepository.Verify(repository => repository.GetActiveSelectionsAsync(
            It.IsAny<IReadOnlyCollection<long>>(),
            It.IsAny<IReadOnlyCollection<BenefitType>>()), Times.Once);
        benefitRepository.Verify(repository => repository.QueryAsync(
            It.IsAny<Expression<Func<UserBenefit, bool>>?>()), Times.Once);
        attachmentRepository.Verify(repository => repository.QueryAsync(
            It.IsAny<Expression<Func<Attachment, bool>>?>()), Times.Once);
    }

    [Fact]
    public void PublicContract_ShouldNotExposeInternalCommerceIdentifiers()
    {
        var propertyNames = typeof(Radish.Model.ViewModels.UserAdornmentVo)
            .GetProperties()
            .Concat(typeof(Radish.Model.ViewModels.UserAdornmentItemVo).GetProperties())
            .Select(property => property.Name)
            .ToList();

        Assert.DoesNotContain(propertyNames, name =>
            name.Contains("BenefitId", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Order", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Price", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Operation", StringComparison.OrdinalIgnoreCase));
    }

    private static UserBenefit CreateBenefit(
        long id,
        long userId,
        BenefitType benefitType,
        string value,
        long? iconAttachmentId,
        DateTime? expiresAt = null)
    {
        return new UserBenefit
        {
            Id = id,
            UserId = userId,
            BenefitType = benefitType,
            BenefitValue = value,
            BenefitName = benefitType == BenefitType.Badge ? "元老徽章" : value,
            BenefitIconAttachmentId = iconAttachmentId,
            EffectiveAt = NowUtc.AddDays(-1),
            ExpiresAt = expiresAt,
            CreateTime = NowUtc.AddDays(-1),
            CreateBy = "System"
        };
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(utcNow);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
