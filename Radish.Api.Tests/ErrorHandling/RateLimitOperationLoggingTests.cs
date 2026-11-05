using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Common.CoreTool;
using Radish.Common.OptionTool;
using Radish.Extension.ExperienceExtension;
using Radish.Extension.RateLimitExtension;
using Radish.IService;
using Serilog;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class RateLimitOperationLoggingTests
{
    public static IEnumerable<object[]> Routes()
    {
        foreach (var route in ApplicationOperationLoggingTests.Routes())
        foreach (var diagnostics in new[] { false, true })
        foreach (var enableLogging in new[] { false, true })
            yield return route.Concat(new object[] { diagnostics, enableLogging }).ToArray();
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task RateLimit_ShouldKeep429AndOnlyGenerateExplicitDevelopmentDiagnostics(
        bool candidate, string environment, bool audit, bool diagnostics, bool enableLogging)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: diagnostics, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = capture.SensitiveScope();
        await using var pipeline = new RateLimitOperationPipeline(capture, diagnostics, enableLogging);
        Assert.Equal("rate_limit.configured", Assert.Single(capture.Codes));
        Assert.False(Assert.Single(capture.Events).Diagnostic);
        capture.Clear();
        var first = await pipeline.Request();
        Assert.Equal(200, first.Status);
        var second = await pipeline.Request();
        Assert.Equal(429, second.Status);
        Assert.True(double.TryParse(second.RetryAfter, out var retryAfter) && retryAfter >= 0);
        using var response = JsonDocument.Parse(second.Body);
        Assert.Equal(429, response.RootElement.GetProperty("statusCode").GetInt32());
        Assert.False(response.RootElement.GetProperty("isSuccess").GetBoolean());
        var enabled = environment == "Development" && diagnostics && enableLogging;
        Assert.Equal(enabled ? new[] { "rate_limit.rejected" } : Array.Empty<string>(), capture.Codes);
        Assert.All(capture.Events, e => Assert.True(e.Diagnostic));
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(ApplicationOperationLoggingTests.Routes), MemberType = typeof(ApplicationOperationLoggingTests))]
    public async Task Lists_ShouldRetainExistingBypassAndNamedPolicy403(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = capture.SensitiveScope();
        foreach (var list in new[] { "Whitelist", "Blacklist" })
        {
            await using (var pipeline = new RateLimitOperationPipeline(capture, true, true, list: list))
            {
                capture.Clear();
                Assert.Equal(200, (await pipeline.Request()).Status);
                Assert.Equal(200, (await pipeline.Request()).Status);
                if (environment == "Development")
                    Assert.All(capture.Codes, code => Assert.Equal("rate_limit." + list.ToLowerInvariant() + "_observed", code));
                else Assert.Empty(capture.Events);
                capture.AssertSafe();
            }
            capture.Clear();
        }
        await using var named = new RateLimitOperationPipeline(capture, true, true, list: "Blacklist", namedPolicy: true);
        capture.Clear();
        Assert.Equal(200, (await named.Request()).Status);
        var rejected = await named.Request();
        Assert.Equal(403, rejected.Status);
        using var response = JsonDocument.Parse(rejected.Body);
        Assert.Equal("RateLimit.IpBlocked", response.RootElement.GetProperty("code").GetString());
        if (environment == "Development") Assert.Contains("rate_limit.rejected", capture.Codes);
        else Assert.Empty(capture.Events);
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(ApplicationOperationLoggingTests.Routes), MemberType = typeof(ApplicationOperationLoggingTests))]
    public async Task DisabledRateLimitAndExperience_ShouldKeepRegistrationAndQuietMiddleware(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        await using var pipeline = new RateLimitOperationPipeline(capture, true, true, enabled: false);
        Assert.Equal("rate_limit.configured", Assert.Single(capture.Codes));
        capture.Clear();
        Assert.Equal(200, (await pipeline.Request()).Status);
        Assert.Equal(200, (await pipeline.Request()).Status);
        var services = new ServiceCollection();
        services.AddExperienceCalculator(new ConfigurationBuilder().Build());
        Assert.Contains(services, d => d.ServiceType == typeof(IExperienceCalculator) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Empty(capture.Events);
    }
}

/// <summary>构建真实中间件，直接调用 RequestDelegate；不监听、不 Start/Run。</summary>
internal sealed class RateLimitOperationPipeline : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly RequestDelegate _pipeline;
    private readonly (FieldInfo Field, object? Value)[] _previous;

    public RateLimitOperationPipeline(HttpBoundaryCapture capture, bool diagnostics, bool enableLogging,
        string? list = null, bool namedPolicy = false, bool enabled = true)
    {
        _previous = new[] { "HostEnvironment", "WebHostEnvironment", "InternalServices", "Configuration", "RootServices" }
            .Select(name => typeof(InternalApp).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!)
            .Select(field => (field, field.GetValue(null))).ToArray();
        try
        {
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { EnvironmentName = capture.Environment });
            builder.WebHost.UseKestrel(); // Build only; no listener is started.
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RadishLogging:Mode"] = capture.Environment,
                ["RadishLogging:Diagnostics"] = diagnostics.ToString(),
                ["RateLimit:Enable"] = enabled.ToString(), ["RateLimit:EnableLogging"] = enableLogging.ToString(),
                ["RateLimit:Global:PermitLimit"] = "1", ["RateLimit:Global:WindowSeconds"] = "600",
                ["RateLimit:Login:PermitLimit"] = "1", ["RateLimit:Login:WindowSeconds"] = "600",
                ["RateLimit:Whitelist:Enable"] = (list == "Whitelist").ToString(), ["RateLimit:Whitelist:IpAddresses:0"] = "127.0.0.1",
                ["RateLimit:Blacklist:Enable"] = (list == "Blacklist").ToString(), ["RateLimit:Blacklist:IpAddresses:0"] = "127.0.0.1"
            });
            builder.ConfigureApplication();
            InternalApp.ConfigureApplication(builder.Configuration);
            builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimit"));
            using var registrationServices = builder.Services.BuildServiceProvider();
            InternalApp.ConfigureApplication(registrationServices);
            builder.Logging.ClearProviders();
            // Framework pipeline messages are outside these owned sources; retain the actual rate-limit producer's global logger.
            builder.Services.AddRouting();
            builder.Services.AddMetrics();
            builder.Services.AddRateLimitSetup();
            _app = builder.Build();
            InternalApp.ConfigureApplication(_app.Services);
            _app.UseRouting();
            _app.UseRateLimitSetup();
            _app.UseEndpoints(endpoints =>
            {
                var endpoint = endpoints.MapGet("/{**path}", context => context.Response.WriteAsync("ok"));
                if (namedPolicy) endpoint.RequireRateLimiting(RateLimitSetup.PolicyNames.Login);
            });
            _pipeline = ((IApplicationBuilder)_app).Build();
        }
        catch
        {
            foreach (var previous in _previous) previous.Field.SetValue(null, previous.Value);
            throw;
        }
    }

    public async Task<(int Status, string Body, string RetryAfter)> Request()
    {
        var context = new DefaultHttpContext { RequestServices = _app.Services };
        context.Request.Method = "GET";
        context.Request.Path = "/" + HttpBoundaryCapture.Secret;
        context.Request.Headers.Authorization = "Bearer " + HttpBoundaryCapture.Secret;
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        using var body = new MemoryStream();
        context.Response.Body = body;
        await _pipeline(context);
        return (context.Response.StatusCode, System.Text.Encoding.UTF8.GetString(body.ToArray()), context.Response.Headers.RetryAfter.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        try { await _app.DisposeAsync(); }
        finally { foreach (var previous in _previous) previous.Field.SetValue(null, previous.Value); }
    }
}
