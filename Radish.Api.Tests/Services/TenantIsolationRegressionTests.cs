using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radish.Common.CoreTool;
using Radish.Common.HttpContextTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Repository.Base;
using Radish.Service;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>
/// 多租户隔离回归测试
/// </summary>
public class TenantIsolationRegressionTests
{
    [Fact(DisplayName = "SearchUsersForMention 在公共租户上下文仅返回 TenantId=0 数据")]
    public async Task SearchUsersForMentionAsync_ShouldFilterPublicTenantOnly_WhenTenantIdIsZero()
    {
        EnsurePublicTenantAppContext();

        // Arrange
        var mapper = new Mock<IMapper>();
        var userRepository = new Mock<IUserRepository>();
        var baseRepository = new Mock<IBaseRepository<User>>();
        var roleRepository = new Mock<IBaseRepository<Role>>();
        var userRoleRepository = new Mock<IBaseRepository<UserRole>>();
        var departmentService = new Mock<IDepartmentService>();
        var consoleAuthorizationService = new Mock<IConsoleAuthorizationService>();

        Expression<Func<User, bool>>? capturedWhere = null;
        baseRepository
            .Setup(r => r.QueryPageAsync(
                It.IsAny<Expression<Func<User, bool>>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<User, object>>?>(),
                It.IsAny<OrderByType>()))
            .Callback<Expression<Func<User, bool>>?, int, int, Expression<Func<User, object>>?, OrderByType>(
                (where, _, _, _, _) => capturedWhere = where)
            .ReturnsAsync((new List<User>
            {
                new() { Id = 1, UserName = "alice", TenantId = 0, IsEnable = true, IsDeleted = false }
            }, 1));

        mapper.Setup(m => m.Map<List<UserVo>>(It.IsAny<List<User>>()))
            .Returns<List<User>>(users => users.Select(u => new UserVo
            {
                Uuid = u.Id,
                VoUserName = u.UserName,
                VoTenantId = u.TenantId,
                VoIsEnable = u.IsEnable,
                VoIsDeleted = u.IsDeleted
            }).ToList());

        mapper.Setup(m => m.Map<List<UserMentionVo>>(It.IsAny<List<UserVo>>()))
            .Returns<List<UserVo>>(users => users.Select(u => new UserMentionVo
            {
                VoId = u.Uuid,
                VoUserName = u.VoUserName,
                VoDisplayName = u.VoUserName
            }).ToList());

        var service = new UserService(
            departmentService.Object,
            mapper.Object,
            baseRepository.Object,
            userRepository.Object,
            roleRepository.Object,
            userRoleRepository.Object,
            consoleAuthorizationService.Object);

        // Act
        await service.SearchUsersForMentionAsync("ali", tenantId: 0, limit: 10);

        // Assert
        Assert.NotNull(capturedWhere);
        var predicate = capturedWhere!.Compile();

        Assert.True(predicate(new User { UserName = "alice", TenantId = 0, IsEnable = true, IsDeleted = false }));
        Assert.False(predicate(new User { UserName = "alice", TenantId = 2, IsEnable = true, IsDeleted = false }));
        Assert.False(predicate(new User { UserName = "alice", TenantId = 0, IsEnable = false, IsDeleted = false }));
        Assert.False(predicate(new User { UserName = "alice", TenantId = 0, IsEnable = true, IsDeleted = true }));
    }

    [Fact(DisplayName = "QueryMuch 联表表达式在公共租户上下文仅允许 TenantId=0")]
    public void QueryMuchTenantFilter_ShouldAllowOnlyPublicTenant_WhenNoTenantContext()
    {
        EnsurePublicTenantAppContext();

        // Arrange
        var method = typeof(BaseRepository<User>)
            .GetMethod("BuildTenantJoinFilterExpression", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var genericMethod = method!.MakeGenericMethod(typeof(User), typeof(Product), typeof(Order));

        // Act
        var expression = genericMethod.Invoke(null, null) as Expression<Func<User, Product, Order, bool>>;

        // Assert
        Assert.NotNull(expression);
        var predicate = expression!.Compile();

        Assert.True(predicate(
            new User { TenantId = 0 },
            new Product { TenantId = 0 },
            new Order { TenantId = 0 }));

        Assert.False(predicate(
            new User { TenantId = 2 },
            new Product { TenantId = 0 },
            new Order { TenantId = 0 }));

        Assert.False(predicate(
            new User { TenantId = 0 },
            new Product { TenantId = 2 },
            new Order { TenantId = 0 }));

        Assert.False(predicate(
            new User { TenantId = 0 },
            new Product { TenantId = 0 },
            new Order { TenantId = 2 }));
    }

    [Fact(DisplayName = "QueryMuch 联表表达式对非租户实体不追加租户过滤")]
    public void QueryMuchTenantFilter_ShouldReturnNull_WhenEntitiesAreNotTenantScoped()
    {
        EnsurePublicTenantAppContext();

        // Arrange
        var method = typeof(BaseRepository<User>)
            .GetMethod("BuildTenantJoinFilterExpression", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var genericMethod = method!.MakeGenericMethod(typeof(RoleModulePermission), typeof(ApiModule), typeof(Role));

        // Act
        var expression = genericMethod.Invoke(null, null);

        // Assert
        Assert.Null(expression);
    }

    [Fact(DisplayName = "行为类实体在公共租户上下文仅允许 TenantId=0")]
    public void BehaviorEntitiesTenantFilter_ShouldAllowOnlyPublicTenant_WhenNoTenantContext()
    {
        EnsurePublicTenantAppContext();

        AssertPublicTenantOnlyFilter<Reaction>();
        AssertPublicTenantOnlyFilter<UserPostLike>();
        AssertPublicTenantOnlyFilter<UserCommentLike>();
        AssertPublicTenantOnlyFilter<UploadSession>();
        AssertPublicTenantOnlyFilter<UserPaymentPassword>();
    }

    private static void EnsurePublicTenantAppContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        var httpContextUserMock = new Mock<IHttpContextUser>();
        httpContextUserMock.SetupGet(x => x.TenantId).Returns(0);
        services.AddSingleton(httpContextUserMock.Object);

        services.ConfigureApplication();
        var provider = services.BuildServiceProvider();
        provider.ConfigureApplication();

        App.IsBuild = true;
    }

    private static void AssertPublicTenantOnlyFilter<TEntity>() where TEntity : class, new()
    {
        var method = typeof(BaseRepository<User>)
            .GetMethod("BuildTenantFilterExpression", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var genericMethod = method!.MakeGenericMethod(typeof(TEntity));
        var expression = genericMethod.Invoke(null, null) as Expression<Func<TEntity, bool>>;

        Assert.NotNull(expression);
        var predicate = expression!.Compile();

        var publicEntity = new TEntity();
        var privateEntity = new TEntity();

        typeof(TEntity).GetProperty("TenantId")!.SetValue(publicEntity, 0L);
        typeof(TEntity).GetProperty("TenantId")!.SetValue(privateEntity, 2L);

        Assert.True(predicate(publicEntity));
        Assert.False(predicate(privateEntity));
    }
}
