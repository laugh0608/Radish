using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Radish.Common;
using Radish.Common.DbTool;

namespace Radish.DbMigrate;

internal static class DbMigrateDoctor
{
    private static readonly string[] RequiredConnIds =
    {
        "Main",
        SqlSugarConst.LogConfigId,
        SqlSugarConst.ChatConfigId,
    };

    public static void Run(IServiceProvider services, IConfiguration configuration, string environment)
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
        }

        PrintSection("Warning", warnings);
        PrintSection("Error", errors);

        if (errors.Count > 0)
        {
            Console.WriteLine("[Radish.DbMigrate] [Doctor] 结论：当前环境不建议直接执行 seed，请先修复上述问题。");
            return;
        }

        if (warnings.Count > 0)
        {
            Console.WriteLine("[Radish.DbMigrate] [Doctor] 结论：配置可用，但建议先执行 init 或补齐缺失表后再执行 seed。");
            return;
        }

        Console.WriteLine("[Radish.DbMigrate] [Doctor] 结论：当前环境可直接执行 init / seed。");
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

            if (inspectionResult.MissingTables.Count == 0)
            {
                Console.WriteLine("[Radish.DbMigrate] [Doctor] 主库业务表检查：已齐全。");
                return;
            }

            warnings.Add($"主库业务表缺失：{string.Join(", ", inspectionResult.MissingTables)}");
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
