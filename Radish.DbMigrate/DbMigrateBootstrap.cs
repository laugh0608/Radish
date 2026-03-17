using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Extension;
using Radish.Extension.ExperienceExtension;
using Radish.Extension.SqlSugarExtension;

namespace Radish.DbMigrate;

internal static class DbMigrateBootstrap
{
    public static HostApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        ConfigureConfiguration(builder);
        ConfigureServices(builder);

        return builder;
    }

    private static void ConfigureConfiguration(HostApplicationBuilder builder)
    {
        var solutionRoot = AppPathTool.GetSolutionRootOrBasePath();
        var projectRoot = Path.Combine(solutionRoot, nameof(Radish.DbMigrate));

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile(Path.Combine(solutionRoot, "appsettings.Shared.json"), optional: true, reloadOnChange: false);
        builder.Configuration.AddJsonFile(Path.Combine(projectRoot, "appsettings.json"), optional: true, reloadOnChange: false);
        builder.Configuration.AddJsonFile(Path.Combine(projectRoot, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: false);
        builder.Configuration.AddJsonFile(Path.Combine(projectRoot, "appsettings.Local.json"), optional: true, reloadOnChange: false);
        builder.Configuration.AddJsonFile(Path.Combine(solutionRoot, "appsettings.Local.json"), optional: true, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables();

        InternalApp.ConfigureApplication(builder.Configuration);
    }

    private static void ConfigureServices(HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(new AppSettingsTool(builder.Configuration));
        builder.Services.AddAllOptionRegister();
        builder.Services.AddSqlSugarSetup();
        builder.Services.AddExperienceCalculator(builder.Configuration);
        builder.Services.ConfigureApplication();
    }
}
