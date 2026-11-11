using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Radish.Api.Tests.ErrorHandling;
using Radish.Model.Models;
using Radish.Service;
using Serilog.Events;
using Xunit;
using static Radish.Api.Tests.ShopOutputAssertions;

namespace Radish.Api.Tests;

public sealed partial class PaymentPasswordLoggingTests
{
    [Theory]
    [MemberData(nameof(ShopOutputLoggingTests.OutputScopes), MemberType = typeof(ShopOutputLoggingTests))]
    public async Task FinalOutput_ShouldKeepNamedIdentityCountsAndSuggestionFallback(bool candidate, string environment, bool audit, bool mel)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute(capture, mel);
        using var activity = new Activity("payment-results").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var f = new Fixture(capture.Logger);
        Assert.Equal(0, await f.Service.ClearExpiredLocksAsync());
        Assert.Equal(3, (await f.Service.GenerateSecuritySuggestionsAsync(7)).Count);
        Assert.Empty(capture.Events);
        f.Repository.Setup(r => r.ClearExpiredLocksAsync(Now)).ReturnsAsync(3);
        Assert.Equal(3, await f.Service.ClearExpiredLocksAsync());
        Check(capture, new Expected(typeof(PaymentPasswordService), "payment.locks_cleared", LogEventLevel.Information, null, ("processedCount", 3)));
        f.Repository.Setup(r => r.GetByUserIdAsync(7)).ThrowsAsync(new IOException(Secret));
        Assert.Equal("无法获取安全建议，请稍后重试", Assert.Single(await f.Service.GenerateSecuritySuggestionsAsync(7)));
        Check(capture, new Expected(typeof(PaymentPasswordService), "payment.suggestions_failed", LogEventLevel.Error, "io"));
        f.Repository.SetupSequence(r => r.GetByUserIdAsync(7)).ReturnsAsync((UserPaymentPassword?)null).ThrowsAsync(new IOException(Secret));
        var status = await f.Service.GetPaymentPasswordStatusAsync(7);
        Assert.NotNull(status);
        Assert.False(status.VoHasPaymentPassword);
        Assert.Equal("无法获取安全建议，请稍后重试", Assert.Single(status.VoSecuritySuggestions));
        Check(capture, new Expected(typeof(PaymentPasswordService), "payment.suggestions_failed", LogEventLevel.Error, "io"));
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task FinalOutput_ShouldPreserveOutputFailureAfterCleanupAndBeforeFallback(bool candidate, string environment, bool audit)
    {
        using var broken = new HangfireBrokenWriter();
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit, output: broken);
        using var global = new ApplicationLogScope(capture.Logger);
        var outputFailure = new IOException(Secret);
        capture.Sink.EmitFailure = outputFailure;
        using var f = new Fixture(capture.Logger);
        f.Repository.Setup(r => r.ClearExpiredLocksAsync(Now)).ReturnsAsync(3);
        if (audit) await AssertAuditFailure(() => f.Service.ClearExpiredLocksAsync(), outputFailure);
        else Assert.Equal(3, await f.Service.ClearExpiredLocksAsync());
        f.Repository.Verify(r => r.ClearExpiredLocksAsync(Now), Times.Once);
        f.Repository.Setup(r => r.GetByUserIdAsync(7)).ThrowsAsync(new IOException("suggestions"));
        if (audit) await AssertAuditFailure(() => f.Service.GenerateSecuritySuggestionsAsync(7), outputFailure);
        else Assert.Equal("无法获取安全建议，请稍后重试", Assert.Single(await f.Service.GenerateSecuritySuggestionsAsync(7)));
        AssertOutputFailed(capture);
    }
}
