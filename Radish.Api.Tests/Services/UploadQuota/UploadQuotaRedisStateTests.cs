using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Radish.Api.Tests.Services.UploadQuota;

public abstract class UploadQuotaRedisStateTests
{
    protected abstract string Backend { get; }

    [Theory]
    [InlineData(9007199254740991L)]
    [InlineData(9007199254740993L)]
    [InlineData(long.MaxValue)]
    public async Task RedisStorage_ShouldKeepCanonicalDecimalAndExistingKeyTypes(long bytes)
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var service = scope.CreateService();
        Assert.True((await service.AcquireUploadAsync(scope.UserId, "target", bytes)).IsAllowed);
        var raw = await scope.ReadReservationAsync("target");
        Assert.StartsWith(bytes.ToString(CultureInfo.InvariantCulture) + "|", raw!);
        Assert.Equal(4, raw!.Split('|').Length);
        Assert.Equal(RedisType.Hash, await scope.Database.KeyTypeAsync(scope.Prefix + scope.ReservationKey));
        Assert.Equal(RedisType.String, await scope.Database.KeyTypeAsync(scope.Prefix + scope.RateKey));
        Assert.InRange((await scope.Database.KeyTimeToLiveAsync(scope.Prefix + scope.RateKey))!.Value.TotalSeconds, 110, 120);
        Assert.InRange((await scope.Database.KeyTimeToLiveAsync(scope.Prefix + scope.ReservationKey))!.Value.TotalSeconds, 3590, 3600);
        await service.CompleteUploadAsync(scope.UserId, "target");
        Assert.Equal(bytes.ToString(CultureInfo.InvariantCulture), await scope.ReadCounterAsync(scope.DailyKey));
        Assert.Equal(RedisType.String, await scope.Database.KeyTypeAsync(scope.Prefix + scope.DailyKey));
        Assert.InRange((await scope.Database.KeyTimeToLiveAsync(scope.Prefix + scope.DailyKey))!.Value.TotalSeconds, 3590, 3600);
    }

    [Fact]
    public async Task ExpiredBetweenPreReadAndScript_ShouldNotSettle()
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var service = scope.CreateService();
        await service.AcquireUploadAsync(scope.UserId, "target", 100, TimeSpan.FromSeconds(1));
        await service.AcquireUploadAsync(scope.UserId, "live", 200, TimeSpan.FromMinutes(10));
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        database.Setup(db => db.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .Returns(async (RedisKey key, RedisValue field, CommandFlags flags) =>
            {
                var value = await scope.Database.HashGetAsync(key, field, flags);
                scope.Clock.Advance(TimeSpan.FromSeconds(2));
                return value;
            });
        database.Setup(db => db.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]?>(), It.IsAny<RedisValue[]?>(), It.IsAny<CommandFlags>()))
            .Returns((string script, RedisKey[]? keys, RedisValue[]? values, CommandFlags flags) =>
                scope.Database.ScriptEvaluateAsync(script, keys, values, flags));
        var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        connection.Setup(mux => mux.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);
        await scope.CreateService(connection: connection.Object).CompleteUploadAsync(scope.UserId, "target");
        var stats = await service.GetUploadStatisticsAsync(scope.UserId);
        Assert.Equal(0, stats.UploadedSizeToday);
        Assert.Equal(200, stats.ReservedUploadSizeToday);
        database.Verify(db => db.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]?>(), It.IsAny<RedisValue[]?>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Theory]
    [InlineData("daily")]
    [InlineData("rate")]
    [InlineData("reservation")]
    public async Task WrongKeyType_ShouldFailBeforeWriting(string kind)
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var service = scope.CreateService();
        await service.AcquireUploadAsync(scope.UserId, "target", 100);
        var before = await scope.ReadReservationAsync("target");
        var key = scope.Prefix + (kind == "daily" ? scope.DailyKey : kind == "rate" ? scope.RateKey : scope.ReservationKey);
        await scope.Database.KeyDeleteAsync(key);
        if (kind == "reservation") await scope.Database.StringSetAsync(key, "wrong-type");
        else await scope.Database.HashSetAsync(key, "wrong", "type");
        var acquireError = await Assert.ThrowsAsync<RedisServerException>(() => service.AcquireUploadAsync(scope.UserId, "new", 1));
        Assert.Contains("WRONGTYPE", acquireError.Message);
        var statsError = await Assert.ThrowsAsync<RedisServerException>(() => service.GetUploadStatisticsAsync(scope.UserId));
        Assert.Contains("WRONGTYPE", statsError.Message);
        if (kind != "rate")
        {
            var completeError = await Assert.ThrowsAsync<RedisServerException>(() => service.CompleteUploadAsync(scope.UserId, "target"));
            Assert.Contains("WRONGTYPE", completeError.Message);
        }
        if (kind != "reservation") Assert.Equal(before, await scope.ReadReservationAsync("target"));
        else Assert.Equal("wrong-type", (string?)await scope.Database.StringGetAsync(key));
        if (kind != "rate") Assert.Equal("1", await scope.ReadCounterAsync(scope.RateKey));
    }

    [Fact]
    public async Task ServerExpiry_ShouldRemoveSoleReservationWithoutSettlement()
    {
        await using var scope = await UploadQuotaTestScope.OpenAsync(Backend);
        var service = scope.CreateService();
        await service.AcquireUploadAsync(scope.UserId, "short", 100, TimeSpan.FromSeconds(1));
        var elapsed = Stopwatch.StartNew();
        while (await scope.Database.KeyExistsAsync(scope.Prefix + scope.ReservationKey) && elapsed.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(await scope.Database.KeyExistsAsync(scope.Prefix + scope.ReservationKey));
        await service.CompleteUploadAsync(scope.UserId, "short");
        var stats = await service.GetUploadStatisticsAsync(scope.UserId);
        Assert.Equal(0, stats.CurrentConcurrentUploads);
        Assert.Equal(0, stats.OccupiedUploadSizeToday);
        Assert.Equal(1, stats.UploadsThisMinute);
    }
}

[Trait("Cache", "Redis")]
public sealed class UploadQuotaRedisStorageTests : UploadQuotaRedisStateTests
{
    protected override string Backend => "redis";
}

[Trait("Cache", "Valkey")]
public sealed class UploadQuotaValkeyStorageTests : UploadQuotaRedisStateTests
{
    protected override string Backend => "valkey";
}
