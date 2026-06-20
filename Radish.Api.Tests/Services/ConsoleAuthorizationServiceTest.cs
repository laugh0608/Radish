using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
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
                IsEnabled = true
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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveRoleAuthorizationAsync(
            new SaveRoleAuthorizationDto
            {
                RoleId = 10,
                ResourceIds = [1],
                ExpectedModifyTime = currentModifyTime.AddMinutes(-1)
            },
            10001,
            "tester"));

        Assert.Equal("角色授权已被其他管理员修改，请刷新后重试", exception.Message);
        roleConsoleResourceRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<RoleConsoleResource, RoleConsoleResource>>>(),
                It.IsAny<Expression<Func<RoleConsoleResource, bool>>>()),
            Times.Never);
        roleConsoleResourceRepository.Verify(
            repository => repository.AddRangeAsync(It.IsAny<List<RoleConsoleResource>>()),
            Times.Never);
    }
}
