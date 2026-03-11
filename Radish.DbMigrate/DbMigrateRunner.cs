using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Radish.Common.DbTool;

namespace Radish.DbMigrate;

internal static class DbMigrateRunner
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, string environment, string[] args)
    {
        var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";

        switch (mode)
        {
            case "doctor":
            case "verify":
                DbMigrateDoctor.Run(services, configuration, environment);
                break;

            case "init":
                await RunInitAsync(services, configuration, environment);
                break;

            case "seed":
                await RunSeedAsync(services, configuration, environment);
                break;

            default:
                PrintHelp();
                break;
        }
    }

    private static async Task RunInitAsync(IServiceProvider services, IConfiguration configuration, string environment)
    {
        Console.WriteLine($"[Radish.DbMigrate] Environment: {environment}");

        var db = services.GetRequiredService<ISqlSugarClient>();

        Console.WriteLine("[Radish.DbMigrate] 创建数据库（如不存在）...");
        foreach (var _ in BaseDbConfig.AllConfigs)
        {
            db.DbMaintenance.CreateDatabase();
        }

        Console.WriteLine("[Radish.DbMigrate] 初始化业务表结构（Code First）...");

        foreach (var config in BaseDbConfig.AllConfigs)
        {
            var dbForConfig = (SqlSugarScope)db;
            var configId = config.ConfigId?.ToString();
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new InvalidOperationException("DbMigrate 遇到缺少 ConfigId 的数据库连接配置，无法继续初始化表结构。");
            }

            var conn = dbForConfig.GetConnectionScope(configId);

            var modelAssembly = typeof(Radish.Model.Root.RootEntityTKey<>).Assembly;
            var allEntityTypes = modelAssembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
                .Where(t =>
                    t.GetCustomAttributes(typeof(SugarTable), inherit: true).Any() ||
                    (t.BaseType != null && t.BaseType.IsGenericType &&
                     t.BaseType.GetGenericTypeDefinition() == typeof(Radish.Model.Root.RootEntityTKey<>))
                )
                .Concat(new[] { typeof(Radish.Model.UserRole) })
                .Distinct();

            var configIdStr = configId;
            var entityTypesForConfig = allEntityTypes.Where(type =>
            {
                var tenantAttr = type.GetCustomAttributes(typeof(TenantAttribute), inherit: true)
                    .Cast<TenantAttribute>()
                    .FirstOrDefault();

                if (tenantAttr != null && tenantAttr.configId != null)
                {
                    var tenantConfigId = tenantAttr.configId.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(tenantConfigId))
                    {
                        return string.Equals(tenantConfigId, configIdStr, StringComparison.OrdinalIgnoreCase);
                    }
                }

                return string.Equals(configIdStr, "Main", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            foreach (var type in entityTypesForConfig)
            {
                Console.WriteLine($"  -> Init table for entity: {type.FullName} (ConnId={config.ConfigId})");
                conn.CodeFirst.InitTables(type);
            }
        }

        Console.WriteLine("[Radish.DbMigrate] Init 完成。");
    }

    private static async Task RunSeedAsync(IServiceProvider services, IConfiguration configuration, string environment)
    {
        Console.WriteLine($"[Radish.DbMigrate] Environment: {environment}");

        var db = services.GetRequiredService<ISqlSugarClient>();

        Console.WriteLine("[Radish.DbMigrate] 检查数据库表结构...");
        var roleTableExists = db.DbMaintenance.IsAnyTable("Role", false);
        var shopTableExists = db.DbMaintenance.IsAnyTable("ShopProductCategory", false);
        var userTimePreferenceTableExists = db.DbMaintenance.IsAnyTable("UserTimePreference", false);
        var stickerGroupTableExists = db.DbMaintenance.IsAnyTable("StickerGroup", false);
        var stickerTableExists = db.DbMaintenance.IsAnyTable("Sticker", false);
        var reactionTableExists = db.DbMaintenance.IsAnyTable("Reaction", false);
        var wikiDocumentTableExists = db.DbMaintenance.IsAnyTable("WikiDocument", false);
        var wikiDocumentRevisionTableExists = db.DbMaintenance.IsAnyTable("WikiDocumentRevision", false);

        if (!roleTableExists || !shopTableExists || !userTimePreferenceTableExists || !stickerGroupTableExists || !stickerTableExists || !reactionTableExists || !wikiDocumentTableExists || !wikiDocumentRevisionTableExists)
        {
            var missingTables = new List<string>();
            if (!roleTableExists) missingTables.Add("Role");
            if (!shopTableExists) missingTables.Add("ShopProductCategory");
            if (!userTimePreferenceTableExists) missingTables.Add("UserTimePreference");
            if (!stickerGroupTableExists) missingTables.Add("StickerGroup");
            if (!stickerTableExists) missingTables.Add("Sticker");
            if (!reactionTableExists) missingTables.Add("Reaction");
            if (!wikiDocumentTableExists) missingTables.Add("WikiDocument");
            if (!wikiDocumentRevisionTableExists) missingTables.Add("WikiDocumentRevision");

            Console.WriteLine($"[Radish.DbMigrate] ⚠️  检测到表结构缺失 ({string.Join(", ", missingTables)})，自动执行 init...");
            await RunInitAsync(services, configuration, environment);
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("[Radish.DbMigrate] ✓ 数据库表结构已存在");
        }

        Console.WriteLine("[Radish.DbMigrate] 开始执行初始数据 Seed...");
        Console.WriteLine("[Radish.DbMigrate] 表情包种子策略：当前不预置默认分组/表情，仅确保表结构可用。");
        await InitialDataSeeder.SeedAsync(db, services);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Radish.DbMigrate 用法:");
        Console.WriteLine("  dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor");
        Console.WriteLine("      只读检查当前配置、连接定义与 seed 核心表状态。");
        Console.WriteLine("  dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init");
        Console.WriteLine("      初始化数据库并基于实体结构创建/更新表结构。");
        Console.WriteLine("  dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- seed");
        Console.WriteLine("      执行数据初始化（例如默认角色/管理员/租户等）。");
    }
}
