using System;
using Radish.Auth.Models;
using Radish.Auth.OpenIddict;
using Xunit;

namespace Radish.Api.Tests.Auth;

public class IdleSessionPolicyTest
{
    [Fact]
    public void Evaluate_Should_Allow_Missing_LastActive_For_Legacy_RefreshToken()
    {
        var options = new IdleSessionOptions { Enable = true, IdleTimeoutDays = 7 };
        var now = DateTimeOffset.Parse("2026-06-10T12:00:00Z");

        var result = IdleSessionPolicy.Evaluate(null, null, now, options);

        Assert.False(result.IsExpired);
        Assert.Null(result.EffectiveLastActiveAt);
    }

    [Fact]
    public void Evaluate_Should_Expire_When_Effective_LastActive_Exceeds_Idle_Window()
    {
        var options = new IdleSessionOptions
        {
            Enable = true,
            IdleTimeoutDays = 7,
            ClockSkewSeconds = 60
        };
        var now = DateTimeOffset.Parse("2026-06-10T12:00:00Z");
        var lastActiveAt = now.AddDays(-7).AddSeconds(-61).ToUnixTimeSeconds();

        var result = IdleSessionPolicy.Evaluate(lastActiveAt, null, now, options);

        Assert.True(result.IsExpired);
        Assert.Equal(lastActiveAt, result.EffectiveLastActiveAt);
    }

    [Fact]
    public void Evaluate_Should_Use_Client_LastActive_When_It_Is_Newer_Than_Stored_Value()
    {
        var options = new IdleSessionOptions
        {
            Enable = true,
            IdleTimeoutDays = 7,
            ClockSkewSeconds = 60
        };
        var now = DateTimeOffset.Parse("2026-06-10T12:00:00Z");
        var storedLastActiveAt = now.AddDays(-8).ToUnixTimeSeconds();
        var clientLastActiveAt = now.AddHours(-1).ToUnixTimeSeconds();

        var result = IdleSessionPolicy.Evaluate(storedLastActiveAt, clientLastActiveAt, now, options);

        Assert.False(result.IsExpired);
        Assert.Equal(clientLastActiveAt, result.EffectiveLastActiveAt);
    }

    [Fact]
    public void NormalizeClientActivityUnixTimeSeconds_Should_Clamp_Future_Values()
    {
        var options = new IdleSessionOptions { ClientActivityFutureToleranceSeconds = 60 };
        var now = DateTimeOffset.Parse("2026-06-10T12:00:00Z");
        var future = now.AddMinutes(5).ToUnixTimeSeconds().ToString();

        var normalized = IdleSessionPolicy.NormalizeClientActivityUnixTimeSeconds(future, now, options);

        Assert.Equal(now.ToUnixTimeSeconds(), normalized);
    }

    [Fact]
    public void ResolveNextLastActiveAt_Should_Seed_Now_When_No_Activity_Exists()
    {
        var now = DateTimeOffset.Parse("2026-06-10T12:00:00Z");

        var resolved = IdleSessionPolicy.ResolveNextLastActiveAt(null, null, now);

        Assert.Equal(now.ToUnixTimeSeconds(), resolved);
    }
}
