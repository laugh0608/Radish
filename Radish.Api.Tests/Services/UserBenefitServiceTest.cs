using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
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
        var userInventoryRepository = new Mock<IBaseRepository<UserInventory>>(MockBehavior.Strict);
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
