using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.DbMigrate;
using Radish.Extension;
using Radish.Extension.SqlSugarExtension;
using SqlSugar;

// 简单的迁移/初始化控制台：
// dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init
//  - 初始化数据库（按配置）并根据实体结构创建/更新表
// dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- seed
//  - 执行基础数据灌入（例如默认角色/管理员/租户等）

var builder = Host.CreateApplicationBuilder(args);

// 复用与 Radish.Api 相同的配置加载顺序
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

// 注册 AppSettings 与 SqlSugar，与 API 完全一致
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

    // 这里直接扫描 Radish.Model 程序集中的实体类型
    var modelAssembly = typeof(Radish.Model.Root.RootEntityTKey<>).Assembly;
    var entityTypes = modelAssembly
        .GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
        .Where(t => t.GetCustomAttributes(typeof(SugarTable), inherit: true).Any());

    foreach (var type in entityTypes)
    {
        Console.WriteLine($"  -> Init table for entity: {type.FullName}");
        db.CodeFirst.InitTables(type);
    }

    Console.WriteLine("[Radish.DbMigrate] Init 完成。");
}

static async Task RunSeedAsync(IServiceProvider services, IConfiguration configuration, string environment)
{
    Console.WriteLine($"[Radish.DbMigrate] Environment: {environment}");

    var db = services.GetRequiredService<ISqlSugarClient>();

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
