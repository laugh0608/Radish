using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public class RoleGovernanceServiceTest
{
    [Theory]
    [InlineData("System")]
    [InlineData(" admin ")]
    public async Task CreateRoleAsync_ShouldRejectReservedRoleName(string roleName)
    {
        var repository = new Mock<IBaseRepository<Role>>(MockBehavior.Strict);
        var service = new RoleGovernanceService(Mock.Of<IMapper>(), repository.Object);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.CreateRoleAsync(
            CreateRequest(roleName),
            100,
            "operator"));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("Role.ReservedName", exception.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(RoleGovernanceService.SystemRoleId, "System")]
    [InlineData(RoleGovernanceService.AdminRoleId, "Admin")]
    public async Task UpdateRoleAsync_ShouldRejectBuiltInRole(long roleId, string roleName)
    {
        var repository = new Mock<IBaseRepository<Role>>(MockBehavior.Strict);
        repository
            .Setup(item => item.QueryFirstAsync(It.IsAny<Expression<Func<Role, bool>>?>()))
            .ReturnsAsync(new Role
            {
                Id = roleId,
                RoleName = roleName,
                IsEnabled = true
            });
        var service = new RoleGovernanceService(Mock.Of<IMapper>(), repository.Object);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateRoleAsync(
            roleId,
            CreateRequest("Operator"),
            100,
            "operator"));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("Role.BuiltInProtected", exception.ErrorCode);
        repository.Verify(
            item => item.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Role, Role>>>(),
                It.IsAny<Expression<Func<Role, bool>>>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateRoleAsync_ShouldUseExplicitBusinessColumnsAndPreserveIdentityFields()
    {
        var role = new Role
        {
            Id = 20001,
            RoleName = "Operator",
            RoleDescription = "Old",
            IsEnabled = true,
            CreateId = 9,
            CreateBy = "seed",
            CreateTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        Expression<Func<Role, Role>>? capturedUpdate = null;
        var repository = new Mock<IBaseRepository<Role>>(MockBehavior.Strict);
        repository
            .SetupSequence(item => item.QueryFirstAsync(It.IsAny<Expression<Func<Role, bool>>?>()))
            .ReturnsAsync(role)
            .ReturnsAsync((Role?)null);
        repository
            .Setup(item => item.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Role, Role>>>(),
                It.IsAny<Expression<Func<Role, bool>>>()))
            .Callback<Expression<Func<Role, Role>>, Expression<Func<Role, bool>>>((update, _) => capturedUpdate = update)
            .ReturnsAsync(1);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        mapper
            .Setup(item => item.Map<RoleVo>(It.IsAny<object>()))
            .Returns((object source) =>
            {
                var sourceRole = Assert.IsType<Role>(source);
                return new RoleVo
                {
                    VoId = sourceRole.Id,
                    VoRoleName = sourceRole.RoleName,
                    VoCreateId = sourceRole.CreateId,
                    VoCreateBy = sourceRole.CreateBy,
                    VoCreateTime = sourceRole.CreateTime
                };
            });
        var service = new RoleGovernanceService(mapper.Object, repository.Object);

        var result = await service.UpdateRoleAsync(
            role.Id,
            CreateRequest("Reviewer"),
            101,
            "admin");

        Assert.Equal("Reviewer", result.VoRoleName);
        Assert.Equal(9, result.VoCreateId);
        Assert.Equal("seed", result.VoCreateBy);
        Assert.NotNull(capturedUpdate);
        var memberNames = Assert.IsType<MemberInitExpression>(capturedUpdate!.Body)
            .Bindings
            .Select(binding => binding.Member.Name)
            .ToList();
        Assert.DoesNotContain(nameof(Role.Id), memberNames);
        Assert.DoesNotContain(nameof(Role.CreateId), memberNames);
        Assert.DoesNotContain(nameof(Role.CreateBy), memberNames);
        Assert.DoesNotContain(nameof(Role.CreateTime), memberNames);
        Assert.DoesNotContain(nameof(Role.IsDeleted), memberNames);
    }

    private static RoleMutationDto CreateRequest(string roleName)
    {
        return new RoleMutationDto
        {
            VoRoleName = roleName,
            VoRoleDescription = "Role",
            VoAuthorityScope = -1,
            VoIsEnabled = true
        };
    }
}
