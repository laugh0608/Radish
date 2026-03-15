using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Radish.Common;
using Radish.DbMigrate;

// 简单的迁移/初始化控制台：
// dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj
// dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- apply
//  - 推荐入口。自动检查数据库、按需 init，并执行 seed
// dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor
//  - 只读检查当前配置、连接定义与 seed 核心表状态
// dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init
//  - 高级命令。仅初始化数据库（按配置）并根据实体结构创建/更新表
// dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- seed
//  - 高级命令。执行基础数据灌入（例如默认角色/管理员/租户等）

var builder = DbMigrateBootstrap.CreateBuilder(args);

using var host = builder.Build();
Radish.Common.CoreTool.InternalApp.ConfigureApplication(host);

var services = host.Services;
var configuration = builder.Configuration;

await DbMigrateRunner.RunAsync(services, configuration, builder.Environment.EnvironmentName, args);

return;
