using System;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Radish.Common.OptionTool;
using Radish.IService;
using Xunit;

namespace Radish.Api.Tests.Services.UploadQuota;

public abstract class UploadQuotaBoundaryTests
{
    protected abstract string Backend { get; }

    [Theory]
    [InlineData(0, 100L)]
    [InlineData(1, 0L)]
    [InlineData(2, 0L)]
    public async Task Complete_ShouldRespectTargetExpiryWithAnotherLiveReservation(int advanceSeconds, long expectedCompleted)
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var service = scope.CreateService();
        Assert.True((await service.AcquireUploadAsync(scope.UserId, "target", 100, TimeSpan.FromSeconds(1))).IsAllowed);
        Assert.True((await service.AcquireUploadAsync(scope.UserId, "live", 200, TimeSpan.FromMinutes(10))).IsAllowed);
        scope.Clock.Advance(TimeSpan.FromSeconds(advanceSeconds));
        await service.CompleteUploadAsync(scope.UserId, "target");
        await service.CompleteUploadAsync(scope.UserId, "target");
        var stats = await service.GetUploadStatisticsAsync(scope.UserId);
        Assert.Equal(expectedCompleted, stats.UploadedSizeToday);
        Assert.Equal(200, stats.ReservedUploadSizeToday);
        Assert.Equal(1, stats.CurrentConcurrentUploads);
        Assert.Equal(2, stats.UploadsThisMinute);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(2147483647L)]
    [InlineData(2147483648L)]
    [InlineData(4000000001L)]
    [InlineData(8000000002L)]
    [InlineData(9007199254740991L)]
    [InlineData(9007199254740992L)]
    [InlineData(9007199254740993L)]
    [InlineData(long.MaxValue - 1)]
    [InlineData(long.MaxValue)]
    public async Task Capacity_ShouldRetainExactAcceptedBytesThroughStatisticsAndSettlement(long bytes)
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var service = scope.CreateService(new UploadRateLimitOptions { MaxDailyUploadSize = bytes });
        Assert.True((await service.AcquireUploadAsync(scope.UserId, "large", bytes)).IsAllowed);
        Assert.Equal(bytes, (await service.GetUploadStatisticsAsync(scope.UserId)).ReservedUploadSizeToday);
        Assert.Equal(bytes, (await service.GetUploadStatisticsAsync(scope.UserId)).ReservedUploadSizeToday);
        Assert.Equal(UploadRateLimitFailureKind.DailyUploadSize,
            (await service.AcquireUploadAsync(scope.UserId, "excess", 1)).FailureKind);
        await service.CompleteUploadAsync(scope.UserId, "large");
        await service.CompleteUploadAsync(scope.UserId, "large");
        var stats = await service.GetUploadStatisticsAsync(scope.UserId);
        Assert.Equal(bytes, stats.UploadedSizeToday);
        Assert.Equal(0, stats.ReservedUploadSizeToday);
        Assert.Equal(bytes, stats.OccupiedUploadSizeToday);
        Assert.Equal(1, stats.UploadsThisMinute);
    }
    [Theory]
    [InlineData(9007199254740991L, 3L)]
    [InlineData(long.MaxValue - 1, 1L)]
    public async Task MultipleReservations_ShouldPreserveExactSumAndRemainingCapacity(long first, long second)
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var total = checked(first + second);
        var service = scope.CreateService(new UploadRateLimitOptions { MaxDailyUploadSize = total });
        Assert.True((await service.AcquireUploadAsync(scope.UserId, "first", first)).IsAllowed);
        Assert.True((await service.AcquireUploadAsync(scope.UserId, "second", second)).IsAllowed);
        Assert.Equal(total, (await service.GetUploadStatisticsAsync(scope.UserId)).ReservedUploadSizeToday);
        await service.CompleteUploadAsync(scope.UserId, "first");
        var stats = await service.GetUploadStatisticsAsync(scope.UserId);
        Assert.Equal(first, stats.UploadedSizeToday);
        Assert.Equal(second, stats.ReservedUploadSizeToday);
        Assert.Equal(total, stats.OccupiedUploadSizeToday);
        Assert.Equal(UploadRateLimitFailureKind.DailyUploadSize,
            (await service.AcquireUploadAsync(scope.UserId, "excess", 1)).FailureKind);
        await service.CompleteUploadAsync(scope.UserId, "second");
        Assert.Equal(total.ToString(CultureInfo.InvariantCulture), await scope.ReadCounterAsync(scope.DailyKey));
    }

    [Fact]
    public async Task HealthyStoredReservation_ShouldSettleDespiteReducedConfiguration()
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        await scope.SeedReservationAsync("old", "9007199254740993", scope.Clock.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds());
        var service = scope.CreateService(new UploadRateLimitOptions { MaxDailyUploadSize = 1 });
        Assert.Equal(9007199254740993L, (await service.GetUploadStatisticsAsync(scope.UserId)).ReservedUploadSizeToday);
        Assert.Equal(UploadRateLimitFailureKind.DailyUploadSize,
            (await service.AcquireUploadAsync(scope.UserId, "new", 1)).FailureKind);
        await service.CompleteUploadAsync(scope.UserId, "old");
        Assert.Equal(9007199254740993L, (await service.GetUploadStatisticsAsync(scope.UserId)).UploadedSizeToday);
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("-1")]
    [InlineData("9.007199254741e+15")]
    [InlineData("9223372036854775808")]
    public async Task InvalidReservation_ShouldRemainVisibleAndNotBePrunedOrConsumeAttempt(string size)
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        await scope.SeedReservationAsync("bad", size, scope.Clock.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds());
        await scope.SeedReservationAsync("expired", "1", scope.Clock.GetUtcNow().AddSeconds(-1).ToUnixTimeSeconds());
        var original = await scope.ReadReservationAsync("bad");
        var service = scope.CreateService();
        await AssertInvalidStateAsync(() => service.GetUploadStatisticsAsync(scope.UserId));
        await AssertInvalidStateAsync(() => service.AcquireUploadAsync(scope.UserId, "new", 1));
        await AssertInvalidStateAsync(() => service.CompleteUploadAsync(scope.UserId, "bad"));
        Assert.Equal(original, await scope.ReadReservationAsync("bad"));
        Assert.NotNull(await scope.ReadReservationAsync("expired"));
        Assert.Null(await scope.ReadCounterAsync(scope.RateKey));
        Assert.Null(await scope.ReadCounterAsync(scope.DailyKey));
        await service.FailUploadAsync(scope.UserId, "bad");
        Assert.Null(await scope.ReadReservationAsync("bad"));
        Assert.Equal(0, (await service.GetUploadStatisticsAsync(scope.UserId)).OccupiedUploadSizeToday);
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("-1")]
    [InlineData("9.007199254741e+15")]
    [InlineData("9223372036854775808")]
    [InlineData("")]
    [InlineData("01")]
    public async Task InvalidDailyCounter_ShouldNotDeleteTargetOrMutateState(string counter)
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var service = scope.CreateService();
        Assert.True((await service.AcquireUploadAsync(scope.UserId, "target", 100)).IsAllowed);
        var before = await scope.ReadReservationAsync("target");
        await scope.SetCounterAsync(scope.DailyKey, counter);
        await AssertInvalidStateAsync(() => service.GetUploadStatisticsAsync(scope.UserId));
        await AssertInvalidStateAsync(() => service.AcquireUploadAsync(scope.UserId, "new", 100));
        await AssertInvalidStateAsync(() => service.CompleteUploadAsync(scope.UserId, "target"));
        Assert.Equal(before, await scope.ReadReservationAsync("target"));
        Assert.Equal(counter, await scope.ReadCounterAsync(scope.DailyKey));
        Assert.Equal("1", await scope.ReadCounterAsync(scope.RateKey));
        Assert.Null(await scope.ReadReservationAsync("new"));
        await service.ResetUserLimitsAsync(scope.UserId);
        Assert.Equal(0, (await service.GetUploadStatisticsAsync(scope.UserId)).OccupiedUploadSizeToday);
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("2147483648")]
    public async Task InvalidMinuteCounter_ShouldNotConsumeCapacity(string counter)
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        await scope.SetCounterAsync(scope.RateKey, counter);
        var service = scope.CreateService();
        await AssertInvalidStateAsync(() => service.AcquireUploadAsync(scope.UserId, "new", 1));
        await AssertInvalidStateAsync(() => service.GetUploadStatisticsAsync(scope.UserId));
        Assert.Null(await scope.ReadReservationAsync("new"));
        Assert.Equal(counter, await scope.ReadCounterAsync(scope.RateKey));
    }

    [Fact]
    public async Task OverflowingStoredState_ShouldFailBeforeSettlementOrCleanup()
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        await scope.SetCounterAsync(scope.DailyKey, long.MaxValue.ToString(CultureInfo.InvariantCulture));
        await scope.SeedReservationAsync("target", "1", scope.Clock.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds());
        await scope.SeedReservationAsync("expired", "1", scope.Clock.GetUtcNow().AddSeconds(-1).ToUnixTimeSeconds());
        var target = await scope.ReadReservationAsync("target");
        var service = scope.CreateService();
        await AssertInvalidStateAsync(() => service.GetUploadStatisticsAsync(scope.UserId));
        await AssertInvalidStateAsync(() => service.AcquireUploadAsync(scope.UserId, "new", 1));
        await AssertInvalidStateAsync(() => service.CompleteUploadAsync(scope.UserId, "target"));
        Assert.Equal(target, await scope.ReadReservationAsync("target"));
        Assert.NotNull(await scope.ReadReservationAsync("expired"));
        Assert.Equal(long.MaxValue.ToString(CultureInfo.InvariantCulture), await scope.ReadCounterAsync(scope.DailyKey));
        Assert.Null(await scope.ReadCounterAsync(scope.RateKey));
    }

    [Fact]
    public async Task OverflowingPendingSum_ShouldFailWithoutPruningExpiredEntries()
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var expiry = scope.Clock.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds();
        await scope.SeedReservationAsync("first", long.MaxValue.ToString(CultureInfo.InvariantCulture), expiry);
        await scope.SeedReservationAsync("second", "1", expiry);
        await scope.SeedReservationAsync("expired", "1", 1);
        var service = scope.CreateService();
        await AssertInvalidStateAsync(() => service.GetUploadStatisticsAsync(scope.UserId));
        await AssertInvalidStateAsync(() => service.AcquireUploadAsync(scope.UserId, "new", 1));
        Assert.NotNull(await scope.ReadReservationAsync("expired"));
        Assert.NotNull(await scope.ReadReservationAsync("first"));
        Assert.NotNull(await scope.ReadReservationAsync("second"));
        Assert.Null(await scope.ReadCounterAsync(scope.RateKey));
    }

    [Theory]
    [InlineData(0, 10, 100L, UploadRateLimitFailureKind.ConcurrentUploads)]
    [InlineData(10, 0, 100L, UploadRateLimitFailureKind.UploadFrequency)]
    [InlineData(10, 10, 0L, UploadRateLimitFailureKind.DailyUploadSize)]
    public async Task ZeroLimits_ShouldRejectNewReservations(int concurrent, int minute, long daily, UploadRateLimitFailureKind reason)
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var service = scope.CreateService(new UploadRateLimitOptions
        { MaxConcurrentUploads = concurrent, MaxUploadsPerMinute = minute, MaxDailyUploadSize = daily });
        Assert.Equal(reason, (await service.AcquireUploadAsync(scope.UserId, "new", 1)).FailureKind);
        Assert.Null(await scope.ReadReservationAsync("new"));
        Assert.Null(await scope.ReadCounterAsync(scope.RateKey));
    }

    [Theory]
    [InlineData(-1, 10, 100L)]
    [InlineData(10, -1, 100L)]
    [InlineData(10, 10, -1L)]
    public async Task NegativeEnabledConfiguration_ShouldFailAtConstruction(int concurrent, int minute, long daily)
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        Assert.Throws<OptionsValidationException>(() => scope.CreateService(new UploadRateLimitOptions
        { MaxConcurrentUploads = concurrent, MaxUploadsPerMinute = minute, MaxDailyUploadSize = daily }));
    }

    [Fact]
    public async Task DisabledLimits_ShouldNotAccountButStillRejectInvalidArguments()
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var service = scope.CreateService(new UploadRateLimitOptions { Enable = false, MaxDailyUploadSize = -1 });
        Assert.True((await service.AcquireUploadAsync(scope.UserId, "disabled", long.MaxValue)).IsAllowed);
        await service.CompleteUploadAsync(scope.UserId, "disabled");
        await service.FailUploadAsync(scope.UserId, "disabled");
        Assert.Equal(0, (await service.GetUploadStatisticsAsync(scope.UserId)).OccupiedUploadSizeToday);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.AcquireUploadAsync(scope.UserId, "zero", 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.AcquireUploadAsync(scope.UserId, "negative", -1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AcquireUploadAsync(scope.UserId, "", 1));
    }

    [Theory]
    [InlineData(3, 10000L, 100L, 3)]
    [InlineData(30, 9007199254740993L, 3002399751580331L, 3)]
    public async Task ConcurrentAcquire_ShouldRespectLimitsAcrossIndependentClients(int concurrent, long daily, long bytes, int accepted)
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        using var second = scope.ConnectionString == null ? null : await ConnectionMultiplexer.ConnectAsync(scope.ConnectionString);
        var options = new UploadRateLimitOptions { MaxConcurrentUploads = concurrent, MaxUploadsPerMinute = 100, MaxDailyUploadSize = daily };
        var services = new[] { scope.CreateService(options), scope.CreateService(options, second) };
        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(index => services[index % 2].AcquireUploadAsync(scope.UserId, "id-" + index, bytes)));
        Assert.Equal(accepted, results.Count(result => result.IsAllowed));
        var stats = await services[0].GetUploadStatisticsAsync(scope.UserId);
        Assert.Equal(accepted, stats.CurrentConcurrentUploads);
        Assert.Equal(accepted, stats.UploadsThisMinute);
        Assert.Equal(checked(bytes * accepted), stats.ReservedUploadSizeToday);
    }

    [Fact]
    public async Task ConcurrentCompleteFailAndStatistics_ShouldSettleAtMostOnce()
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        using var second = scope.ConnectionString == null ? null : await ConnectionMultiplexer.ConnectAsync(scope.ConnectionString);
        var services = new[] { scope.CreateService(), scope.CreateService(connection: second) };
        const long bytes = 9007199254740993L;
        Assert.True((await services[0].AcquireUploadAsync(scope.UserId, "target", bytes)).IsAllowed);
        await Task.WhenAll(Enumerable.Range(0, 30).Select(async index =>
        {
            var service = services[index % 2];
            if (index % 3 == 0) await service.CompleteUploadAsync(scope.UserId, "target");
            else if (index % 3 == 1) await service.FailUploadAsync(scope.UserId, "target");
            else
            {
                var during = await service.GetUploadStatisticsAsync(scope.UserId);
                Assert.Contains(during.OccupiedUploadSizeToday, new[] { 0L, bytes });
            }
        }));
        var stats = await services[0].GetUploadStatisticsAsync(scope.UserId);
        Assert.Contains(stats.UploadedSizeToday, new[] { 0L, bytes });
        Assert.Equal(0, stats.ReservedUploadSizeToday);
        Assert.Equal(0, stats.CurrentConcurrentUploads);
        Assert.Equal(1, stats.UploadsThisMinute);
    }

    [Fact]
    public async Task CrossBusinessDay_ShouldSettleOriginalDateWithExactCapacity()
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        scope.Clock.Advance(TimeSpan.FromMinutes(59));
        var oldDailyKey = scope.DailyKey;
        var service = scope.CreateService();
        const long bytes = 9007199254740993L;
        Assert.True((await service.AcquireUploadAsync(scope.UserId, "target", bytes)).IsAllowed);
        scope.Clock.Advance(TimeSpan.FromMinutes(2));
        await service.CompleteUploadAsync(scope.UserId, "target");
        Assert.Equal(bytes.ToString(CultureInfo.InvariantCulture), await scope.ReadCounterAsync(oldDailyKey));
        Assert.Equal(0, (await service.GetUploadStatisticsAsync(scope.UserId)).OccupiedUploadSizeToday);
    }

    [Fact]
    public async Task MissingOrSoleExpiredTarget_ShouldNotSettle()
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var service = scope.CreateService();
        await service.CompleteUploadAsync(scope.UserId, "missing");
        Assert.True((await service.AcquireUploadAsync(scope.UserId, "sole", 100, TimeSpan.FromSeconds(1))).IsAllowed);
        scope.Clock.Advance(TimeSpan.FromSeconds(1));
        await service.CompleteUploadAsync(scope.UserId, "sole");
        Assert.Equal(0, (await service.GetUploadStatisticsAsync(scope.UserId)).OccupiedUploadSizeToday);
        Assert.Null(await scope.ReadReservationAsync("sole"));
        Assert.Null(await scope.ReadCounterAsync(scope.DailyKey));
    }

    [Fact]
    public async Task CapacityArithmetic_ShouldMatchBigIntegerOracleForDeterministicInt64Samples()
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var random = new Random(20261021);
        for (var sample = 0; sample < 32; sample++)
        {
            var first = random.NextInt64(1, long.MaxValue);
            var second = random.NextInt64(1, long.MaxValue - first + 1);
            var expected = new BigInteger(first) + second;
            var service = scope.CreateService(new UploadRateLimitOptions { MaxDailyUploadSize = (long)expected });
            await service.ResetUserLimitsAsync(scope.UserId);
            Assert.True((await service.AcquireUploadAsync(scope.UserId, "first", first)).IsAllowed);
            Assert.Equal(UploadRateLimitFailureKind.DailyUploadSize,
                (await service.AcquireUploadAsync(scope.UserId, "too-large", second + 1)).FailureKind);
            Assert.True((await service.AcquireUploadAsync(scope.UserId, "second", second)).IsAllowed);
            Assert.Equal(expected, new BigInteger((await service.GetUploadStatisticsAsync(scope.UserId)).ReservedUploadSizeToday));
            await service.CompleteUploadAsync(scope.UserId, "first");
            await service.CompleteUploadAsync(scope.UserId, "second");
            Assert.Equal(expected.ToString(CultureInfo.InvariantCulture), await scope.ReadCounterAsync(scope.DailyKey));
        }
    }

    private static async Task AssertInvalidStateAsync(Func<Task> action)
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(action);
        if (exception is RedisServerException redisError)
            Assert.Contains("RADISH_UPLOAD_QUOTA_INVALID_STATE", redisError.Message);
        else
            Assert.True(exception is InvalidOperationException or OverflowException,
                $"期望配额状态错误，实际为 {exception.GetType().Name}");
    }

}

[Trait("Cache", "Memory")]
public sealed class UploadQuotaMemoryTests : UploadQuotaBoundaryTests
{
    protected override string Backend => "memory";
}

[Trait("Cache", "Redis")]
public sealed class UploadQuotaRedisTests : UploadQuotaBoundaryTests
{
    protected override string Backend => "redis";
}

[Trait("Cache", "Valkey")]
public sealed class UploadQuotaValkeyTests : UploadQuotaBoundaryTests
{
    protected override string Backend => "valkey";
}
