using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radish.Api.Services;
using Radish.Extension.Log;
using Radish.Common.OptionTool;
using Radish.IService;
using Radish.Model;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class ProgramDocumentSyncLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Guard_ShouldKeepBothTableQueriesAndSkipBeforeServiceResolution(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        using var scope = capture.SensitiveScope();
        foreach (var show in new[] { false, true })
        foreach (var document in new[] { false, true })
        foreach (var revision in new[] { false, true })
        {
            if (show && document && revision) continue;
            using var db = Database(document, revision);
            var queries = 0;
            db.Aop.OnLogExecuting = (_, _) => queries++;
            var resolutions = 0;
            using var provider = new ServiceCollection().AddSingleton<ISqlSugarClient>(db)
                .AddScoped<IWikiDocumentService>(_ => { resolutions++; throw new StartupUnreadableException(); }).BuildServiceProvider();
            await WikiBuiltInDocumentStartupSync.RunAsync(provider, new DocumentOptions { ShowBuiltInDocs = show }, capture.Logger);
            Assert.Equal(2, queries); // Show=false still evaluates both original IsAnyTable arguments.
            Assert.Equal(0, resolutions);
            Assert.Equal("document.builtin_sync_skipped", Assert.Single(capture.Events).Code);
            var expected = !show ? "disabled" : !document && !revision ? "both-tables-missing"
                : !document ? "document-table-missing" : "revision-table-missing";
            Assert.Equal(expected, HostProgramLoggingTests.Properties(capture)["builtInSyncSkipReason"]);
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task SyncResult_ShouldBeObservedAfterReturnAndPreserveScopeDisposal(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        using var scope = capture.SensitiveScope();
        using var db = Database();
        foreach (var reason in new[] { WikiBuiltInSyncSkipReason.None, WikiBuiltInSyncSkipReason.Disabled,
            WikiBuiltInSyncSkipReason.DirectoryMissing, WikiBuiltInSyncSkipReason.NoMarkdown, (WikiBuiltInSyncSkipReason)999 })
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<WikiBuiltInSyncSummary>(TaskCreationOptions.RunContinuationsAsynchronously);
            var service = new Mock<IWikiDocumentService>();
            var disposable = service.As<IDisposable>();
            service.Setup(s => s.SyncBuiltInDocumentsAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) =>
            {
                Assert.Equal(default, token); // Keep the original default token, not a new host cancellation policy.
                entered.SetResult();
                return release.Task;
            });
            using var provider = new ServiceCollection().AddSingleton<ISqlSugarClient>(db)
                .AddScoped<IWikiDocumentService>(_ => service.Object).BuildServiceProvider();
            var run = WikiBuiltInDocumentStartupSync.RunAsync(provider, new DocumentOptions { ShowBuiltInDocs = true }, capture.Logger);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Assert.Empty(capture.Events);
                Assert.False(run.IsCompleted);
            }
            finally
            {
                release.TrySetResult(new WikiBuiltInSyncSummary { IsSkipped = reason != WikiBuiltInSyncSkipReason.None,
                    SkipReason = reason, SyncedCount = 2, CreatedCount = 1 });
            }
            await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(reason == WikiBuiltInSyncSkipReason.None ? "document.builtin_sync_completed" : "document.builtin_sync_skipped",
                Assert.Single(capture.Events).Code);
            if (reason == WikiBuiltInSyncSkipReason.None) Assert.Equal("2", HostProgramLoggingTests.Properties(capture)["syncedCount"]);
            if ((int)reason == 999) Assert.Equal("unknown", HostProgramLoggingTests.Properties(capture)["builtInSyncSkipReason"]);
            disposable.Verify(s => s.Dispose(), Times.Once);
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Failures_ShouldRemainConsumedWithoutClaimingRollbackOrCompletion(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false);
        using var scope = capture.SensitiveScope();
        using var db = Database();
        foreach (var exception in new Exception[] { new StartupUnreadableException(), new OperationCanceledException(HttpBoundaryCapture.Secret) })
        {
            var changed = false;
            var service = new Mock<IWikiDocumentService>();
            var disposable = service.As<IDisposable>();
            service.Setup(s => s.SyncBuiltInDocumentsAsync(It.IsAny<CancellationToken>())).Returns(() =>
            { changed = true; return Task.FromException<WikiBuiltInSyncSummary>(exception); });
            using var provider = new ServiceCollection().AddSingleton<ISqlSugarClient>(db)
                .AddScoped<IWikiDocumentService>(_ => service.Object).BuildServiceProvider();
            await WikiBuiltInDocumentStartupSync.RunAsync(provider, new DocumentOptions { ShowBuiltInDocs = true }, capture.Logger);
            Assert.True(changed); // The existing consumer does not introduce compensating work.
            Assert.Equal("document.builtin_sync_failed", Assert.Single(capture.Events).Code);
            Assert.Equal(exception is OperationCanceledException ? "cancelled" : "other", capture.Events[0].FailureKind);
            disposable.Verify(s => s.Dispose(), Times.Once);
            capture.AssertSafe();
            capture.Clear();
        }
        await WikiBuiltInDocumentStartupSync.RunAsync(new ThrowingScopeProvider(), new DocumentOptions { ShowBuiltInDocs = true }, capture.Logger);
        Assert.Equal("document.builtin_sync_failed", Assert.Single(capture.Events).Code);
        capture.AssertSafe();
        capture.Clear();
        using var resolving = new ServiceCollection().AddSingleton<ISqlSugarClient>(db)
            .AddScoped<IWikiDocumentService>(_ => throw new StartupUnreadableException()).BuildServiceProvider();
        await WikiBuiltInDocumentStartupSync.RunAsync(resolving, new DocumentOptions { ShowBuiltInDocs = true }, capture.Logger);
        Assert.Equal("document.builtin_sync_failed", Assert.Single(capture.Events).Code);
        capture.AssertSafe();
        capture.Clear();
        db.Aop.OnLogExecuting = (_, _) => throw new StartupUnreadableException();
        await WikiBuiltInDocumentStartupSync.RunAsync(resolving, new DocumentOptions { ShowBuiltInDocs = false }, capture.Logger);
        Assert.Equal("document.builtin_sync_failed", Assert.Single(capture.Events).Code);
        capture.AssertSafe();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputFailure_ShouldKeepExistingCatchAndAuditBehavior(bool audit)
    {
        using var db = Database();
        var service = new Mock<IWikiDocumentService>();
        service.Setup(s => s.SyncBuiltInDocumentsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new WikiBuiltInSyncSummary());
        using var provider = new ServiceCollection().AddSingleton<ISqlSugarClient>(db)
            .AddScoped<IWikiDocumentService>(_ => service.Object).BuildServiceProvider();
        var failure = new IOException(HttpBoundaryCapture.Secret);
        var sink = new ProgramThrowingSink(failure);
        var configuration = new LoggerConfiguration();
        if (audit) configuration.AuditTo.Sink(new HostProgramLogSink(sink));
        else configuration.WriteTo.Sink(new HostProgramLogSink(sink));
        using var logger = configuration.CreateLogger();
        if (audit) Assert.Contains(failure, (await Assert.ThrowsAsync<AggregateException>(() => WikiBuiltInDocumentStartupSync.RunAsync(provider, new DocumentOptions { ShowBuiltInDocs = true }, logger))).Flatten().InnerExceptions);
        else await WikiBuiltInDocumentStartupSync.RunAsync(provider, new DocumentOptions { ShowBuiltInDocs = true }, logger);
        Assert.Equal(audit ? 2 : 1, sink.Attempts); // A throwing completion audit is still caught by the existing startup catch.
    }

    [Fact]
    public async Task ScopeDisposalFailure_ShouldStillBeConsumedAfterReturnedSummary()
    {
        using var capture = new HttpBoundaryCapture(true, "Production");
        using var db = Database();
        var service = new Mock<IWikiDocumentService>();
        service.As<IDisposable>().Setup(s => s.Dispose()).Throws(new StartupUnreadableException());
        service.Setup(s => s.SyncBuiltInDocumentsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new WikiBuiltInSyncSummary());
        using var provider = new ServiceCollection().AddSingleton<ISqlSugarClient>(db)
            .AddScoped<IWikiDocumentService>(_ => service.Object).BuildServiceProvider();
        await WikiBuiltInDocumentStartupSync.RunAsync(provider, new DocumentOptions { ShowBuiltInDocs = true }, capture.Logger);
        Assert.Equal(new[] { "document.builtin_sync_completed", "document.builtin_sync_failed" }, capture.Codes);
        capture.AssertSafe();
    }

    private static SqlSugarClient Database(bool document = true, bool revision = true)
    {
        var db = new SqlSugarClient(new ConnectionConfig { DbType = DbType.Sqlite,
            ConnectionString = "Data Source=:memory:", IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute });
        if (document) db.Ado.ExecuteCommand("CREATE TABLE WikiDocument (Id INTEGER)");
        if (revision) db.Ado.ExecuteCommand("CREATE TABLE WikiDocumentRevision (Id INTEGER)");
        return db;
    }

    private sealed class ThrowingScopeProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => throw new StartupUnreadableException();
    }
}
