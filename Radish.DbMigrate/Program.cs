using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.DbMigrate;
using Radish.Extension;
using Radish.Extension.SqlSugarExtension;
using Radish.Common.DbTool;
using SqlSugar;

// 简单的迁移/初始化控制台：
// dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init
//  - 初始化数据库（按配置）并根据实体结构创建/更新表
// dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- seed
//  - 执行基础数据灌入（例如默认角色/管理员/租户等）

var builder = Host.CreateApplicationBuilder(args);

// 复用与 Radish.Api 相同的配置加载顺序（但 DbMigrate 运行在控制台，需显式指向输出目录的配置文件）
var contentRoot = AppContext.BaseDirectory;

builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile(Path.Combine(contentRoot, "appsettings.json"), optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile(Path.Combine(contentRoot, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile(Path.Combine(contentRoot, "appsettings.Local.json"), optional: true, reloadOnChange: false);

// 先将配置绑定到全局 App/InternalApp（用于 ConfigurableOptions 等静态访问）
InternalApp.ConfigureApplication(builder.Configuration);

// 注册 AppSettings 与 SqlSugar，与 API 尽量保持一致
builder.Services.AddSingleton(new AppSettingsTool(builder.Configuration));
builder.Services.AddAllOptionRegister();
builder.Services.AddSqlSugarSetup();

// 标记 InternalApp 已就绪，便于 SqlSugarAop 等读取配置
builder.Services.ConfigureApplication();

using var host = builder.Build();
InternalApp.ConfigureApplication(host);

var services = host.Services;
var configuration = builder.Configuration;

var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";

switch (mode)
{
    case "init":
        await RunInitAsync(services, configuration, builder.Environment.EnvironmentName);
        break;

    case "seed":
        await RunSeedAsync(services, configuration, builder.Environment.EnvironmentName);
        break;

    default:
        PrintHelp();
        break;
}

return;

static async Task RunInitAsync(IServiceProvider services, IConfiguration configuration, string environment)
{
    Console.WriteLine($"[Radish.DbMigrate] Environment: {environment}");

    var db = services.GetRequiredService<ISqlSugarClient>();

    Console.WriteLine("[Radish.DbMigrate] 创建数据库（如不存在）...");
    foreach (var _ in Radish.Common.DbTool.BaseDbConfig.AllConfigs)
    {
        // 使用当前连接执行 CreateDatabase，SqlSugar 会根据连接串创建数据库文件/实例
        db.DbMaintenance.CreateDatabase();
    }

    Console.WriteLine("[Radish.DbMigrate] 初始化业务表结构（Code First）...");

    // 直接复用 BaseDbConfig.AllConfigs 的连接配置，通过 Code First 为所有实体建表
    foreach (var config in BaseDbConfig.AllConfigs)
    {
        var dbForConfig = (SqlSugarScope)db;
        var conn = dbForConfig.GetConnectionScope(config.ConfigId.ToString());

        // 按约定扫描 Radish.Model 程序集中的实体类型：
        // - 带有 [SugarTable] 特性的实体；
        // - 或继承自 RootEntityTKey<> 的实体（大部分业务表）。
        var modelAssembly = typeof(Radish.Model.Root.RootEntityTKey<>).Assembly;
        var allEntityTypes = modelAssembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
            .Where(t =>
                t.GetCustomAttributes(typeof(SugarTable), inherit: true).Any() ||
                (t.BaseType != null && t.BaseType.IsGenericType &&
                 t.BaseType.GetGenericTypeDefinition() == typeof(Radish.Model.Root.RootEntityTKey<>))
            )
            // 显式包含 UserRole 这样的非 RootEntityTKey<> 映射实体
            .Concat(new[] { typeof(Radish.Model.UserRole) })
            .Distinct();

        // 根据 [Tenant(configId)] 注解过滤实体
        var entityTypesForConfig = allEntityTypes.Where(type =>
        {
            var tenantAttr = type.GetCustomAttributes(typeof(TenantAttribute), inherit: true)
                .Cast<TenantAttribute>()
                .FirstOrDefault();

            // 如果实体有 [Tenant(configId)] 注解，只在对应的数据库中初始化
            if (tenantAttr != null && !string.IsNullOrEmpty(tenantAttr.configId))
            {
                return tenantAttr.configId.Equals(config.ConfigId.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            // 如果实体没有 [Tenant] 注解，只在主库中初始化（排除 Log 库）
            return !config.ConfigId.ToString().Equals("Log", StringComparison.OrdinalIgnoreCase);
        }).ToList();

        foreach (var type in entityTypesForConfig)
        {
            Console.WriteLine($"  -> Init table for entity: {type.FullName} (ConnId={config.ConfigId})");
            conn.CodeFirst.InitTables(type);
        }
    }

    Console.WriteLine("[Radish.DbMigrate] Init 完成。");
}

static async Task RunSeedAsync(IServiceProvider services, IConfiguration configuration, string environment)
{
    Console.WriteLine($"[Radish.DbMigrate] Environment: {environment}");

    var db = services.GetRequiredService<ISqlSugarClient>();

    // 检查 Role 表是否存在，如果不存在则先执行 init
    Console.WriteLine("[Radish.DbMigrate] 检查数据库表结构...");
    var roleTableExists = db.DbMaintenance.IsAnyTable("Role", false);

    if (!roleTableExists)
    {
        Console.WriteLine("[Radish.DbMigrate] ⚠️  检测到数据库表结构未初始化，自动执行 init...");
        await RunInitAsync(services, configuration, environment);
        Console.WriteLine();
    }
    else
    {
        Console.WriteLine("[Radish.DbMigrate] ✓ 数据库表结构已存在");
    }

    Console.WriteLine("[Radish.DbMigrate] 开始执行初始数据 Seed...");
    await InitialDataSeeder.SeedAsync(db);
}

static void PrintHelp()
{
    Console.WriteLine("Radish.DbMigrate 用法:");
    Console.WriteLine("  dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init");
    Console.WriteLine("      初始化数据库并基于实体结构创建/更新表结构。");
    Console.WriteLine("  dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- seed");
    Console.WriteLine("      执行数据初始化（例如默认角色/管理员/租户等）。");
}
