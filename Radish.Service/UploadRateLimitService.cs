using System.Globalization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.IService;
using Radish.Service.Internal;
using StackExchange.Redis;

namespace Radish.Service;

/// <summary>
/// 上传限流服务实现。
/// </summary>
/// <remarks>
/// Redis 模式通过 Lua 一次完成检查与预留；内存缓存模式通过按用户异步锁保护同一组缓存状态。
/// 成功取得预留即消费分钟尝试次数；日容量在预留期间占用，成功后结算，失败、取消或过期时释放。
/// </remarks>
public class UploadRateLimitService : IUploadRateLimitService
{
    private const int DefaultReservationSeconds = 60 * 60;

    private readonly ICaching _cache;
    private readonly UploadRateLimitOptions _options;
    private readonly RedisOptions _redisOptions;
    private readonly TimeProvider _timeProvider;
    private readonly BusinessCalendar _businessCalendar;
    private readonly IDatabase? _redisDatabase;

    public UploadRateLimitService(
        ICaching cache,
        IOptions<UploadRateLimitOptions> options,
        IOptions<RedisOptions> redisOptions,
        TimeProvider timeProvider,
        BusinessCalendar businessCalendar,
        IEnumerable<IConnectionMultiplexer> redisConnections)
    {
        _cache = cache;
        _options = options.Value;
        if (_options.Enable && (_options.MaxConcurrentUploads < 0 || _options.MaxUploadsPerMinute < 0 || _options.MaxDailyUploadSize < 0))
        {
            throw new OptionsValidationException(nameof(UploadRateLimitOptions), typeof(UploadRateLimitOptions),
                ["启用上传限流时，并发数、分钟次数和日容量必须非负；0 表示阻止新增预留。"]);
        }
        _redisOptions = redisOptions.Value;
        _timeProvider = timeProvider;
        _businessCalendar = businessCalendar;
        _redisDatabase = _redisOptions.Enable
            ? redisConnections.SingleOrDefault()?.GetDatabase()
                ?? throw new InvalidOperationException("Redis 已启用，但上传限流服务未取得 Redis 连接。")
            : null;
    }

    public async Task<UploadRateLimitCheckResult> AcquireUploadAsync(
        long userId,
        string uploadId,
        long fileSize,
        TimeSpan? reservationLifetime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileSize);

        if (!_options.Enable)
        {
            return UploadRateLimitCheckResult.Allowed();
        }

        var lifetime = NormalizeReservationLifetime(reservationLifetime);
        var result = _redisDatabase == null
            ? await AcquireInMemoryAsync(userId, uploadId, fileSize, lifetime)
            : await AcquireInRedisAsync(userId, uploadId, fileSize, lifetime);

        return result;
    }

    public async Task CompleteUploadAsync(long userId, string uploadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);
        if (!_options.Enable)
        {
            return;
        }

        _ = _redisDatabase == null
            ? await CompleteInMemoryAsync(userId, uploadId)
            : await CompleteInRedisAsync(userId, uploadId);
    }

    public async Task FailUploadAsync(long userId, string uploadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);
        if (!_options.Enable)
        {
            return;
        }

        _ = _redisDatabase == null
            ? await FailInMemoryAsync(userId, uploadId)
            : await FailInRedisAsync(userId, uploadId);
    }

    public async Task<UploadStatistics> GetUploadStatisticsAsync(long userId)
    {
        var snapshot = _redisDatabase == null
            ? await GetInMemoryStatisticsAsync(userId)
            : await GetRedisStatisticsAsync(userId);

        return new UploadStatistics
        {
            CurrentConcurrentUploads = snapshot.ConcurrentCount,
            UploadsThisMinute = snapshot.AttemptMinuteCount,
            UploadedSizeToday = snapshot.CompletedDailySize,
            ReservedUploadSizeToday = snapshot.ReservedDailySize,
            MaxConcurrentUploads = _options.MaxConcurrentUploads,
            MaxUploadsPerMinute = _options.MaxUploadsPerMinute,
            MaxDailyUploadSize = _options.MaxDailyUploadSize
        };
    }

    public async Task ResetUserLimitsAsync(long userId)
    {
        var now = _timeProvider.GetUtcNow();
        var keys = new[]
        {
            GetReservationKey(userId),
            GetRateKey(userId, now),
            GetDailySizeKey(userId, now)
        };

        if (_redisDatabase != null)
        {
            await _redisDatabase.KeyDeleteAsync(keys.Select(GetRedisKey).Select(static key => (RedisKey)key).ToArray());
        }
        else
        {
            using (await AsyncKeyedLock.AcquireAsync(GetUserLockKey(userId)))
            {
                foreach (var key in keys)
                {
                    await _cache.Cache.RemoveAsync(key);
                }
            }
        }
    }

    private async Task<UploadRateLimitCheckResult> AcquireInRedisAsync(
        long userId,
        string uploadId,
        long fileSize,
        TimeSpan lifetime)
    {
        var now = _timeProvider.GetUtcNow();
        var rateKey = GetRateKey(userId, now);
        var dailyKey = GetDailySizeKey(userId, now);
        var result = await _redisDatabase!.ScriptEvaluateAsync(
            UploadQuotaScripts.Acquire,
            [
                GetRedisKey(GetReservationKey(userId)),
                GetRedisKey(rateKey),
                GetRedisKey(dailyKey)
            ],
            [
                now.ToUnixTimeSeconds(),
                uploadId,
                fileSize.ToString(CultureInfo.InvariantCulture),
                _options.MaxConcurrentUploads,
                _options.MaxUploadsPerMinute,
                _options.MaxDailyUploadSize.ToString(CultureInfo.InvariantCulture),
                now.Add(lifetime).ToUnixTimeSeconds(),
                Math.Max(1, (long)Math.Ceiling(lifetime.TotalSeconds))
            ]);

        var values = (RedisResult[])result!;
        return BuildAcquireResult((long)values[0], ParseCounter(values[1].ToString()), ParseCounter(values[2].ToString()), fileSize);
    }

    private async Task<UploadRateLimitCheckResult> AcquireInMemoryAsync(
        long userId,
        string uploadId,
        long fileSize,
        TimeSpan lifetime)
    {
        using (await AsyncKeyedLock.AcquireAsync(GetUserLockKey(userId)))
        {
            var now = _timeProvider.GetUtcNow();
            var state = await LoadReservationStateAsync(userId);
            PruneExpiredReservations(state, now.ToUnixTimeSeconds());

            var rateKey = GetRateKey(userId, now);
            var dailyKey = GetDailySizeKey(userId, now);
            var attemptMinuteCount = checked((int)await GetInt64Async(rateKey));
            var completedDailySize = await GetInt64Async(dailyKey);
            var occupiedDailySize = checked(completedDailySize + SumReservations(state, dailyKey));
            UploadRateLimitCheckResult? rejection = null;
            if (state.Reservations.ContainsKey(uploadId))
            {
                await SaveReservationStateAsync(userId, state, now);
                return UploadRateLimitCheckResult.Allowed();
            }
            if (state.Reservations.Count >= _options.MaxConcurrentUploads)
                rejection = BuildAcquireResult(2, state.Reservations.Count, _options.MaxConcurrentUploads, fileSize);
            else if (attemptMinuteCount >= _options.MaxUploadsPerMinute)
                rejection = BuildAcquireResult(3, attemptMinuteCount, _options.MaxUploadsPerMinute, fileSize);
            else if (occupiedDailySize > _options.MaxDailyUploadSize || fileSize > _options.MaxDailyUploadSize - occupiedDailySize)
                rejection = BuildAcquireResult(4, occupiedDailySize, _options.MaxDailyUploadSize, fileSize);

            if (rejection != null)
            {
                await SaveReservationStateAsync(userId, state, now);
                return rejection;
            }

            state.Reservations[uploadId] = new UploadReservation
            {
                FileSize = fileSize,
                RateKey = rateKey,
                DailyKey = dailyKey,
                ExpiresAtUnixSeconds = now.Add(lifetime).ToUnixTimeSeconds()
            };
            await SetInt64Async(rateKey, checked(attemptMinuteCount + 1), TimeSpan.FromMinutes(2));
            await SaveReservationStateAsync(userId, state, now);
            return UploadRateLimitCheckResult.Allowed();
        }
    }

    private async Task<long?> CompleteInRedisAsync(long userId, string uploadId)
    {
        var reservationKey = GetRedisKey(GetReservationKey(userId));
        var value = await _redisDatabase!.HashGetAsync(reservationKey, uploadId);
        if (!value.HasValue) return null;
        var reservation = ParseReservation(value.ToString(), userId);

        var result = await _redisDatabase!.ScriptEvaluateAsync(
            UploadQuotaScripts.Complete,
            [reservationKey, reservation.DailyKey],
            [
                _timeProvider.GetUtcNow().ToUnixTimeSeconds(),
                uploadId,
                Math.Max(1, (long)Math.Ceiling(_businessCalendar.GetTimeUntilNextDate().TotalSeconds))
            ]);
        var values = (RedisResult[])result!;
        return (long)values[0] == 1 ? ParseCounter(values[1].ToString()) : null;
    }

    private async Task<long?> CompleteInMemoryAsync(long userId, string uploadId)
    {
        using (await AsyncKeyedLock.AcquireAsync(GetUserLockKey(userId)))
        {
            var now = _timeProvider.GetUtcNow();
            var state = await LoadReservationStateAsync(userId);
            PruneExpiredReservations(state, now.ToUnixTimeSeconds());
            if (!state.Reservations.TryGetValue(uploadId, out var reservation))
            {
                await SaveReservationStateAsync(userId, state, now);
                return null;
            }

            var completed = checked(await GetInt64Async(reservation.DailyKey) + reservation.FileSize);
            await SetInt64Async(reservation.DailyKey, completed, _businessCalendar.GetTimeUntilNextDate());
            state.Reservations.Remove(uploadId);
            await SaveReservationStateAsync(userId, state, now);
            return reservation.FileSize;
        }
    }

    private async Task<bool> FailInRedisAsync(long userId, string uploadId)
    {
        var result = await _redisDatabase!.ScriptEvaluateAsync(
            UploadQuotaScripts.Fail,
            [GetRedisKey(GetReservationKey(userId))],
            [uploadId]);
        return (long)result == 1;
    }

    private async Task<bool> FailInMemoryAsync(long userId, string uploadId)
    {
        using (await AsyncKeyedLock.AcquireAsync(GetUserLockKey(userId)))
        {
            // 释放目标无需读取其容量，允许调用方释放已损坏的容量记录。
            var document = await LoadReservationDocumentAsync(userId);
            var reservations = GetReservationEntries(document);
            var removed = reservations.Remove(uploadId);
            if (!removed) return false;
            var now = _timeProvider.GetUtcNow();
            foreach (var property in reservations.Properties().ToArray())
            {
                if (ReadExpiry(property.Value["ExpiresAtUnixSeconds"]) <= now.ToUnixTimeSeconds()) property.Remove();
            }
            if (reservations.Count == 0)
                await _cache.Cache.RemoveAsync(GetReservationKey(userId));
            else
            {
                var latestExpiry = reservations.Properties().Max(property => ReadExpiry(property.Value["ExpiresAtUnixSeconds"]));
                await _cache.Cache.SetStringAsync(GetReservationKey(userId), document.ToString(Formatting.None),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(1, latestExpiry - now.ToUnixTimeSeconds())) });
            }
            return true;
        }
    }

    private async Task<UploadStatisticsSnapshot> GetRedisStatisticsAsync(long userId)
    {
        var now = _timeProvider.GetUtcNow();
        var result = await _redisDatabase!.ScriptEvaluateAsync(
            UploadQuotaScripts.Statistics,
            [
                GetRedisKey(GetReservationKey(userId)),
                GetRedisKey(GetRateKey(userId, now)),
                GetRedisKey(GetDailySizeKey(userId, now))
            ],
            [now.ToUnixTimeSeconds()]);
        var values = (RedisResult[])result!;
        return new UploadStatisticsSnapshot(
            checked((int)(long)values[0]),
            checked((int)ParseCounter(values[1].ToString())),
            ParseCounter(values[2].ToString()),
            ParseCounter(values[3].ToString()));
    }

    private async Task<UploadStatisticsSnapshot> GetInMemoryStatisticsAsync(long userId)
    {
        using (await AsyncKeyedLock.AcquireAsync(GetUserLockKey(userId)))
        {
            var now = _timeProvider.GetUtcNow();
            var state = await LoadReservationStateAsync(userId);
            PruneExpiredReservations(state, now.ToUnixTimeSeconds());
            var dailyKey = GetDailySizeKey(userId, now);
            var attempts = checked((int)await GetInt64Async(GetRateKey(userId, now)));
            var completed = await GetInt64Async(dailyKey);
            var pending = SumReservations(state, dailyKey);
            _ = checked(completed + pending);
            await SaveReservationStateAsync(userId, state, now);
            return new UploadStatisticsSnapshot(state.Reservations.Count, attempts, completed, pending);
        }
    }

    private UploadRateLimitCheckResult BuildAcquireResult(long code, long current, long maximum, long fileSize)
    {
        return code switch
        {
            1 => UploadRateLimitCheckResult.Allowed(),
            2 => UploadRateLimitCheckResult.Rejected(
                $"您当前有 {current} 个文件正在上传，已达到并发上传限制（最多 {maximum} 个）",
                UploadRateLimitFailureKind.ConcurrentUploads,
                current,
                maximum),
            3 => UploadRateLimitCheckResult.Rejected(
                $"您本分钟已发起 {current} 次上传，已达到速率限制（最多 {maximum} 次/分钟），请稍后再试",
                UploadRateLimitFailureKind.UploadFrequency,
                current,
                maximum),
            4 => UploadRateLimitCheckResult.Rejected(
                $"您今日已上传或预留 {FormatFileSize(current)}，剩余配额 {FormatFileSize(Math.Max(0, maximum - current))}，无法上传此文件（{FormatFileSize(fileSize)}）",
                UploadRateLimitFailureKind.DailyUploadSize,
                FormatFileSize(current),
                FormatFileSize(Math.Max(0, maximum - current)),
                FormatFileSize(fileSize)),
            _ => throw new InvalidOperationException($"上传限流原子操作返回未知状态：{code}")
        };
    }

    private async Task<JObject> LoadReservationDocumentAsync(long userId)
    {
        var value = await _cache.Cache.GetStringAsync(GetReservationKey(userId));
        return value == null
            ? new JObject { ["Reservations"] = new JObject() }
            : JObject.Parse(value);
    }

    private static JObject GetReservationEntries(JObject document) => document["Reservations"] as JObject
        ?? throw InvalidState();

    private async Task<UploadReservationState> LoadReservationStateAsync(long userId)
    {
        var document = await LoadReservationDocumentAsync(userId);
        var state = new UploadReservationState();
        foreach (var property in GetReservationEntries(document).Properties())
        {
            if (property.Value is not JObject entry || string.IsNullOrWhiteSpace(property.Name)) throw InvalidState();
            var size = ReadJsonInteger(entry["FileSize"]);
            var expiry = ReadExpiry(entry["ExpiresAtUnixSeconds"]);
            var rateKey = entry["RateKey"]?.Type == JTokenType.String ? entry["RateKey"]!.Value<string>() : null;
            var dailyKey = entry["DailyKey"]?.Type == JTokenType.String ? entry["DailyKey"]!.Value<string>() : null;
            if (size == 0 || !IsStoredKey(rateKey, $"upload:{{{userId}}}:rate:", 12)
                || !IsStoredKey(dailyKey, $"upload:{{{userId}}}:daily:", 8)) throw InvalidState();
            state.Reservations.Add(property.Name, new UploadReservation
            {
                FileSize = size, RateKey = rateKey!, DailyKey = dailyKey!, ExpiresAtUnixSeconds = expiry
            });
        }
        return state;
    }

    private static long ReadJsonInteger(JToken? token) => token?.Type == JTokenType.Integer
        ? ParseCounter(token.ToString(Formatting.None)) : throw InvalidState();

    private static long ReadExpiry(JToken? token)
    {
        var expiry = ReadJsonInteger(token);
        return expiry <= DateTimeOffset.MaxValue.ToUnixTimeSeconds() ? expiry : throw InvalidState();
    }

    private static long SumReservations(UploadReservationState state, string dailyKey) => state.Reservations.Values
        .Where(reservation => reservation.DailyKey == dailyKey).Sum(static reservation => reservation.FileSize);

    private async Task SaveReservationStateAsync(
        long userId,
        UploadReservationState state,
        DateTimeOffset now)
    {
        var key = GetReservationKey(userId);
        if (state.Reservations.Count == 0)
        {
            await _cache.Cache.RemoveAsync(key);
            return;
        }

        var latestExpiry = state.Reservations.Values.Max(static reservation => reservation.ExpiresAtUnixSeconds);
        var remaining = TimeSpan.FromSeconds(Math.Max(1, latestExpiry - now.ToUnixTimeSeconds()));
        await _cache.Cache.SetStringAsync(
            key,
            JsonConvert.SerializeObject(state),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining });
    }

    private async Task<long> GetInt64Async(string key)
    {
        var value = await _cache.Cache.GetStringAsync(key);
        return value == null ? 0 : ParseCounter(value);
    }

    private Task SetInt64Async(string key, long value, TimeSpan expiration)
    {
        return _cache.Cache.SetStringAsync(
            key,
            value.ToString(CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration > TimeSpan.Zero
                    ? expiration
                    : TimeSpan.FromSeconds(1)
            });
    }

    private static void PruneExpiredReservations(UploadReservationState state, long nowUnixSeconds)
    {
        foreach (var uploadId in state.Reservations
                     .Where(pair => pair.Value.ExpiresAtUnixSeconds <= nowUnixSeconds)
                     .Select(static pair => pair.Key)
                     .ToArray())
        {
            state.Reservations.Remove(uploadId);
        }
    }

    private static TimeSpan NormalizeReservationLifetime(TimeSpan? requestedLifetime)
    {
        var lifetime = requestedLifetime ?? TimeSpan.FromSeconds(DefaultReservationSeconds);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedLifetime), "上传预留有效期必须大于零。");
        }

        return lifetime;
    }

    private string GetRedisKey(string key) => $"{_redisOptions.InstanceName}{key}";

    // 每个用户的所有 Redis key 共用 hash tag，保证 Redis Cluster 下 Lua KEYS 同槽。
    private static string GetReservationKey(long userId) => $"upload:{{{userId}}}:reservations";

    private static string GetUserLockKey(long userId) => $"upload-rate:{userId}";

    private static string GetRateKey(long userId, DateTimeOffset now)
    {
        var minute = now.UtcDateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        return $"upload:{{{userId}}}:rate:{minute}";
    }

    private string GetDailySizeKey(long userId, DateTimeOffset now)
    {
        var date = _businessCalendar.GetDate(now);
        return $"upload:{{{userId}}}:daily:{date:yyyyMMdd}";
    }

    private ParsedReservation ParseReservation(string value, long userId)
    {
        var segments = value.Split('|');
        if (segments.Length != 4) throw InvalidState();
        var size = ParseCounter(segments[0]);
        var expiry = ParseCounter(segments[3]);
        if (size == 0 || expiry > DateTimeOffset.MaxValue.ToUnixTimeSeconds()
            || !IsStoredKey(segments[1], GetRedisKey($"upload:{{{userId}}}:rate:"), 12)
            || !IsStoredKey(segments[2], GetRedisKey($"upload:{{{userId}}}:daily:"), 8)) throw InvalidState();
        return new ParsedReservation(size, segments[1], segments[2], expiry);
    }

    private static bool IsStoredKey(string? key, string prefix, int digits) => key != null
        && key.StartsWith(prefix, StringComparison.Ordinal) && key.Length == prefix.Length + digits
        && key.AsSpan(prefix.Length).ContainsAnyExceptInRange('0', '9') == false;

    private static long ParseCounter(string value)
    {
        if (value.Length == 0 || (value.Length > 1 && value[0] == '0')
            || value.AsSpan().ContainsAnyExceptInRange('0', '9')
            || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)) throw InvalidState();
        return result;
    }

    private static InvalidOperationException InvalidState() => new("上传配额缓存状态无效或超出整数范围。");

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double length = bytes;
        var order = 0;
        while (length >= 1024 && order < sizes.Length - 1)
        {
            order++;
            length /= 1024;
        }

        return $"{length:0.##} {sizes[order]}";
    }

    private sealed class UploadReservationState
    {
        public Dictionary<string, UploadReservation> Reservations { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class UploadReservation
    {
        public long FileSize { get; set; }

        public string RateKey { get; set; } = string.Empty;

        public string DailyKey { get; set; } = string.Empty;

        public long ExpiresAtUnixSeconds { get; set; }
    }

    private sealed record UploadStatisticsSnapshot(
        int ConcurrentCount,
        int AttemptMinuteCount,
        long CompletedDailySize,
        long ReservedDailySize);

    private readonly record struct ParsedReservation(
        long FileSize,
        string RateKey,
        string DailyKey,
        long ExpiresAtUnixSeconds);
}
