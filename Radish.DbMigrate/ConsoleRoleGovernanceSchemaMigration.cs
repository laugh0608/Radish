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
        if (TableExists(db, nameof(Role)))
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

        if (TableExists(db, nameof(RoleConsoleResource)))
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
        if (!TableExists(db, nameof(Role)))
        {
            issues.Add("缺少表 Role。");
        }
        else if (!IndexExists(db, nameof(Role), RoleNameIndex))
        {
            issues.Add($"缺少索引 {RoleNameIndex}。");
        }

        if (!TableExists(db, nameof(RoleConsoleResource)))
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

        var roleName = DatabaseIdentifierResolver.ResolveColumn(db, nameof(Role), nameof(Role.RoleName))
                       ?? throw new InvalidOperationException("Role.RoleName 不存在。");
        var isDeleted = DatabaseIdentifierResolver.ResolveColumn(db, nameof(Role), nameof(Role.IsDeleted))
                        ?? throw new InvalidOperationException("Role.IsDeleted 不存在。");
        var sql = db.CurrentConnectionConfig.DbType switch
        {
            DbType.Sqlite =>
                $"CREATE UNIQUE INDEX {QuoteIdentifier(RoleNameIndex)} " +
                $"ON {QuoteIdentifier(roleName.TableName)} (LOWER(TRIM({QuoteIdentifier(roleName.ColumnName)}))) " +
                $"WHERE {QuoteIdentifier(isDeleted.ColumnName)} = 0",
            DbType.PostgreSQL =>
                $"CREATE UNIQUE INDEX {QuoteIdentifier(RoleNameIndex)} " +
                $"ON {QuoteIdentifier(roleName.TableName)} (LOWER(TRIM({QuoteIdentifier(roleName.ColumnName)}))) " +
                $"WHERE NOT {QuoteIdentifier(isDeleted.ColumnName)}",
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
        var physicalTableName = DatabaseIdentifierResolver.ResolveColumn(db, tableName, "Id")?.TableName;
        if (physicalTableName == null)
        {
            return false;
        }

        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return db.DbMaintenance.IsAnyIndex(indexName);
        }

        return db.DbMaintenance.GetIndexList(physicalTableName)
            .Any(index => string.Equals(index, indexName, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureRoleResourceIndex(ISqlSugarClient db)
    {
        if (IndexExists(db, nameof(RoleConsoleResource), RoleResourceIndex))
        {
            return;
        }

        var roleId = DatabaseIdentifierResolver.ResolveColumn(
                         db,
                         nameof(RoleConsoleResource),
                         nameof(RoleConsoleResource.RoleId))
                     ?? throw new InvalidOperationException("RoleConsoleResource.RoleId 不存在。");
        var resourceId = DatabaseIdentifierResolver.ResolveColumn(
                             db,
                             nameof(RoleConsoleResource),
                             nameof(RoleConsoleResource.ConsoleResourceId))
                         ?? throw new InvalidOperationException(
                             "RoleConsoleResource.ConsoleResourceId 不存在。");
        var created = db.DbMaintenance.CreateIndex(
            roleId.TableName,
            [roleId.ColumnName, resourceId.ColumnName],
            RoleResourceIndex,
            true);
        if (!created && !IndexExists(db, nameof(RoleConsoleResource), RoleResourceIndex))
        {
            throw new InvalidOperationException($"创建索引 {RoleResourceIndex} 失败。");
        }
    }

    private static bool TableExists(ISqlSugarClient db, string configuredTableName)
    {
        return DatabaseIdentifierResolver.ResolveColumn(db, configuredTableName, "Id") != null;
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
