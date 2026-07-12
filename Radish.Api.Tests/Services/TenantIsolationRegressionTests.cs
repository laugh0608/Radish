using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
using Radish.Repository.UnitOfWorks;
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
        var displayNameChangeRecordRepository = new Mock<IBaseRepository<UserDisplayNameChangeRecord>>();
        var consoleAuthorizationService = new Mock<IConsoleAuthorizationService>();
        var systemSettingProvider = new Mock<ISystemSettingProvider>();

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
                new()
                {
                    Id = 1,
                    UserName = "alice",
                    PublicId = "usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f",
                    PublicIndex = 1000,
                    TenantId = 0,
                    IsEnable = true,
                    IsDeleted = false
                }
            }, 1));

        mapper.Setup(m => m.Map<List<UserVo>>(It.IsAny<List<User>>()))
            .Returns<List<User>>(users => users.Select(u => new UserVo
            {
                Uuid = u.Id,
                VoPublicId = u.PublicId,
                VoPublicIndex = u.PublicIndex,
                VoUserName = u.UserName,
                VoDisplayName = u.UserName,
                VoDisplayHandle = User.BuildDisplayHandle(u.UserName, u.PublicIndex, u.Id),
                VoTenantId = u.TenantId,
                VoIsEnable = u.IsEnable,
                VoIsDeleted = u.IsDeleted
            }).ToList());

        mapper.Setup(m => m.Map<List<UserMentionVo>>(It.IsAny<List<UserVo>>()))
            .Returns<List<UserVo>>(users => users.Select(u => new UserMentionVo
            {
                VoId = u.Uuid,
                VoPublicId = u.VoPublicId,
                VoPublicIndex = u.VoPublicIndex,
                VoUserName = u.VoUserName,
                VoDisplayName = u.VoDisplayName,
                VoDisplayHandle = u.VoDisplayHandle
            }).ToList());

        var service = new UserService(
            mapper.Object,
            baseRepository.Object,
            userRepository.Object,
            roleRepository.Object,
            userRoleRepository.Object,
            displayNameChangeRecordRepository.Object,
            consoleAuthorizationService.Object,
            systemSettingProvider.Object);

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

    [Fact(DisplayName = "BaseRepository 真实查询、软删除和恢复均保持租户隔离")]
    public async Task BaseRepositorySoftDeleteAndRestore_ShouldPreserveTenantIsolation()
    {
        EnsurePublicTenantAppContext();
        var dbPath = Path.Combine(Path.GetTempPath(), $"radish-base-repository-{Guid.NewGuid():N}.db");
        var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            DbType = DbType.Sqlite,
            ConnectionString = $"Data Source={dbPath}",
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

        try
        {
            db.CodeFirst.InitTables<UserBalance>();
            db.Insertable(new[]
            {
                new UserBalance { Id = 1, UserId = 101, TenantId = 0 },
                new UserBalance { Id = 2, UserId = 202, TenantId = 2 }
            }).ExecuteCommand();
            var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
            var repository = new BaseRepository<UserBalance>(unitOfWork);

            Assert.NotNull(await repository.QueryByIdAsync(1));
            Assert.Null(await repository.QueryByIdAsync(2));

            Assert.True(await repository.SoftDeleteByIdAsync(1, "q3-c"));
            Assert.Null(await repository.QueryByIdAsync(1));
            var deleted = db.Queryable<UserBalance>().InSingle(1);
            Assert.True(deleted.IsDeleted);
            Assert.NotNull(deleted.DeletedAt);
            Assert.Equal("q3-c", deleted.DeletedBy);

            Assert.False(await repository.RestoreByIdAsync(2));
            Assert.True(await repository.RestoreByIdAsync(1));
            var restored = await repository.QueryByIdAsync(1);
            Assert.NotNull(restored);
            Assert.False(restored.IsDeleted);
            Assert.Null(restored.DeletedAt);
            Assert.Null(restored.DeletedBy);
        }
        finally
        {
            db.Dispose();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
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
