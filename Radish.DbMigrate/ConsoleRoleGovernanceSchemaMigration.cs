using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>固定角色名称与 Console 资源授权聚合唯一性。</summary>
internal sealed class ConsoleRoleGovernanceSchemaMigration : ISchemaMigration
{
    private const long SystemRoleId = 10000;
    private const long AdminRoleId = 10001;
    private const string RoleNameIndex = "idx_role_active_name_unique";
    private const string RoleResourceIndex = "idx_role_console_resource_unique";

    public static ConsoleRoleGovernanceSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260809_019_console_role_governance";

    public string Scope => "Main";

    public string Description => "固定活动角色名称与角色资源关联唯一性";

    public string ChecksumSource =>
        "20260809_019_console_role_governance|Main|" +
        "role-active-name-lower-trim-unique-v2|role-resource-pair-unique-v1|" +
        "system-admin-protected-identity-v2";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = Diagnose(db, services);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "Console 角色治理迁移前置诊断未通过：" + string.Join("；", issues));
        }

        db.CodeFirst.InitTables<Role, RoleConsoleResource>();
        EnsureActiveRoleNameIndex(db);
        EnsureRoleResourceIndex(db);
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        if (db.DbMaintenance.IsAnyTable(nameof(Role), false))
        {
            var roles = db.Queryable<Role>().ToList();
            var duplicateNames = roles
                .Where(role => !role.IsDeleted)
                .GroupBy(role => role.RoleName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicateNames.Count > 0)
            {
                issues.Add($"活动角色名称重复：{string.Join(", ", duplicateNames)}");
            }

            VerifyBuiltInRole(issues, roles, SystemRoleId, "System");
            VerifyBuiltInRole(issues, roles, AdminRoleId, "Admin");
        }

        if (db.DbMaintenance.IsAnyTable(nameof(RoleConsoleResource), false))
        {
            var duplicateLinks = db.Queryable<RoleConsoleResource>()
                .ToList()
                .GroupBy(link => (link.RoleId, link.ConsoleResourceId))
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key.RoleId}:{group.Key.ConsoleResourceId}")
                .ToList();
            if (duplicateLinks.Count > 0)
            {
                issues.Add($"角色资源关联重复：{string.Join(", ", duplicateLinks)}");
            }
        }

        return issues;
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = Diagnose(db, services).ToList();
        if (!db.DbMaintenance.IsAnyTable(nameof(Role), false))
        {
            issues.Add("缺少表 Role。");
        }
        else if (!IndexExists(db, nameof(Role), RoleNameIndex))
        {
            issues.Add($"缺少索引 {RoleNameIndex}。");
        }

        if (!db.DbMaintenance.IsAnyTable(nameof(RoleConsoleResource), false))
        {
            issues.Add("缺少表 RoleConsoleResource。");
        }
        else if (!IndexExists(db, nameof(RoleConsoleResource), RoleResourceIndex))
        {
            issues.Add($"缺少索引 {RoleResourceIndex}。");
        }

        return issues.Distinct(StringComparer.Ordinal).ToList();
    }

    private static void EnsureActiveRoleNameIndex(ISqlSugarClient db)
    {
        if (IndexExists(db, nameof(Role), RoleNameIndex))
        {
            return;
        }

        var sql = db.CurrentConnectionConfig.DbType switch
        {
            DbType.Sqlite =>
                $"CREATE UNIQUE INDEX \"{RoleNameIndex}\" ON \"Role\" (LOWER(TRIM(\"RoleName\"))) WHERE \"IsDeleted\" = 0",
            DbType.PostgreSQL =>
                $"CREATE UNIQUE INDEX \"{RoleNameIndex}\" ON \"Role\" (LOWER(TRIM(\"RoleName\"))) WHERE NOT \"IsDeleted\"",
            _ => throw new InvalidOperationException(
                $"Console 角色治理迁移不支持数据库 {db.CurrentConnectionConfig.DbType}。")
        };
        db.Ado.ExecuteCommand(sql);
    }

    private static void VerifyBuiltInRole(
        ICollection<string> issues,
        IReadOnlyCollection<Role> roles,
        long roleId,
        string roleName)
    {
        var role = roles.FirstOrDefault(item => item.Id == roleId);
        var roleByName = roles.FirstOrDefault(item =>
            string.Equals(item.RoleName.Trim(), roleName, StringComparison.OrdinalIgnoreCase));
        if (role == null && roleByName == null)
        {
            return;
        }

        if (role == null || roleByName?.Id != roleId || role.IsDeleted || !role.IsEnabled ||
            !string.Equals(role.RoleName, roleName, StringComparison.Ordinal))
        {
            issues.Add($"内建角色 {roleName}（{roleId}）身份无效。");
        }
    }

    private static bool IndexExists(ISqlSugarClient db, string tableName, string indexName)
    {
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return false;
        }

        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return db.DbMaintenance.IsAnyIndex(indexName);
        }

        return db.DbMaintenance.GetIndexList(tableName)
            .Any(index => string.Equals(index, indexName, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureRoleResourceIndex(ISqlSugarClient db)
    {
        if (IndexExists(db, nameof(RoleConsoleResource), RoleResourceIndex))
        {
            return;
        }

        var created = db.DbMaintenance.CreateIndex(
            nameof(RoleConsoleResource),
            [nameof(RoleConsoleResource.RoleId), nameof(RoleConsoleResource.ConsoleResourceId)],
            RoleResourceIndex,
            true);
        if (!created && !IndexExists(db, nameof(RoleConsoleResource), RoleResourceIndex))
        {
            throw new InvalidOperationException($"创建索引 {RoleResourceIndex} 失败。");
        }
    }
}
