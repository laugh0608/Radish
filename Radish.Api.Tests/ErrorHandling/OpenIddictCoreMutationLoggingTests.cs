using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OpenIddictCoreMutationLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Mutations_ShouldPreserveValidationCacheStoreAndFailureBoundaries(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        using var scope = capture.SensitiveScope();
        using var cancellation = new CancellationTokenSource();
        foreach (var operation in new[] { "redeem", "reject", "revoke", "authorization" })
        foreach (var fault in new[] { "none", "concurrency", "update-io", "update-cancel", "cache-remove", "cache-add", "id-once", "id-always", "id-cancel", "set-status", "validation", "get-type", "secondary-id", "fatal" })
        {
            var boundary = new OpenIddictCoreMutationFixture(fixture.Factory, fault);
            var throws = fault is "id-always" or "id-cancel" or "set-status" or "secondary-id" or "fatal";
            if (throws)
            {
                var exception = await Record.ExceptionAsync(async () => await boundary.Mutate(operation, cancellation.Token));
                Assert.Same(fault == "secondary-id" ? boundary.SecondaryFailure : boundary.Failure, exception);
                OpenIddictCoreEvents.AssertEvents(capture);
            }
            else
            {
                Assert.Equal(fault == "none", await boundary.Mutate(operation, cancellation.Token));
                var firstId = FirstId(operation);
                OpenIddictCoreEvents.AssertEvents(capture, firstId + (fault == "none" ? 0 : fault == "concurrency" ? 1 : 2));
                if (fault != "none" && capture.Events.Length > 0)
                    Assert.Equal(fault == "update-cancel" ? "cancelled" : fault == "validation" || fault == "concurrency" ? "other" : "io", Assert.Single(capture.Events).FailureKind);
            }
            var written = fault is "none" or "cache-add" or "id-once" or "id-always" or "id-cancel";
            Assert.Equal(written ? 1 : 0, boundary.Writes);
            var target = operation == "redeem" ? "redeemed" : operation == "reject" ? "rejected" : "revoked";
            Assert.Equal(fault == "set-status" ? "valid" : target, boundary.Entity.Status);
            Assert.Equal(written ? target : null, boundary.PersistedStatus);
            Assert.Equal(operation == "redeem" ? OpenIddictCoreMutationFixture.Now : (DateTimeOffset?)null, boundary.Entity.RedemptionDate);
            Assert.All(boundary.Tokens, token => Assert.Equal(cancellation.Token, token));
            if (fault == "none")
            {
                var expected = new List<string>();
                if (operation == "redeem") expected.AddRange(new[] { "get-date", "set-date" });
                if (operation == "authorization") expected.Add("get-status");
                expected.AddRange(new[] { "set-status", "get-type", "get-status", "cache-remove", "update", "cache-add", "id" });
                Assert.Equal(expected, boundary.Calls);
            }
            if (fault is "id-once" or "id-always" or "id-cancel") Assert.Equal(2, boundary.IdReads);
            if (fault == "cache-remove") Assert.DoesNotContain("update", boundary.Calls);
            if (fault == "cache-add") Assert.True(boundary.Calls.IndexOf("update") < boundary.Calls.IndexOf("cache-add"));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task EarlyFailuresExistingDatesAndAlreadyRevoked_ShouldKeepOriginalShortcuts(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var fault in new[] { "get-date", "set-date" })
        {
            var boundary = new OpenIddictCoreMutationFixture(fixture.Factory, fault);
            Assert.Same(boundary.Failure, await Record.ExceptionAsync(async () => await boundary.Mutate("redeem", TestContext.Current.CancellationToken)));
            Assert.Equal("valid", boundary.Entity.Status);
            Assert.Equal(0, boundary.Writes);
            OpenIddictCoreEvents.AssertEvents(capture);
        }
        var readFailure = new OpenIddictCoreMutationFixture(fixture.Factory, "get-status");
        Assert.Same(readFailure.Failure, await Record.ExceptionAsync(async () => await readFailure.Mutate("authorization", TestContext.Current.CancellationToken)));
        Assert.Equal("valid", readFailure.Entity.Status);
        Assert.Equal(0, readFailure.Writes);
        OpenIddictCoreEvents.AssertEvents(capture);
        var already = new OpenIddictCoreMutationFixture(fixture.Factory);
        already.Entity.Status = "ReVoKeD";
        Assert.True(await already.Mutate("authorization", TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "get-status" }, already.Calls);
        Assert.Equal(0, already.Writes);
        OpenIddictCoreEvents.AssertEvents(capture);
        foreach (var operation in new[] { "redeem", "reject", "revoke", "authorization" })
        {
            var boundary = new OpenIddictCoreMutationFixture(fixture.Factory, disableCache: true);
            boundary.Entity.RedemptionDate = OpenIddictCoreMutationFixture.Now.AddDays(-1);
            Assert.True(await boundary.Mutate(operation, TestContext.Current.CancellationToken));
            Assert.DoesNotContain("cache-remove", boundary.Calls);
            Assert.DoesNotContain("cache-add", boundary.Calls);
            Assert.DoesNotContain("set-date", boundary.Calls);
            Assert.Equal(OpenIddictCoreMutationFixture.Now.AddDays(-1), boundary.Entity.RedemptionDate);
            OpenIddictCoreEvents.AssertEvents(capture, FirstId(operation));
            capture.Clear();
        }
    }

    internal static int FirstId(string operation) => operation switch
    {
        "redeem" => 6168, "reject" => 6171, "revoke" => 6174, "authorization" => 6164,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
}
