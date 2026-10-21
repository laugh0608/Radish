using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.Service;
using StackExchange.Redis;
using Xunit;

namespace Radish.Api.Tests.Services.UploadQuota;

/// <summary>只连接显式启用的本机隔离实例；每个用例独立前缀，清理不触及其他测试。</summary>
public sealed class UploadQuotaTestScope : IAsyncDisposable
{
    private static long _nextUserId;
    private readonly MemoryDistributedCache _memory = new(Options.Create(new MemoryDistributedCacheOptions()));
    public long UserId { get; } = Interlocked.Increment(ref _nextUserId);
    public UploadQuotaClock Clock { get; } = new();
    public ConnectionMultiplexer? Connection { get; private init; }
    public IDatabase Database => Connection!.GetDatabase(15);
    public IDistributedCache Memory => _memory;
    public string Prefix { get; private init; } = string.Empty;
    public string? ConnectionString { get; private init; }
    public string ReservationKey => $"upload:{{{UserId}}}:reservations";
    public string DailyKey => $"upload:{{{UserId}}}:daily:{Clock.GetUtcNow().AddHours(8):yyyyMMdd}";
    public string RateKey => $"upload:{{{UserId}}}:rate:{Clock.GetUtcNow():yyyyMMddHHmm}";

    public static async Task<UploadQuotaTestScope> OpenAsync(string backend)
    {
        if (backend == "memory") return new();
        Assert.Contains(backend, new[] { "redis", "valkey" });
        Assert.SkipUnless(Environment.GetEnvironmentVariable("RADISH_TEST_UPLOAD_QUOTA_INTEGRATION") == "1",
            "未显式启用上传配额 Redis / Valkey 隔离集成测试。");
        var connectionString = Environment.GetEnvironmentVariable($"RADISH_TEST_UPLOAD_QUOTA_{backend.ToUpperInvariant()}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "已启用集成测试，必须提供对应的本机连接。");
        var config = ConfigurationOptions.Parse(connectionString!);
        Assert.Equal(15, config.DefaultDatabase);
        var endpoint = Assert.Single(config.EndPoints);
        var host = endpoint is DnsEndPoint dns ? dns.Host : ((IPEndPoint)endpoint).Address.ToString();
        var port = endpoint is DnsEndPoint d ? d.Port : ((IPEndPoint)endpoint).Port;
        Assert.Equal("127.0.0.1", host);
        Assert.Equal(backend == "redis" ? 16379 : 16380, port);
        var rootPrefix = Environment.GetEnvironmentVariable("RADISH_TEST_UPLOAD_QUOTA_PREFIX");
        Assert.NotNull(rootPrefix);
        Assert.StartsWith("radish-upload-quota.", rootPrefix);
        Assert.EndsWith(":", rootPrefix);
        var connection = await ConnectionMultiplexer.ConnectAsync(config);
        Assert.True(connection.IsConnected);
        return new() { Connection = connection, ConnectionString = connectionString, Prefix = rootPrefix + Guid.NewGuid().ToString("N") + ":" };
    }

    public UploadRateLimitService CreateService(UploadRateLimitOptions? options = null, IConnectionMultiplexer? connection = null) => new(
        new Caching(_memory), Options.Create(options ?? new UploadRateLimitOptions
        {
            MaxConcurrentUploads = 30, MaxUploadsPerMinute = 100, MaxDailyUploadSize = long.MaxValue
        }),
        Options.Create(new RedisOptions { Enable = Connection != null, InstanceName = Prefix }), Clock,
        new BusinessCalendar(Clock, Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" })),
        Connection == null ? Array.Empty<IConnectionMultiplexer>() : new[] { connection ?? Connection });

    public async Task SetCounterAsync(string key, string value)
    {
        if (Connection != null) await Database.StringSetAsync(Prefix + key, value, TimeSpan.FromMinutes(10));
        else await Memory.SetStringAsync(key, value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        }, TestContext.Current.CancellationToken);
    }

    public async Task<string?> ReadCounterAsync(string key) => Connection != null
        ? (string?)await Database.StringGetAsync(Prefix + key)
        : await Memory.GetStringAsync(key, TestContext.Current.CancellationToken);

    public async Task SeedReservationAsync(string id, string size, long expiresAt)
    {
        if (Connection != null)
        {
            await Database.HashSetAsync(Prefix + ReservationKey, id, $"{size}|{Prefix + RateKey}|{Prefix + DailyKey}|{expiresAt}");
            await Database.KeyExpireAsync(Prefix + ReservationKey, TimeSpan.FromMinutes(10));
        }
        else
        {
            var raw = await Memory.GetStringAsync(ReservationKey, TestContext.Current.CancellationToken);
            var document = raw == null ? new JObject { ["Reservations"] = new JObject() } : JObject.Parse(raw);
            var number = size.Length > 0 && (char.IsAsciiDigit(size[0]) || size[0] == '-') ? JToken.Parse(size) : new JValue(size);
            document["Reservations"]![id] = new JObject
            {
                ["FileSize"] = number, ["RateKey"] = RateKey, ["DailyKey"] = DailyKey, ["ExpiresAtUnixSeconds"] = expiresAt
            };
            await Memory.SetStringAsync(ReservationKey, document.ToString(Formatting.None),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) }, TestContext.Current.CancellationToken);
        }
    }

    public async Task<string?> ReadReservationAsync(string id)
    {
        if (Connection != null) return (string?)await Database.HashGetAsync(Prefix + ReservationKey, id);
        var raw = await Memory.GetStringAsync(ReservationKey, TestContext.Current.CancellationToken);
        return raw == null ? null : JObject.Parse(raw)["Reservations"]?[id]?.ToString(Formatting.None);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Connection != null)
            {
                var keys = Connection.GetServer(Connection.GetEndPoints().Single()).Keys(15, Prefix + "*").ToArray();
                if (keys.Length != 0) await Database.KeyDeleteAsync(keys);
            }
        }
        finally
        {
            if (Connection != null) await Connection.DisposeAsync();
        }
    }
}

public sealed class UploadQuotaClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 7, 11, 15, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now += delta;
}
