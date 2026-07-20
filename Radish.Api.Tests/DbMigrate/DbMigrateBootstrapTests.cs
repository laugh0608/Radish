using Microsoft.Extensions.Hosting;
using Radish.DbMigrate;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.DbMigrate;

public sealed class DbMigrateBootstrapTests
{
    [Fact(Timeout = 10_000, DisplayName = "DbMigrate 应通过显式配置完成 Host 初始化")]
    public void CreateBuilder_ShouldBuildHostWithExplicitConfiguration()
    {
        var builder = DbMigrateBootstrap.CreateBuilder();

        builder.Configuration["MainDb"].ShouldBe("Main");
        using var host = builder.Build();
        host.Services.ShouldNotBeNull();
    }
}
