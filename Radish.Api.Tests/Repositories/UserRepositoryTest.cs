using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Common.CoreTool;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class UserRepositoryTest
{
    [Fact]
    public async Task QueryConsolePageAsync_ShouldApplyEnabledRoleAndStablePaging()
    {
        new ServiceCollection().ConfigureApplication();
        var path = Path.Combine(Path.GetTempPath(), $"radish-console-users-{Guid.NewGuid():N}.db");
        using var database = CreateClient(path);
        try
        {
            database.CodeFirst.InitTables<User, Role, UserRole>();
            await database.Insertable(new[]
            {
                CreateUser(101, "Alpha", true),
                CreateUser(102, "Beta", false),
                CreateUser(103, "Gamma", true),
                CreateUser(104, "Deleted", true, isDeleted: true),
            }).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            await database.Insertable(new[]
            {
                new Role { Id = 201, RoleName = "Admin", IsEnabled = true },
                new Role { Id = 202, RoleName = "User", IsEnabled = true },
            }).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            await database.Insertable(new[]
            {
                new UserRole { Id = 301, UserId = 101, RoleId = 201 },
                new UserRole { Id = 302, UserId = 102, RoleId = 201 },
                new UserRole { Id = 303, UserId = 103, RoleId = 202 },
            }).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            var repository = CreateRepository(database);

            var firstPage = await repository.QueryConsolePageAsync(new ConsoleUserPageQuery(
                PageIndex: 1,
                PageSize: 1,
                Keyword: string.Empty,
                IsEnabled: true,
                RoleName: "ADMIN"));

            Assert.Equal(1, firstPage.Total);
            Assert.Equal(101, Assert.Single(firstPage.Items).Id);

            var stableFirstPage = await repository.QueryConsolePageAsync(new ConsoleUserPageQuery(
                PageIndex: 1,
                PageSize: 1,
                Keyword: string.Empty,
                IsEnabled: true,
                RoleName: string.Empty));
            var stableSecondPage = await repository.QueryConsolePageAsync(new ConsoleUserPageQuery(
                PageIndex: 2,
                PageSize: 1,
                Keyword: string.Empty,
                IsEnabled: true,
                RoleName: string.Empty));
            var keywordResult = await repository.QueryConsolePageAsync(new ConsoleUserPageQuery(
                PageIndex: 1,
                PageSize: 10,
                Keyword: "ALPHA@RADISH",
                IsEnabled: null,
                RoleName: string.Empty));

            Assert.Equal(2, stableFirstPage.Total);
            Assert.Equal(103, Assert.Single(stableFirstPage.Items).Id);
            Assert.Equal(101, Assert.Single(stableSecondPage.Items).Id);
            Assert.Equal(101, Assert.Single(keywordResult.Items).Id);
        }
        finally
        {
            database.Close();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GetRoleNamesByUserIdsAsync_ShouldReturnEnabledRolesWithoutNPlusOne()
    {
        new ServiceCollection().ConfigureApplication();
        var path = Path.Combine(Path.GetTempPath(), $"radish-console-user-roles-{Guid.NewGuid():N}.db");
        using var database = CreateClient(path);
        try
        {
            database.CodeFirst.InitTables<User, Role, UserRole>();
            await database.Insertable(new[]
            {
                new Role { Id = 201, RoleName = "Admin", IsEnabled = true },
                new Role { Id = 202, RoleName = "DisabledRole", IsEnabled = false },
            }).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            await database.Insertable(new[]
            {
                new UserRole { Id = 301, UserId = 101, RoleId = 201 },
                new UserRole { Id = 302, UserId = 101, RoleId = 202 },
            }).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            var repository = CreateRepository(database);

            var result = await repository.GetRoleNamesByUserIdsAsync([101, 102]);

            Assert.Equal(["Admin"], result[101]);
            Assert.False(result.ContainsKey(102));
        }
        finally
        {
            database.Close();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static User CreateUser(long id, string name, bool isEnabled, bool isDeleted = false)
    {
        return new User
        {
            Id = id,
            UserName = name,
            UserEmail = $"{name.ToLowerInvariant()}@radish.test",
            LoginPassword = "hash",
            TenantId = 0,
            IsEnable = isEnabled,
            IsDeleted = isDeleted,
        };
    }

    private static UserRepository CreateRepository(SqlSugarScope database)
    {
        return new UserRepository(new UnitOfWorkManage(database, NullLogger<UnitOfWorkManage>.Instance));
    }

    private static SqlSugarScope CreateClient(string path)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        });
    }
}
