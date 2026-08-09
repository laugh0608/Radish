using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ConsoleRoleGovernanceSchemaMigrationTest
{
    [Fact]
    public void Migration_ShouldRemainRepeatableAndEnforceAggregateUniquenessOnSqlite()
    {
        var path = CreatePath("unique");
        using var db = CreateSqliteClient(path);
        using var services = new ServiceCollection()
            .AddSingleton<ISqlSugarClient>(db)
            .BuildServiceProvider();
        try
        {
            var migration = ConsoleRoleGovernanceSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Contains(
                SchemaMigrationRegistry.All,
                item => item.MigrationId == "20260809_019_console_role_governance" &&
                        item.Scope == "Main");

            db.Insertable(CreateRole(20001, "Reviewer")).ExecuteCommand();
            var duplicateRole = CreateRole(20002, "reviewer");
            duplicateRole.RoleName = " reviewer ";
            Assert.ThrowsAny<Exception>(() => db.Insertable(duplicateRole).ExecuteCommand());

            db.Insertable(CreateRoleResource(30001, 20001, 40001)).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() =>
                db.Insertable(CreateRoleResource(30002, 20001, 40001)).ExecuteCommand());
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void Diagnose_ShouldRejectDuplicateNamesAndBuiltInIdentityCollisions()
    {
        var path = CreatePath("diagnose");
        using var db = CreateSqliteClient(path);
        using var services = new ServiceCollection()
            .AddSingleton<ISqlSugarClient>(db)
            .BuildServiceProvider();
        try
        {
            db.CodeFirst.InitTables<Role>();
            db.Insertable(new[]
            {
                CreateRole(10000, "Operator"),
                CreateRole(20001, " System "),
                CreateRole(20002, "Reviewer"),
                CreateRole(20003, "reviewer")
            }).ExecuteCommand();

            var migration = ConsoleRoleGovernanceSchemaMigration.Instance;
            var issues = migration.Diagnose(db, services);

            Assert.Contains(issues, issue => issue.Contains("活动角色名称重复", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Contains("内建角色 System", StringComparison.Ordinal));
            Assert.Throws<InvalidOperationException>(() => migration.Apply(db, services));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static Role CreateRole(long id, string name) => new(name)
    {
        Id = id,
        IsEnabled = true,
        IsDeleted = false,
        CreateId = 1001,
        CreateBy = "test",
        CreateTime = DateTime.UtcNow,
        ModifyId = 1001,
        ModifyBy = "test",
        ModifyTime = DateTime.UtcNow
    };

    private static RoleConsoleResource CreateRoleResource(long id, long roleId, long resourceId) => new()
    {
        Id = id,
        RoleId = roleId,
        ConsoleResourceId = resourceId,
        CreateId = 1001,
        CreateBy = "test",
        CreateTime = DateTime.UtcNow,
        ModifyId = 1001,
        ModifyBy = "test",
        ModifyTime = DateTime.UtcNow,
        IsDeleted = false
    };

    private static SqlSugarScope CreateSqliteClient(string path) => new(new ConnectionConfig
    {
        ConfigId = "main",
        ConnectionString = $"Data Source={path}",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });

    private static string CreatePath(string scope) => Path.Combine(
        Path.GetTempPath(),
        $"radish-console-role-governance-{scope}-{Guid.NewGuid():N}.db");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
