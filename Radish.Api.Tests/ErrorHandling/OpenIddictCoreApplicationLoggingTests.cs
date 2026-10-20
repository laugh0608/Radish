using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OpenIddictCoreApplicationLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ClientSecrets_ShouldKeepResultsAndBothMalformedHashEvents(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        using var scope = capture.SensitiveScope();
        foreach (var kind in new[] { "public", "missing", "malformed", "mismatch", "valid" })
        {
            var entity = new CoreLogEntity();
            var store = Store(entity);
            store.Setup(s => s.GetClientTypeAsync(entity, It.IsAny<CancellationToken>())).ReturnsAsync(kind == "public" ? "public" : "confidential");
            var manager = new ApplicationManager(fixture.Factory, store.Object);
            var hash = kind is "valid" or "mismatch" ? await manager.Hash(HttpBoundaryCapture.Secret) : kind == "malformed" ? "not base64!" : null;
            store.Setup(s => s.GetClientSecretAsync(entity, It.IsAny<CancellationToken>())).ReturnsAsync(hash);
            Assert.Equal(kind == "valid", await manager.ValidateClientSecretAsync(entity, kind == "mismatch" ? "different" : HttpBoundaryCapture.Secret, TestContext.Current.CancellationToken));
            OpenIddictCoreEvents.AssertEvents(capture, kind switch
            {
                "public" => [6159], "missing" => [6160], "malformed" => [6163, 6161], "mismatch" => [6161], _ => []
            });
            store.Verify(s => s.GetClientSecretAsync(entity, It.IsAny<CancellationToken>()), kind == "public" ? Times.Never() : Times.Once());
            store.Verify(s => s.GetClientIdAsync(entity, It.IsAny<CancellationToken>()), kind is "public" or "valid" ? Times.Never() : Times.Once());
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Redirects_ShouldKeepExactAndNativeLoopbackRules(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var logout in new[] { false, true })
        foreach (var native in new[] { false, true })
        foreach (var (uri, exact, relaxed) in new[]
        {
            ("http://127.0.0.1/cb", true, true), ("http://127.0.0.1:49152/cb", false, true),
            ("http://127.0.0.1:49152/CB", false, false), ("http://127.0.0.1:49152/cb?" + HttpBoundaryCapture.Secret, false, false),
            ("http://127.0.0.1:49152/cb#" + HttpBoundaryCapture.Secret, false, false),
            ("https://127.0.0.1:49152/cb", false, false), ("http://localhost:49152/cb", false, false),
            ("http://" + HttpBoundaryCapture.Secret + "@127.0.0.1:49152/cb", false, false),
            ("https://example.invalid/" + HttpBoundaryCapture.Secret, false, false)
        })
        {
            var entity = new CoreLogEntity();
            var store = Store(entity);
            store.Setup(s => s.GetApplicationTypeAsync(entity, It.IsAny<CancellationToken>())).ReturnsAsync(native ? "native" : "web");
            store.Setup(s => s.GetRedirectUrisAsync(entity, It.IsAny<CancellationToken>())).ReturnsAsync(ImmutableArray.Create("http://127.0.0.1/cb"));
            store.Setup(s => s.GetPostLogoutRedirectUrisAsync(entity, It.IsAny<CancellationToken>())).ReturnsAsync(ImmutableArray.Create("http://127.0.0.1/cb"));
            var manager = new ApplicationManager(fixture.Factory, store.Object);
            var expected = exact || native && relaxed;
            Assert.Equal(expected, logout ? await manager.ValidatePostLogoutRedirectUriAsync(entity, uri, TestContext.Current.CancellationToken) : await manager.ValidateRedirectUriAsync(entity, uri, TestContext.Current.CancellationToken));
            OpenIddictCoreEvents.AssertEvents(capture, expected ? [] : [logout ? 6202 : 6162]);
            store.Verify(s => s.GetClientIdAsync(entity, It.IsAny<CancellationToken>()), expected ? Times.Never() : Times.Once());
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task PreLogIdentifierReadFailures_ShouldPropagateWithoutInventingRejectionEvents(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var cancelled in new[] { false, true })
        foreach (var kind in new[] { "missing", "malformed", "redirect", "logout" })
        {
            using var cancellation = new CancellationTokenSource();
            var entity = new CoreLogEntity();
            var store = Store(entity);
            Exception failure = cancelled ? new OperationCanceledException(HttpBoundaryCapture.Secret) : new IOException(HttpBoundaryCapture.Secret);
            store.Setup(s => s.GetClientIdAsync(entity, cancellation.Token)).Throws(failure);
            store.Setup(s => s.GetClientTypeAsync(entity, cancellation.Token)).ReturnsAsync("confidential");
            store.Setup(s => s.GetClientSecretAsync(entity, cancellation.Token)).ReturnsAsync(kind == "malformed" ? "not base64!" : null);
            store.Setup(s => s.GetRedirectUrisAsync(entity, cancellation.Token)).ReturnsAsync(ImmutableArray<string>.Empty);
            store.Setup(s => s.GetPostLogoutRedirectUrisAsync(entity, cancellation.Token)).ReturnsAsync(ImmutableArray<string>.Empty);
            var manager = new ApplicationManager(fixture.Factory, store.Object);
            var exception = await Record.ExceptionAsync(async () =>
            {
                if (kind == "redirect") await manager.ValidateRedirectUriAsync(entity, "https://example.invalid/" + HttpBoundaryCapture.Secret, cancellation.Token);
                else if (kind == "logout") await manager.ValidatePostLogoutRedirectUriAsync(entity, "https://example.invalid/" + HttpBoundaryCapture.Secret, cancellation.Token);
                else await manager.ValidateClientSecretAsync(entity, HttpBoundaryCapture.Secret, cancellation.Token);
            });
            Assert.Same(failure, exception);
            OpenIddictCoreEvents.AssertEvents(capture, kind == "malformed" ? [6163] : []);
            store.Verify(s => s.GetClientIdAsync(entity, cancellation.Token), Times.Once());
            capture.Clear();
        }
    }

    private static Mock<IOpenIddictApplicationStore<CoreLogEntity>> Store(CoreLogEntity entity)
    {
        var store = new Mock<IOpenIddictApplicationStore<CoreLogEntity>>(MockBehavior.Strict);
        store.Setup(s => s.GetClientIdAsync(entity, It.IsAny<CancellationToken>())).ReturnsAsync(HttpBoundaryCapture.Secret);
        return store;
    }
    private sealed class ApplicationManager(ILoggerFactory factory, IOpenIddictApplicationStore<CoreLogEntity> store)
        : OpenIddictApplicationManager<CoreLogEntity>(Mock.Of<IOpenIddictApplicationCache<CoreLogEntity>>(),
            factory.CreateLogger<OpenIddictApplicationManager<CoreLogEntity>>(), new OpenIddictOptions<OpenIddictCoreOptions>(new()), store)
    {
        internal ValueTask<string> Hash(string value) => ObfuscateClientSecretAsync(value);
    }
}
