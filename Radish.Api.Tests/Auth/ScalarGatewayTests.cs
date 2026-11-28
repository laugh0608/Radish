using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radish.Extension.OpenApiExtension;
using Xunit;

namespace Radish.Api.Tests.Auth;

public sealed class ScalarGatewayTests
{
    [Theory]
    [InlineData("https://radish.test:35443", "https://radish.test:35443")]
    [InlineData("https://radish.test:35443/prefix/", "https://radish.test:35443/prefix")]
    [InlineData(null, "https://localhost:5000")]
    public async Task OpenApiAndScalarPageUseRegisteredPublicGateway(string? issuer, string expected)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ApplicationName = typeof(ScalarSetup).Assembly.GetName().Name });
        builder.WebHost.UseKestrel(); // 仅构造内存管道。
        builder.Services.AddMetrics();
        builder.Configuration.AddInMemoryCollection();
        builder.Configuration["OpenIddict:Server:Issuer"] = issuer;
        builder.Services.AddControllers();
        builder.Services.AddAuthorization();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddApiVersioning().AddMvc().AddApiExplorer(o => o.GroupNameFormat = "'v'VVV");
        builder.Services.AddScalarSetup();
        await using var app = builder.Build();
        app.UseRouting();
        app.UseScalarUI();
        app.MapGet("/account", () => "account").WithGroupName("v1").RequireAuthorization();
        app.MapGet("/public", () => "public").WithGroupName("v1").RequireAuthorization().AllowAnonymous();
        app.UseEndpoints(_ => { });
        var pipeline = ((IApplicationBuilder)app).Build();

        using var json = JsonDocument.Parse(await Read("/openapi/v1.json"));
        var document = json.RootElement;
        var servers = document.GetProperty("servers");
        Assert.Equal(expected, servers[0].GetProperty("url").GetString());
        if (issuer != null) Assert.Equal(1, servers.GetArrayLength());
        var flow = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("oauth2")
            .GetProperty("flows").GetProperty("authorizationCode");
        Assert.Equal(expected + "/connect/authorize", flow.GetProperty("authorizationUrl").GetString());
        Assert.Equal(expected + "/connect/token", flow.GetProperty("tokenUrl").GetString());
        var paths = document.GetProperty("paths");
        Assert.True(paths.GetProperty("/account").GetProperty("get").GetProperty("security")[0].TryGetProperty("oauth2", out _));
        Assert.False(paths.GetProperty("/public").GetProperty("get").TryGetProperty("security", out _));

        var page = await Read("/scalar/v1");
        Assert.Contains(expected + "/scalar/oauth2-callback", page);
        Assert.Contains("radish-scalar", page);
        Assert.Contains("SHA-256", page);

        async Task<string> Read(string path)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("internal-api", 5100);
            context.Request.Method = "GET";
            context.Request.Path = path;
            await using var stream = new MemoryStream();
            context.Response.Body = stream;
            await pipeline(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        }
    }
}
