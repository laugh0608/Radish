using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Common.CoreTool;
using Radish.Api.Tests.TestCollections;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
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

    [Fact]
    public async Task DisableAccount_ProtectsTargetsAndCommitsOneRecoverableAudit()
    {
        new ServiceCollection().ConfigureApplication();
        var path = Path.Combine(Path.GetTempPath(), $"radish-account-disable-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);
        try
        {
            using var otherConnection = CreateClient(path);
            await CheckDisableAsync(db, CreateRepository(otherConnection));
        }
        finally
        {
            db.Close();
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public Task PostgreSql_DisableAccountProtectsTargetsAndCommitsOneRecoverableAudit() =>
        TemporaryPostgreSqlDatabase.RunAsync(async cs =>
        {
            new ServiceCollection().ConfigureApplication();
            SqlSugarScope OpenConnection() => PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
            {
                ConfigId = "main", ConnectionString = cs, DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
            });
            using var db = OpenConnection();
            using var otherConnection = OpenConnection();
            await CheckDisableAsync(db, CreateRepository(otherConnection));
        });

    private static async Task CheckDisableAsync(SqlSugarScope db, UserRepository otherRequest)
    {
        db.CodeFirst.InitTables<User, Role, UserRole, ReliableOutboxMessage>();
        var ct = TestContext.Current.CancellationToken;
        var user = CreateUser(101, "Target", true);
        var protectedUser = CreateUser(102, "Operator", true);
        await db.Insertable(new[] { user, protectedUser, CreateUser(103, "Historical", false) }).ExecuteCommandAsync(ct);
        await db.Insertable(new Role { Id = 505, RoleName = "Admin", IsEnabled = true }).ExecuteCommandAsync(ct);
        await db.Insertable(new UserRole { Id = 506, UserId = 102, RoleId = 505 }).ExecuteCommandAsync(ct);
        var repo = CreateRepository(db);
        var now = DateTime.UtcNow;
        foreach (var (tenant, target, actor, expected) in new[] { (1, 101, 104, 404), (0, 101, 101, 403), (0, 102, 104, 403) })
        {
            var error = await Assert.ThrowsAsync<Radish.Common.Exceptions.BusinessException>(() =>
                repo.DisableAccountAsync(tenant, target, actor, "reason", now));
            Assert.Equal(expected, error.StatusCode);
        }
        Assert.Equal(0, await db.Queryable<ReliableOutboxMessage>().CountAsync(ct));

        var sqlite = db.CurrentConnectionConfig.DbType == DbType.Sqlite;
        await db.Ado.ExecuteCommandAsync(sqlite
            ? "CREATE TRIGGER abort_audit BEFORE INSERT ON ReliableOutboxMessage BEGIN SELECT RAISE(ABORT, 'fixture failure'); END"
            : "ALTER TABLE reliableoutboxmessage ADD CONSTRAINT abort_audit CHECK (tasktype <> 'AccountDisabled')");
        await Assert.ThrowsAnyAsync<Exception>(() => repo.DisableAccountAsync(0, 101, 104, "reason", now));
        Assert.True((await db.Queryable<User>().InSingleAsync(101)).IsEnable);
        await db.Ado.ExecuteCommandAsync(sqlite ? "DROP TRIGGER abort_audit"
            : "ALTER TABLE reliableoutboxmessage DROP CONSTRAINT abort_audit");

        var commands = await Task.WhenAll(repo.DisableAccountAsync(0, 101, 104, "first", now),
            otherRequest.DisableAccountAsync(0, 101, 104, "second", now));
        Assert.Single(commands, result => result.Created);
        Assert.Equal(commands[0].Task.Id, commands[1].Task.Id);
        Assert.False((await db.Queryable<User>().InSingleAsync(101)).IsEnable);
        var task = Assert.Single(await db.Queryable<ReliableOutboxMessage>().ToListAsync(ct));
        Assert.Equal(ReliableOutboxStatuses.Processing, task.Status);
        Assert.Contains("104", task.PayloadJson);
        Assert.True((await repo.DisableAccountAsync(0, 103, 104, "legacy", now)).Created);

        // Simulate loss of the initial worker, then redelivery after an unacknowledged successful revocation.
        var outbox = new ReliableOutboxRepository(db);
        var claimed = await outbox.ClaimDueAsync("Main", 10, "replacement", now.AddMinutes(6), TimeSpan.FromMinutes(5));
        Assert.Contains(claimed, item => item.Id == task.Id);
        await outbox.MarkSucceededAsync("Main", task.Id, now.AddMinutes(6));
        Assert.Equal(ReliableOutboxStatuses.Succeeded, (await repo.FindDisableTaskAsync(0, 101))!.Status);
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
