using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Xunit;
using static Radish.Api.Tests.ErrorHandling.YarpConfigurationTestSupport;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class YarpConfigurationManagerTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task InitialLoadFailure_ShouldWrapOriginalExceptionAndNotEmitReloadFailure(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var fixture = new YarpConfigurationTestSupport(capture);
        var failure = new IOException(HttpBoundaryCapture.Secret);
        fixture.Provider.LoadFailure = failure;
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(fixture.InitialLoad);
        Assert.Same(failure, thrown.InnerException);
        Assert.Empty(fixture.Clusters());
        Assert.Empty(fixture.Routes());
        Assert.Equal(0, fixture.Monitor.Calls);
        Assert.Equal(0, fixture.Clients.Calls);
        Assert.Empty(capture.Events);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task InitialInvalidConfig_ShouldKeepAggregatedValidationErrorsBeforeAnyUpdate(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var fixture = new YarpConfigurationTestSupport(capture);
        var route = Route();
        var cluster = Cluster();
        fixture.Provider.Current = new ConfigurationSnapshot([route, route with { RouteId = route.RouteId.ToUpperInvariant() }],
            [cluster, cluster with { ClusterId = cluster.ClusterId.ToLowerInvariant() }]);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(fixture.InitialLoad);
        var errors = Assert.IsType<AggregateException>(thrown.InnerException);
        Assert.Contains(errors.InnerExceptions, e => e.Message.Contains("Duplicate route", StringComparison.Ordinal));
        Assert.Contains(errors.InnerExceptions, e => e.Message.Contains("Duplicate cluster", StringComparison.Ordinal));
        Assert.Empty(fixture.Clusters());
        Assert.Empty(fixture.Routes());
        Assert.Equal(0, fixture.Clients.Calls);
        Assert.Equal(0, fixture.Monitor.Calls);
        Assert.Empty(capture.Events);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task AddChangeRemoveAndEquivalentConfig_ShouldPreserveRuntimeStateAndEndpointTokens(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var fixture = new YarpConfigurationTestSupport(capture);
        var snapshots = new List<ConfigurationSnapshot> { fixture.Provider.Current };
        try
        {
            await fixture.InitialLoad();
            Assert.Equal(1, fixture.Monitor.Calls);
            var originalCluster = Assert.Single(fixture.Clusters());
            var originalRoute = Assert.Single(fixture.Routes());
            var originalEndpoints = fixture.DataSource.Endpoints;
            Assert.Single(originalEndpoints);
            var originalToken = fixture.DataSource.GetChangeToken();
            AssertCodes(capture, "proxy.config_manager_destination_added", "proxy.config_manager_cluster_added", "proxy.config_manager_route_added");
            capture.Clear();

            var equal = new ConfigurationSnapshot(snapshots[0].Routes, snapshots[0].Clusters);
            snapshots.Add(equal);
            fixture.Provider.Replace(equal);
            await fixture.Reload();
            Assert.Same(originalCluster, Assert.Single(fixture.Clusters()));
            Assert.Same(originalRoute, Assert.Single(fixture.Routes()));
            Assert.Same(originalEndpoints, fixture.DataSource.Endpoints);
            Assert.False(originalToken.HasChanged);
            Assert.Empty(capture.Events);

            var changedCluster = Cluster(address: "https://example.invalid/" + HttpBoundaryCapture.Secret) with
            { HttpRequest = new ForwarderRequestConfig { Version = HttpVersion.Version11 } };
            var changedRoute = Route(order: 1);
            var changed = Snapshot(changedCluster, changedRoute);
            snapshots.Add(changed);
            fixture.Provider.Replace(changed);
            await fixture.Reload();
            Assert.Same(originalCluster, Assert.Single(fixture.Clusters()));
            Assert.Equal(changedCluster, originalCluster.Model.Config);
            Assert.NotSame(originalRoute, Assert.Single(fixture.Routes()));
            Assert.Equal(1, Assert.Single(fixture.Routes()).Config.Order);
            Assert.NotSame(originalEndpoints, fixture.DataSource.Endpoints);
            Assert.True(originalToken.HasChanged);
            AssertCodes(capture, "proxy.config_manager_destination_changed", "proxy.config_manager_cluster_changed", "proxy.config_manager_route_changed");
            capture.Clear();

            var withoutDestinations = Snapshot(changedCluster with { Destinations = new Dictionary<string, DestinationConfig>() }, changedRoute);
            snapshots.Add(withoutDestinations);
            fixture.Provider.Replace(withoutDestinations);
            await fixture.Reload();
            Assert.Empty(originalCluster.Destinations);
            AssertCodes(capture, "proxy.config_manager_destination_removed");
            capture.Clear();

            var empty = new ConfigurationSnapshot([], []);
            snapshots.Add(empty);
            fixture.Provider.Replace(empty);
            await fixture.Reload();
            Assert.Empty(fixture.Clusters());
            Assert.Empty(fixture.Routes());
            Assert.Empty(fixture.DataSource.Endpoints);
            Assert.Equal(1, fixture.Monitor.Calls); // reload 不新增 initial probe；替身从不发送请求。
            AssertCodes(capture, "proxy.config_manager_cluster_removed", "proxy.config_manager_route_removed");
        }
        finally { fixture.Dispose(); }
        Assert.All(snapshots, snapshot => Assert.Equal(0, snapshot.Token.Subscriptions));
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ReloadLoadFailure_ShouldRetainOldStateAndRecoverWithoutChangingException(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var fixture = new YarpConfigurationTestSupport(capture);
        await fixture.InitialLoad();
        var originalCluster = Assert.Single(fixture.Clusters());
        var originalRoute = Assert.Single(fixture.Routes());
        var originalEndpoints = fixture.DataSource.Endpoints;
        capture.Clear();

        var failure = new IOException(HttpBoundaryCapture.Secret);
        fixture.Provider.Current.Token.MarkChanged();
        fixture.Provider.LoadFailure = failure;
        await fixture.Reload();
        Assert.Same(failure, fixture.Callbacks.LoadingFailure);
        Assert.Same(originalCluster, Assert.Single(fixture.Clusters()));
        Assert.Same(originalRoute, Assert.Single(fixture.Routes()));
        Assert.Same(originalEndpoints, fixture.DataSource.Endpoints);
        AssertCodes(capture, "proxy.config_manager_reload_failed");
        capture.Clear();

        fixture.Provider.LoadFailure = null;
        fixture.Provider.Replace(Snapshot(route: Route(order: 5)));
        await fixture.Reload();
        Assert.Equal(5, Assert.Single(fixture.Routes()).Config.Order);
        Assert.NotSame(originalEndpoints, fixture.DataSource.Endpoints);
        AssertCodes(capture, "proxy.config_manager_route_changed");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ReloadValidationFailure_ShouldLeaveStateUntouchedAndAllowRecovery(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var fixture = new YarpConfigurationTestSupport(capture);
        await fixture.InitialLoad();
        var oldClusterModel = Assert.Single(fixture.Clusters()).Model;
        var oldRoute = Assert.Single(fixture.Routes());
        var endpoints = fixture.DataSource.Endpoints;
        var endpointToken = fixture.DataSource.GetChangeToken();
        var clientCalls = fixture.Clients.Calls;
        capture.Clear();

        var badCluster = Cluster() with { HttpRequest = new ForwarderRequestConfig { Version = new Version(4, 0) } };
        fixture.Provider.Replace(Snapshot(badCluster, Route(order: 6)));
        await fixture.Reload();
        Assert.IsType<AggregateException>(fixture.Callbacks.ApplyingFailure);
        Assert.Same(oldClusterModel, Assert.Single(fixture.Clusters()).Model);
        Assert.Same(oldRoute, Assert.Single(fixture.Routes()));
        Assert.Same(endpoints, fixture.DataSource.Endpoints);
        Assert.False(endpointToken.HasChanged);
        Assert.Equal(clientCalls, fixture.Clients.Calls);
        AssertCodes(capture, "proxy.config_manager_apply_failed");
        capture.Clear();

        fixture.Provider.Replace(Snapshot(route: Route(order: 6)));
        await fixture.Reload();
        Assert.Equal(6, Assert.Single(fixture.Routes()).Config.Order);
        Assert.True(endpointToken.HasChanged);
        AssertCodes(capture, "proxy.config_manager_route_changed");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task LateApplyFailure_ShouldRetainFrameworkPartialUpdateAndOriginalFailure(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var fixture = new YarpConfigurationTestSupport(capture);
        await fixture.InitialLoad();
        var cluster = Assert.Single(fixture.Clusters());
        var oldModel = cluster.Model;
        var oldRoute = Assert.Single(fixture.Routes());
        var endpoints = fixture.DataSource.Endpoints;
        capture.Clear();

        var failure = new InvalidOperationException(HttpBoundaryCapture.Secret);
        fixture.Clients.Failure = failure;
        var address = "https://example.invalid/changed/" + HttpBoundaryCapture.Secret;
        fixture.Provider.Replace(Snapshot(Cluster(address: address), Route(order: 7)));
        await fixture.Reload();
        Assert.Same(failure, fixture.Callbacks.ApplyingFailure);
        Assert.Same(oldModel, cluster.Model);
        Assert.Equal(address, Assert.Single(cluster.Destinations).Value.Model.Config.Address);
        Assert.Same(oldRoute, Assert.Single(fixture.Routes()));
        Assert.Same(endpoints, fixture.DataSource.Endpoints);
        AssertCodes(capture, "proxy.config_manager_destination_changed", "proxy.config_manager_apply_failed");
        capture.Clear();

        fixture.Clients.Failure = null;
        fixture.Provider.Replace(Snapshot(Cluster(address: "https://example.invalid/recovered"), Route(order: 7)));
        await fixture.Reload();
        Assert.Equal("https://example.invalid/recovered", Assert.Single(cluster.Destinations).Value.Model.Config.Address);
        Assert.Equal(7, Assert.Single(fixture.Routes()).Config.Order);
        AssertCodes(capture, "proxy.config_manager_destination_changed", "proxy.config_manager_route_changed");
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ConfigCallbacks_ShouldKeepExistingCaughtAndPropagatedBoundaries(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        using var fixture = new YarpConfigurationTestSupport(capture);
        await fixture.InitialLoad();
        var oldRoute = Assert.Single(fixture.Routes());
        capture.Clear();
        var loadedFailure = new InvalidOperationException(HttpBoundaryCapture.Secret);
        fixture.Callbacks.ThrowOnLoaded = loadedFailure;
        fixture.Provider.Replace(Snapshot(route: Route(order: 8)));
        Assert.Same(loadedFailure, await Assert.ThrowsAsync<InvalidOperationException>(fixture.Reload));
        Assert.Same(oldRoute, Assert.Single(fixture.Routes()));
        Assert.Empty(capture.Events); // ConfigurationLoaded 位于框架 reload 的 apply catch 之外。

        fixture.Callbacks.ThrowOnLoaded = null;
        var appliedFailure = new IOException(HttpBoundaryCapture.Secret);
        fixture.Callbacks.ThrowOnApplied = appliedFailure;
        fixture.Provider.Replace(Snapshot(route: Route(order: 9)));
        await fixture.Reload();
        Assert.Equal(9, Assert.Single(fixture.Routes()).Config.Order);
        Assert.Same(appliedFailure, fixture.Callbacks.ApplyingFailure);
        AssertCodes(capture, "proxy.config_manager_route_changed", "proxy.config_manager_apply_failed");
    }
}
