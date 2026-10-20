using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Radish.Extension.Log;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OpenIddictCoreOutputFailureTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SinkFailures_ShouldKeepManagerCatchBoundariesWithAndWithoutProtection(bool audit, bool alwaysThrow)
    {
        foreach (var protect in new[] { false, true })
        foreach (var operation in new[] { "redeem", "reject", "revoke", "authorization" })
        {
            var sink = new FailingSink(alwaysThrow);
            var configuration = new LoggerConfiguration().MinimumLevel.Verbose();
            ILogEventSink output = protect ? new OpenIddictLogSink(sink) : sink;
            if (audit) configuration.AuditTo.Sink(output);
            else configuration.WriteTo.Sink(output);
            using var logger = configuration.CreateLogger();
            var bridge = new Serilog.Extensions.Logging.SerilogLoggerFactory(logger, dispose: false);
            using ILoggerFactory factory = protect ? new OpenIddictLoggerFactory(bridge) : bridge;
            var boundary = new OpenIddictCoreMutationFixture(factory);
            if (audit && alwaysThrow)
            {
                var exception = Assert.IsType<AggregateException>(await Record.ExceptionAsync(async () => await boundary.Mutate(operation, TestContext.Current.CancellationToken)));
                Assert.Same(sink.Failure, Assert.Single(exception.Flatten().InnerExceptions));
            }
            else Assert.Equal(!audit, await boundary.Mutate(operation, TestContext.Current.CancellationToken));
            Assert.Equal(1, boundary.Writes);
            Assert.Equal(audit ? 2 : 1, sink.Attempts);
            Assert.Equal(audit ? 2 : 1, boundary.IdReads);
            Assert.Equal(audit ? LogEventLevel.Warning : LogEventLevel.Information, sink.Last!.Level);
            if (protect)
            {
                Assert.Null(sink.Last.Exception);
                if (audit) Assert.Equal("aggregate", ((ScalarValue)sink.Last.Properties["failureKind"]).Value);
                Assert.DoesNotContain(HttpBoundaryCapture.Secret, sink.Last.RenderMessage() + string.Join(" ", sink.Last.Properties));
                Assert.Equal(OpenIddictCoreEvents.All.Single(row => row.Id == OpenIddictCoreMutationLoggingTests.FirstId(operation) + (audit ? 2 : 0)).Code,
                    ((ScalarValue)sink.Last.Properties["EventCode"]).Value);
            }
        }
    }

    [Fact]
    public async Task DisabledLogger_ShouldNotSkipFrameworkIdentifierReadsOrAlterTheirFailures()
    {
        using var logger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();
        using var factory = new OpenIddictLoggerFactory(new Serilog.Extensions.Logging.SerilogLoggerFactory(logger, dispose: false));
        foreach (var operation in new[] { "redeem", "reject", "revoke", "authorization" })
        {
            var boundary = new OpenIddictCoreMutationFixture(factory, "id-always");
            Assert.Same(boundary.Failure, await Record.ExceptionAsync(async () => await boundary.Mutate(operation, TestContext.Current.CancellationToken)));
            Assert.Equal(1, boundary.Writes);
            Assert.Equal(2, boundary.IdReads);
        }
    }

    private sealed class FailingSink(bool alwaysThrow) : ILogEventSink
    {
        internal readonly IOException Failure = new(HttpBoundaryCapture.Secret);
        internal int Attempts;
        internal LogEvent? Last;
        public void Emit(LogEvent logEvent)
        {
            Last = logEvent;
            Attempts++;
            if (Attempts == 1 || alwaysThrow) throw Failure;
        }
    }
}
