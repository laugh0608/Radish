using System;
using System.Data;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.LogTool;
using Radish.Extension.SqlSugarExtension;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class RuntimeLogIngestOutputTests
{
    [Theory]
    [InlineData(false, "Development", false)]
    [InlineData(false, "Production", false)]
    [InlineData(false, "Development", true)]
    [InlineData(true, "Development", false)]
    [InlineData(true, "Production", false)]
    public void SharedLoggingConfiguration_ShouldExcludeIngestionWithoutSuppressingOutsideEvents(bool candidate, string mode, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, mode, audit: audit);
        using (new RuntimeLogIngestScope()) capture.Logger.Error(new IOException("PRIVATE"), "PRIVATE");
        capture.Factory.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics")
            .LogInformation(new EventId(1), "Request started {Path}", "/internal/logs/ingest");
        capture.Factory.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics")
            .LogInformation(new EventId(2), "Request finished {Path}", "/internal/logs/ingest");
        Assert.Empty(capture.Events);
        capture.Logger.ForContext("EventCode", "runtime.started").Information("safe lifecycle");
        Assert.Single(capture.Events);
        capture.AssertSafe();
    }

    [Fact]
    public void SqlitePragmaFailure_ShouldPreserveFalseAndAvoidMainLogger()
    {
        using var capture = new HttpBoundaryCapture(false, "Production");
        var previous = Log.Logger;
        Log.Logger = capture.Logger;
        var previousError = Console.Error;
        using var emergency = new StringWriter();
        Console.SetError(emergency);
        try
        {
            var connection = new Mock<IDbConnection>();
            connection.Setup(c => c.CreateCommand()).Throws(new IOException("PRIVATE"));
            var execute = typeof(SqlSugarSetup).GetMethod("ExecuteSqlitePragma", BindingFlags.NonPublic | BindingFlags.Static)!;
            Assert.Equal(false, execute.Invoke(null, [connection.Object, "PRAGMA busy_timeout=1000", false]));
            Assert.Empty(capture.Events);
            Assert.DoesNotContain("PRIVATE", emergency.ToString());
        }
        finally { Console.SetError(previousError); Log.Logger = previous; }
    }
}
