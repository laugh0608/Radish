using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.Common.Exceptions;
using Radish.Common.PermissionTool;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ConsoleAuthorizationServiceTest
{
    [Fact]
    public async Task SaveRoleAuthorizationAsync_ShouldRejectStaleExpectedModifyTimeBeforeWriting()
    {
        var roleRepository = new Mock<IBaseRepository<Role>>(MockBehavior.Strict);
        var consoleResourceRepository = new Mock<IBaseRepository<ConsoleResource>>(MockBehavior.Strict);
        var roleConsoleResourceRepository = new Mock<IBaseRepository<RoleConsoleResource>>(MockBehavior.Strict);
        var consoleResourceApiModuleRepository = new Mock<IBaseRepository<ConsoleResourceApiModule>>(MockBehavior.Strict);
        var roleModulePermissionRepository = new Mock<IBaseRepository<RoleModulePermission>>(MockBehavior.Strict);
        var apiModuleRepository = new Mock<IBaseRepository<ApiModule>>(MockBehavior.Strict);
        var currentModifyTime = new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

        roleRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Role, bool>>?>()))
            .ReturnsAsync(new Role
            {
                Id = 10,
                RoleName = "Operator",
                IsEnabled = true,
                ModifyTime = currentModifyTime
            });
        consoleResourceRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<ConsoleResource, bool>>?>()))
            .ReturnsAsync(new List<ConsoleResource>
            {
                new()
                {
                    Id = 1,
                    ResourceKey = ConsolePermissions.Access,
                    ResourceName = "Console Access",
                    IsEnabled = true
                }
            });
        roleConsoleResourceRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<RoleConsoleResource, bool>>?>()))
            .ReturnsAsync(new List<RoleConsoleResource>
            {
                new()
                {
                    RoleId = 10,
                    ConsoleResourceId = 1,
                    IsDeleted = false,
                    CreateTime = currentModifyTime.AddMinutes(-5),
                    ModifyTime = currentModifyTime
                }
            });

        var service = new ConsoleAuthorizationService(
            Mock.Of<IMapper>(),
            roleRepository.Object,
            consoleResourceRepository.Object,
            roleConsoleResourceRepository.Object,
            consoleResourceApiModuleRepository.Object,
            roleModulePermissionRepository.Object,
            apiModuleRepository.Object);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SaveRoleAuthorizationAsync(
            new SaveRoleAuthorizationDto
            {
                RoleId = 10,
                ResourceIds = [1],
                ExpectedModifyTime = currentModifyTime.AddMinutes(-1)
            },
            10001,
            "tester"));

        Assert.Equal("角色授权已被其他管理员修改，请刷新后重试", exception.Message);
        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("RoleAuthorization.VersionConflict", exception.ErrorCode);
        roleConsoleResourceRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<RoleConsoleResource, RoleConsoleResource>>>(),
                It.IsAny<Expression<Func<RoleConsoleResource, bool>>>()),
            Times.Never);
        roleConsoleResourceRepository.Verify(
            repository => repository.AddRangeAsync(It.IsAny<List<RoleConsoleResource>>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveRoleAuthorizationAsync_ShouldRejectBuiltInRoleBeforeWriting()
    {
        var roleRepository = new Mock<IBaseRepository<Role>>(MockBehavior.Strict);
        roleRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Role, bool>>?>()))
            .ReturnsAsync(new Role
            {
                Id = RoleGovernanceService.SystemRoleId,
                RoleName = "System",
                IsEnabled = true
            });
        var service = CreateService(roleRepository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SaveRoleAuthorizationAsync(
            new SaveRoleAuthorizationDto
            {
                RoleId = RoleGovernanceService.SystemRoleId,
                ResourceIds = []
            },
            10001,
            "tester"));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("RoleAuthorization.BuiltInProtected", exception.ErrorCode);
        roleRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Role, Role>>>(),
                It.IsAny<Expression<Func<Role, bool>>>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveRoleAuthorizationAsync_ShouldAdvanceRoleVersionWhenClearingAllResources()
    {
        var currentVersion = new DateTime(2026, 8, 9, 8, 0, 0, DateTimeKind.Utc);
        var roleRepository = new Mock<IBaseRepository<Role>>(MockBehavior.Strict);
        roleRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Role, bool>>?>()))
            .ReturnsAsync(new Role
            {
                Id = 20,
                RoleName = "Operator",
                IsEnabled = true,
                ModifyTime = currentVersion
            });
        roleRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Role, Role>>>(),
                It.IsAny<Expression<Func<Role, bool>>>()))
            .ReturnsAsync(1);
        var resourceRepository = new Mock<IBaseRepository<ConsoleResource>>(MockBehavior.Strict);
        resourceRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<ConsoleResource, bool>>?>()))
            .ReturnsAsync(new List<ConsoleResource>
            {
                new() { Id = 1, ResourceKey = "console.roles.view", ResourceName = "Roles", IsEnabled = true }
            });
        var linkRepository = new Mock<IBaseRepository<RoleConsoleResource>>(MockBehavior.Strict);
        linkRepository
            .SetupSequence(repository => repository.QueryAsync(It.IsAny<Expression<Func<RoleConsoleResource, bool>>?>()))
            .ReturnsAsync(new List<RoleConsoleResource>
            {
                new() { Id = 30, RoleId = 20, ConsoleResourceId = 1 }
            })
            .ReturnsAsync([]);
        linkRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<RoleConsoleResource, RoleConsoleResource>>>(),
                It.IsAny<Expression<Func<RoleConsoleResource, bool>>>()))
            .ReturnsAsync(1);
        var service = CreateService(roleRepository, resourceRepository, linkRepository);

        var saved = await service.SaveRoleAuthorizationAsync(
            new SaveRoleAuthorizationDto
            {
                RoleId = 20,
                ResourceIds = [],
                ExpectedModifyTime = currentVersion
            },
            10001,
            "tester");

        Assert.True(saved);
        roleRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Role, Role>>>(),
                It.IsAny<Expression<Func<Role, bool>>>()),
            Times.Once);
        linkRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<RoleConsoleResource, RoleConsoleResource>>>(),
                It.IsAny<Expression<Func<RoleConsoleResource, bool>>>()),
            Times.Once);
        linkRepository.Verify(repository => repository.AddRangeAsync(It.IsAny<List<RoleConsoleResource>>()), Times.Never);
    }

    [Fact]
    public async Task SaveRoleAuthorizationAsync_ShouldReturnConflictWhenRoleVersionCasMisses()
    {
        var currentVersion = new DateTime(2026, 8, 9, 9, 0, 0, DateTimeKind.Utc);
        var roleRepository = new Mock<IBaseRepository<Role>>(MockBehavior.Strict);
        roleRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Role, bool>>?>()))
            .ReturnsAsync(new Role
            {
                Id = 21,
                RoleName = "Reviewer",
                IsEnabled = true,
                ModifyTime = currentVersion
            });
        roleRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Role, Role>>>(),
                It.IsAny<Expression<Func<Role, bool>>>()))
            .ReturnsAsync(0);
        var resourceRepository = new Mock<IBaseRepository<ConsoleResource>>(MockBehavior.Strict);
        resourceRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<ConsoleResource, bool>>?>()))
            .ReturnsAsync([]);
        var linkRepository = new Mock<IBaseRepository<RoleConsoleResource>>(MockBehavior.Strict);
        var service = CreateService(roleRepository, resourceRepository, linkRepository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SaveRoleAuthorizationAsync(
            new SaveRoleAuthorizationDto
            {
                RoleId = 21,
                ResourceIds = [],
                ExpectedModifyTime = currentVersion
            },
            10001,
            "tester"));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("RoleAuthorization.VersionConflict", exception.ErrorCode);
        linkRepository.Verify(
            repository => repository.QueryAsync(It.IsAny<Expression<Func<RoleConsoleResource, bool>>?>()),
            Times.Never);
    }

    private static ConsoleAuthorizationService CreateService(
        Mock<IBaseRepository<Role>> roleRepository,
        Mock<IBaseRepository<ConsoleResource>>? resourceRepository = null,
        Mock<IBaseRepository<RoleConsoleResource>>? linkRepository = null)
    {
        return new ConsoleAuthorizationService(
            Mock.Of<IMapper>(),
            roleRepository.Object,
            (resourceRepository ?? new Mock<IBaseRepository<ConsoleResource>>()).Object,
            (linkRepository ?? new Mock<IBaseRepository<RoleConsoleResource>>()).Object,
            Mock.Of<IBaseRepository<ConsoleResourceApiModule>>(),
            Mock.Of<IBaseRepository<RoleModulePermission>>(),
            Mock.Of<IBaseRepository<ApiModule>>());
    }
}
