using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Radish.Common;
using Radish.Common.DbTool;
using Radish.Common.TimeTool;
using Radish.Auth.OpenIddict;

namespace Radish.DbMigrate;

internal static class DbMigrateDoctor
{
    private static readonly string[] RequiredConnIds =
    {
        "Main",
        SqlSugarConst.LogConfigId,
        SqlSugarConst.ChatConfigId,
    };

    public static void Run(
        IServiceProvider services,
        IConfiguration configuration,
        string environment,
        bool strict,
        bool failOnErrors = false)
    {
        Console.WriteLine($"[Radish.DbMigrate] [Doctor] Environment: {environment}");

        var configuredDatabases = AppSettingsTool.RadishApp<MutiDbOperate>("Databases") ?? new List<MutiDbOperate>();
        var enabledDatabases = configuredDatabases.Where(database => database.Enabled).ToList();
        var mainDbConnId = AppSettingsTool.RadishApp("MainDb");

        var errors = new List<string>();
        var warnings = new List<string>();

        Console.WriteLine($"[Radish.DbMigrate] [Doctor] MainDb: {mainDbConnId ?? "<null>"}");
        Console.WriteLine($"[Radish.DbMigrate] [Doctor] Databases: 配置 {configuredDatabases.Count} 个，启用 {enabledDatabases.Count} 个");

        if (configuredDatabases.Count == 0)
        {
            errors.Add("未读取到 Databases 配置。请检查 appsettings.Shared.json 或 Local 覆盖配置。");
        }

        if (string.IsNullOrWhiteSpace(mainDbConnId))
        {
            errors.Add("缺少 MainDb 配置，无法判断主库。");
        }

        var enabledConnIdSet = enabledDatabases
            .Select(database => database.ConnId)
            .Where(connId => !string.IsNullOrWhiteSpace(connId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var connId in RequiredConnIds)
        {
            if (!enabledConnIdSet.Contains(connId))
            {
                errors.Add($"缺少必需启用连接：{connId}");
            }
        }

        if (!string.IsNullOrWhiteSpace(mainDbConnId) && !enabledConnIdSet.Contains(mainDbConnId))
        {
            errors.Add($"MainDb={mainDbConnId} 未在 Enabled=true 的 Databases 中找到。");
        }

        foreach (var database in configuredDatabases)
        {
            var dbType = Enum.GetName(typeof(DataBaseType), database.DbType) ?? database.DbType.ToString();
            var connId = string.IsNullOrWhiteSpace(database.ConnId) ? "<empty>" : database.ConnId;
            Console.WriteLine($"[Radish.DbMigrate] [Doctor] ConnId={connId}, Enabled={database.Enabled}, DbType={dbType}, Target={DescribeTarget(database)}");

            if (database.Enabled && string.IsNullOrWhiteSpace(database.ConnectionString))
            {
                errors.Add($"连接 {connId} 已启用，但 ConnectionString 为空。");
            }
        }

        if (errors.Count == 0)
        {
            ProbeSeedTables(services, mainDbConnId, warnings, errors);
            ProbeOpenIddict(services, configuration, warnings, errors);
        }

        PrintSection("Warning", warnings);
        PrintSection("Error", errors);

        if (errors.Count > 0)
        {
            Console.WriteLine("[Radish.DbMigrate] [Doctor] 结论：当前环境不建议直接执行 seed，请先修复上述问题。");
            if (strict || failOnErrors)
            {
                throw new InvalidOperationException("DbMigrate 检查失败，请处理上方 Error 后重试。");
            }
            return;
        }

        if (warnings.Count > 0)
        {
            Console.WriteLine("[Radish.DbMigrate] [Doctor] 结论：配置可用，但建议先执行 init 或补齐缺失表后再执行 seed。");
            if (strict)
            {
                throw new InvalidOperationException("DbMigrate verify 失败，请处理上方 Warning 后重试。");
            }
            return;
        }

        Console.WriteLine("[Radish.DbMigrate] [Doctor] 结论：当前环境可直接执行 init / seed。");
    }

    private static void ProbeOpenIddict(
        IServiceProvider services,
        IConfiguration configuration,
        List<string> warnings,
        List<string> errors)
    {
        var database = AuthOpenIddictPersistence.ResolveDatabase(configuration);
        Console.WriteLine($"[Radish.DbMigrate] [Doctor] OpenIddict provider={database.DbType}。");

        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuthOpenIddictDbContext>();
            var status = AuthOpenIddictPersistence.Inspect(db, database);
            Console.WriteLine(
                $"[Radish.DbMigrate] [Doctor] OpenIddict applied={status.AppliedMigrations.Count}, pending={status.PendingMigrations.Count}。");
            if (status.ModelDifferences.Count > 0)
            {
                errors.Add(
                    $"OpenIddict 运行态模型与 migration snapshot 不一致：" +
                    $"{string.Join("；", status.ModelDifferences)}。");
            }
            if (status.PendingMigrations.Count > 0)
            {
                warnings.Add(
                    $"OpenIddict pending migrations：{string.Join(", ", status.PendingMigrations)}；请执行 DbMigrate apply。");
            }
        }
        catch (InvalidOperationException exception) when (
            database.DbType == DataBaseType.Sqlite &&
            exception.Message.Contains("SQLite 数据库不存在", StringComparison.Ordinal))
        {
            warnings.Add(exception.Message);
        }
        catch (Exception exception)
        {
            errors.Add($"OpenIddict schema 探测失败：{exception.Message}");
        }
    }

    private static void ProbeSeedTables(IServiceProvider services, string? mainDbConnId, List<string> warnings, List<string> errors)
    {
        try
        {
            var inspectionResult = DbMigrateInspection.InspectSeedReadiness(services, mainDbConnId);

            if (inspectionResult.DatabaseFileMissing)
            {
                warnings.Add($"主库 SQLite 文件不存在：{inspectionResult.DatabaseFilePath ?? "<unknown>"}");
                warnings.Add("核心表状态未探测，因为数据库文件尚未创建。可先执行 init。");
                return;
            }

            if (inspectionResult.MissingTables.Count == 0 && inspectionResult.MissingColumns.Count == 0)
            {
                Console.WriteLine("[Radish.DbMigrate] [Doctor] 主库业务表检查：已齐全。");
            }

            if (inspectionResult.MissingTables.Count > 0)
            {
                warnings.Add($"主库业务表缺失：{string.Join(", ", inspectionResult.MissingTables)}");
            }

            if (inspectionResult.MissingColumns.Count > 0)
            {
                warnings.Add($"主库业务表缺列：{string.Join(", ", inspectionResult.MissingColumns)}");
            }

            var db = services.GetRequiredService<ISqlSugarClient>();
            if (db is SqlSugarScope dbScope)
            {
                var normalizedMainDbConnId = (string.IsNullOrWhiteSpace(mainDbConnId) ? "Main" : mainDbConnId)
                    .ToLowerInvariant();
                var mainDb = dbScope.GetConnectionScope(normalizedMainDbConnId);
                errors.AddRange(FileAccessTokenSecurityMigration.Verify(mainDb));
                var timeAudit = TimeSemanticsAudit.Inspect(
                    mainDb,
                    services.GetRequiredService<BusinessCalendar>());
                foreach (var summary in timeAudit.Summaries)
                {
                    Console.WriteLine($"[Radish.DbMigrate] [Doctor] Time: {summary}");
                }
                warnings.AddRange(timeAudit.Warnings);

                var schemaStatuses = SchemaMigrationLedger.Inspect(dbScope);
                foreach (var status in schemaStatuses)
                {
                    Console.WriteLine(
                        $"[Radish.DbMigrate] [Doctor] Schema: {status.Scope}.{status.MigrationId}: {status.Message}");
                    if (!status.ChecksumMatches)
                    {
                        errors.Add($"{status.Scope}.{status.MigrationId} checksum drift。");
                    }
                    else if (!status.Applied)
                    {
                        warnings.Add($"{status.Scope}.{status.MigrationId} pending；请执行 DbMigrate apply。");
                    }
                }

                foreach (var migration in SchemaMigrationRegistry.All
                             .Where(migration => string.Equals(
                                 migration.Scope,
                                 normalizedMainDbConnId,
                                 StringComparison.OrdinalIgnoreCase)))
                {
                    var isApplied = schemaStatuses
                        .Any(status =>
                            string.Equals(status.Scope, migration.Scope, StringComparison.OrdinalIgnoreCase) &&
                            status.MigrationId == migration.MigrationId &&
                            status.Applied);
                    if (!isApplied)
                    {
                        warnings.AddRange(migration.Diagnose(mainDb, services)
                            .Select(message => $"{migration.Scope}.{migration.MigrationId}: {message}"));
                    }
                }

                errors.AddRange(SchemaMigrationLedger.VerifyApplied(dbScope, services));
            }
        }
        catch (Exception exception)
        {
            errors.Add($"核心表探测失败：{exception.Message}");
        }
    }

    private static string DescribeTarget(MutiDbOperate database)
    {
        if (database.DbType == DataBaseType.Sqlite)
        {
            return $"DataBases/{database.ConnectionString}";
        }

        return string.IsNullOrWhiteSpace(database.ConnectionString) ? "<empty>" : "<configured>";
    }

    private static void PrintSection(string title, IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            Console.WriteLine($"[Radish.DbMigrate] [Doctor] {title}: {item}");
        }
    }
}
