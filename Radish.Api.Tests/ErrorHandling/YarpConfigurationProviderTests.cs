using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class YarpConfigurationProviderTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void InitialConversionFailure_ShouldThrowWithoutPublishingSnapshot(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var config = Configuration("not-an-integer");
        using var services = Services(capture, config);
        var provider = services.GetRequiredService<IProxyConfigProvider>();
        Assert.Throws<FormatException>(() => provider.GetConfig());
        YarpConfigurationTestSupport.AssertCodes(capture, "proxy.config_source_loading", "proxy.config_source_conversion_failed");
        Assert.Equal("Error", capture.Events.Last().Level);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void ReloadConversionFailure_ShouldRetainSnapshotAndRecoverOnNextChange(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var config = Configuration("1");
        using var services = Services(capture, config);
        var provider = services.GetRequiredService<IProxyConfigProvider>();
        var original = provider.GetConfig();
        Assert.Same(original, provider.GetConfig());
        Assert.Equal(1, Assert.Single(original.Routes).Order);
        YarpConfigurationTestSupport.AssertCodes(capture, "proxy.config_source_loading");
        capture.Clear();

        config["Routes:route:Order"] = "not-an-integer";
        config.Reload(); // 仅内存 provider 的通知，无文件监视。
        Assert.Same(original, provider.GetConfig());
        Assert.False(original.ChangeToken.HasChanged);
        YarpConfigurationTestSupport.AssertCodes(capture, "proxy.config_source_loading", "proxy.config_source_conversion_failed");
        capture.Clear();

        config["Routes:route:Order"] = "2";
        config.Reload();
        var recovered = provider.GetConfig();
        Assert.NotSame(original, recovered);
        Assert.True(original.ChangeToken.HasChanged);
        Assert.False(recovered.ChangeToken.HasChanged);
        Assert.Equal(2, Assert.Single(recovered.Routes).Order);
        YarpConfigurationTestSupport.AssertCodes(capture, "proxy.config_source_loading");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void NotificationFailure_ShouldKeepNewSnapshotAndDisposeSubscription(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var config = Configuration("1");
        using var services = Services(capture, config);
        var provider = services.GetRequiredService<IProxyConfigProvider>();
        var original = provider.GetConfig();
        capture.Clear();
        var callbacks = 0;
        using var subscription = original.ChangeToken.RegisterChangeCallback(_ =>
        {
            callbacks++;
            throw new InvalidOperationException(HttpBoundaryCapture.Secret);
        }, null);
        config["Routes:route:Order"] = "2";
        config.Reload();
        Assert.Equal(1, callbacks);
        var current = provider.GetConfig();
        Assert.NotSame(original, current);
        Assert.Equal(2, Assert.Single(current.Routes).Order);
        Assert.True(original.ChangeToken.HasChanged);
        YarpConfigurationTestSupport.AssertCodes(capture, "proxy.config_source_loading", "proxy.config_source_notification_failed");
        capture.Clear();

        ((IDisposable)provider).Dispose();
        config["Routes:route:Order"] = "3";
        config.Reload();
        Assert.Same(current, provider.GetConfig());
        Assert.Empty(capture.Events);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Validator_ShouldWarnForHttp10ButAcceptItAndRejectUnsupportedVersion(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var fixture = new YarpConfigurationTestSupport(capture);
        var validator = fixture.Services.GetRequiredService<IConfigValidator>();
        var cluster = YarpConfigurationTestSupport.Cluster();
        Assert.Empty(await validator.ValidateClusterAsync(cluster));
        Assert.Empty(capture.Events);
        foreach (var version in new[] { HttpVersion.Version10, HttpVersion.Version11, HttpVersion.Version20, HttpVersion.Version30 })
        {
            Assert.Empty(await validator.ValidateClusterAsync(cluster with { HttpRequest = new ForwarderRequestConfig { Version = version } }));
            YarpConfigurationTestSupport.AssertCodes(capture, version == HttpVersion.Version10 ? ["proxy.config_validator_http10_detected"] : []);
            if (version == HttpVersion.Version10) Assert.Equal("Warning", Assert.Single(capture.Events).Level);
            capture.Clear();
        }
        var errors = await validator.ValidateClusterAsync(cluster with { HttpRequest = new ForwarderRequestConfig { Version = new Version(4, 0) } });
        Assert.IsType<ArgumentException>(Assert.Single(errors));
        Assert.Empty(capture.Events);
    }

    private static IConfigurationRoot Configuration(string order) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Routes:route:Order"] = order,
        ["Routes:route:Match:Path"] = "/" + HttpBoundaryCapture.Secret,
        ["Routes:route:ClusterId"] = HttpBoundaryCapture.Secret,
        [$"Clusters:{HttpBoundaryCapture.Secret}:Destinations:destination:Address"] = "https://example.invalid/" + HttpBoundaryCapture.Secret
    }).Build();

    private static ServiceProvider Services(HttpBoundaryCapture capture, IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(capture.Factory);
        services.AddReverseProxy().LoadFromConfig(config);
        // 只解析配置 provider，不解析 manager / health monitor，也不创建宿主。
        return services.BuildServiceProvider();
    }
}
