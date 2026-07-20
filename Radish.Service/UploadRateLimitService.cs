using System.Globalization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.IService;
using Radish.Service.Internal;
using Serilog;
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

    private const string AcquireScript = """
        local now = tonumber(ARGV[1])
        local uploadId = ARGV[2]
        local fileSize = tonumber(ARGV[3])
        local maxConcurrent = tonumber(ARGV[4])
        local maxPerMinute = tonumber(ARGV[5])
        local maxDailySize = tonumber(ARGV[6])
        local expiresAt = tonumber(ARGV[7])
        local reservationTtl = tonumber(ARGV[8])

        local entries = redis.call('HGETALL', KEYS[1])
        for index = 1, #entries, 2 do
            local value = entries[index + 1]
            local _, _, _, entryExpiresAt = string.match(value, '^(%d+)|([^|]+)|([^|]+)|(%d+)$')
            if entryExpiresAt == nil or tonumber(entryExpiresAt) <= now then
                redis.call('HDEL', KEYS[1], entries[index])
            end
        end

        if redis.call('HEXISTS', KEYS[1], uploadId) == 1 then
            return {1, 0, 0}
        end

        local activeEntries = redis.call('HVALS', KEYS[1])
        local concurrentCount = #activeEntries
        if concurrentCount >= maxConcurrent then
            return {2, concurrentCount, maxConcurrent}
        end

        local pendingDailySize = 0
        for _, value in ipairs(activeEntries) do
            local reservedSize, rateKey, dailyKey, entryExpiresAt = string.match(value, '^(%d+)|([^|]+)|([^|]+)|(%d+)$')
            if dailyKey == KEYS[3] then
                pendingDailySize = pendingDailySize + tonumber(reservedSize)
            end
        end

        local attemptMinuteCount = tonumber(redis.call('GET', KEYS[2]) or '0')
        if attemptMinuteCount >= maxPerMinute then
            return {3, attemptMinuteCount, maxPerMinute}
        end

        local completedDailySize = tonumber(redis.call('GET', KEYS[3]) or '0')
        local occupiedDailySize = completedDailySize + pendingDailySize
        if occupiedDailySize + fileSize > maxDailySize then
            return {4, occupiedDailySize, maxDailySize}
        end

        local value = tostring(fileSize) .. '|' .. KEYS[2] .. '|' .. KEYS[3] .. '|' .. tostring(expiresAt)
        redis.call('HSET', KEYS[1], uploadId, value)
        redis.call('INCR', KEYS[2])
        redis.call('EXPIRE', KEYS[2], 120)
        local currentTtl = redis.call('TTL', KEYS[1])
        if currentTtl < reservationTtl then
            redis.call('EXPIRE', KEYS[1], reservationTtl)
        end
        return {1, 0, 0}
        """;

    private const string CompleteScript = """
        local now = tonumber(ARGV[1])
        local uploadId = ARGV[2]
        local dailyTtl = tonumber(ARGV[3])
        local value = redis.call('HGET', KEYS[1], uploadId)
        if not value then
            return {0, 0}
        end

        local fileSize, rateKey, dailyKey, expiresAt = string.match(value, '^(%d+)|([^|]+)|([^|]+)|(%d+)$')
        if dailyKey ~= KEYS[2] then
            return {-1, 0}
        end
        redis.call('HDEL', KEYS[1], uploadId)
        if redis.call('HLEN', KEYS[1]) == 0 then
            redis.call('DEL', KEYS[1])
        end
        redis.call('INCRBY', KEYS[2], tonumber(fileSize))
        redis.call('EXPIRE', KEYS[2], dailyTtl)
        return {1, tonumber(fileSize)}
        """;

    private const string FailScript = """
        local removed = redis.call('HDEL', KEYS[1], ARGV[1])
        if redis.call('HLEN', KEYS[1]) == 0 then
            redis.call('DEL', KEYS[1])
        end
        return removed
        """;

    private const string StatisticsScript = """
        local now = tonumber(ARGV[1])
        local entries = redis.call('HGETALL', KEYS[1])
        for index = 1, #entries, 2 do
            local value = entries[index + 1]
            local _, _, _, entryExpiresAt = string.match(value, '^(%d+)|([^|]+)|([^|]+)|(%d+)$')
            if entryExpiresAt == nil or tonumber(entryExpiresAt) <= now then
                redis.call('HDEL', KEYS[1], entries[index])
            end
        end
        local pendingDailySize = 0
        local activeEntries = redis.call('HVALS', KEYS[1])
        for _, value in ipairs(activeEntries) do
            local reservedSize, rateKey, dailyKey, entryExpiresAt = string.match(value, '^(%d+)|([^|]+)|([^|]+)|(%d+)$')
            if dailyKey == KEYS[3] then
                pendingDailySize = pendingDailySize + tonumber(reservedSize)
            end
        end
        return {
            redis.call('HLEN', KEYS[1]),
            tonumber(redis.call('GET', KEYS[2]) or '0'),
            tonumber(redis.call('GET', KEYS[3]) or '0'),
            pendingDailySize
        }
        """;

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

        if (!result.IsAllowed)
        {
            Log.Warning(
                "[UploadRateLimit] 用户 {UserId} 上传预留被拒绝: {FailureKind}; 文件大小: {FileSize}",
                userId,
                result.FailureKind,
                fileSize);
        }

        return result;
    }

    public async Task CompleteUploadAsync(long userId, string uploadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);
        if (!_options.Enable)
        {
            return;
        }

        var completedSize = _redisDatabase == null
            ? await CompleteInMemoryAsync(userId, uploadId)
            : await CompleteInRedisAsync(userId, uploadId);

        if (completedSize.HasValue)
        {
            Log.Information(
                "[UploadRateLimit] 用户 {UserId} 完成上传: {UploadId}, 大小: {Size}",
                userId,
                uploadId,
                FormatFileSize(completedSize.Value));
        }
    }

    public async Task FailUploadAsync(long userId, string uploadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);
        if (!_options.Enable)
        {
            return;
        }

        var removed = _redisDatabase == null
            ? await FailInMemoryAsync(userId, uploadId)
            : await FailInRedisAsync(userId, uploadId);

        if (removed)
        {
            Log.Information("[UploadRateLimit] 用户 {UserId} 释放上传预留: {UploadId}", userId, uploadId);
        }
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

        Log.Information("[UploadRateLimit] 重置用户 {UserId} 的限流计数", userId);
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
            AcquireScript,
            [
                GetRedisKey(GetReservationKey(userId)),
                GetRedisKey(rateKey),
                GetRedisKey(dailyKey)
            ],
            [
                now.ToUnixTimeSeconds(),
                uploadId,
                fileSize,
                _options.MaxConcurrentUploads,
                _options.MaxUploadsPerMinute,
                _options.MaxDailyUploadSize,
                now.Add(lifetime).ToUnixTimeSeconds(),
                Math.Max(1, (long)Math.Ceiling(lifetime.TotalSeconds))
            ]);

        var values = (RedisResult[])result!;
        return BuildAcquireResult((long)values[0], (long)values[1], (long)values[2], fileSize);
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

            if (state.Reservations.ContainsKey(uploadId))
            {
                await SaveReservationStateAsync(userId, state, now);
                return UploadRateLimitCheckResult.Allowed();
            }

            if (state.Reservations.Count >= _options.MaxConcurrentUploads)
            {
                await SaveReservationStateAsync(userId, state, now);
                return BuildAcquireResult(2, state.Reservations.Count, _options.MaxConcurrentUploads, fileSize);
            }

            var rateKey = GetRateKey(userId, now);
            var attemptMinuteCount = await GetInt64Async(rateKey);
            if (attemptMinuteCount >= _options.MaxUploadsPerMinute)
            {
                await SaveReservationStateAsync(userId, state, now);
                return BuildAcquireResult(3, attemptMinuteCount, _options.MaxUploadsPerMinute, fileSize);
            }

            var dailyKey = GetDailySizeKey(userId, now);
            var completedDailySize = await GetInt64Async(dailyKey);
            var occupiedDailySize = completedDailySize + state.Reservations.Values
                .Where(reservation => reservation.DailyKey == dailyKey)
                .Sum(static reservation => reservation.FileSize);
            if (occupiedDailySize + fileSize > _options.MaxDailyUploadSize)
            {
                await SaveReservationStateAsync(userId, state, now);
                return BuildAcquireResult(4, occupiedDailySize, _options.MaxDailyUploadSize, fileSize);
            }

            state.Reservations[uploadId] = new UploadReservation
            {
                FileSize = fileSize,
                RateKey = rateKey,
                DailyKey = dailyKey,
                ExpiresAtUnixSeconds = now.Add(lifetime).ToUnixTimeSeconds()
            };
            await SetInt64Async(rateKey, attemptMinuteCount + 1, TimeSpan.FromMinutes(2));
            await SaveReservationStateAsync(userId, state, now);
            return UploadRateLimitCheckResult.Allowed();
        }
    }

    private async Task<long?> CompleteInRedisAsync(long userId, string uploadId)
    {
        var reservationKey = GetRedisKey(GetReservationKey(userId));
        var value = await _redisDatabase!.HashGetAsync(reservationKey, uploadId);
        if (!value.HasValue || !TryParseReservation(value.ToString(), out var reservation))
        {
            return null;
        }

        var result = await _redisDatabase!.ScriptEvaluateAsync(
            CompleteScript,
            [reservationKey, reservation.DailyKey],
            [
                _timeProvider.GetUtcNow().ToUnixTimeSeconds(),
                uploadId,
                Math.Max(1, (long)Math.Ceiling(_businessCalendar.GetTimeUntilNextDate().TotalSeconds))
            ]);
        var values = (RedisResult[])result!;
        if ((long)values[0] == -1)
        {
            throw new InvalidOperationException("上传预留中的业务日期键与结算键不一致。");
        }
        return (long)values[0] == 1 ? (long)values[1] : null;
    }

    private async Task<long?> CompleteInMemoryAsync(long userId, string uploadId)
    {
        using (await AsyncKeyedLock.AcquireAsync(GetUserLockKey(userId)))
        {
            var now = _timeProvider.GetUtcNow();
            var state = await LoadReservationStateAsync(userId);
            if (!state.Reservations.Remove(uploadId, out var reservation))
            {
                PruneExpiredReservations(state, now.ToUnixTimeSeconds());
                await SaveReservationStateAsync(userId, state, now);
                return null;
            }

            PruneExpiredReservations(state, now.ToUnixTimeSeconds());
            await SetInt64Async(
                reservation.DailyKey,
                await GetInt64Async(reservation.DailyKey) + reservation.FileSize,
                _businessCalendar.GetTimeUntilNextDate());
            await SaveReservationStateAsync(userId, state, now);
            return reservation.FileSize;
        }
    }

    private async Task<bool> FailInRedisAsync(long userId, string uploadId)
    {
        var result = await _redisDatabase!.ScriptEvaluateAsync(
            FailScript,
            [GetRedisKey(GetReservationKey(userId))],
            [uploadId]);
        return (long)result == 1;
    }

    private async Task<bool> FailInMemoryAsync(long userId, string uploadId)
    {
        using (await AsyncKeyedLock.AcquireAsync(GetUserLockKey(userId)))
        {
            var now = _timeProvider.GetUtcNow();
            var state = await LoadReservationStateAsync(userId);
            PruneExpiredReservations(state, now.ToUnixTimeSeconds());
            var removed = state.Reservations.Remove(uploadId);
            await SaveReservationStateAsync(userId, state, now);
            return removed;
        }
    }

    private async Task<UploadStatisticsSnapshot> GetRedisStatisticsAsync(long userId)
    {
        var now = _timeProvider.GetUtcNow();
        var result = await _redisDatabase!.ScriptEvaluateAsync(
            StatisticsScript,
            [
                GetRedisKey(GetReservationKey(userId)),
                GetRedisKey(GetRateKey(userId, now)),
                GetRedisKey(GetDailySizeKey(userId, now))
            ],
            [now.ToUnixTimeSeconds()]);
        var values = (RedisResult[])result!;
        return new UploadStatisticsSnapshot(
            (int)(long)values[0],
            (int)(long)values[1],
            (long)values[2],
            (long)values[3]);
    }

    private async Task<UploadStatisticsSnapshot> GetInMemoryStatisticsAsync(long userId)
    {
        using (await AsyncKeyedLock.AcquireAsync(GetUserLockKey(userId)))
        {
            var now = _timeProvider.GetUtcNow();
            var state = await LoadReservationStateAsync(userId);
            PruneExpiredReservations(state, now.ToUnixTimeSeconds());
            await SaveReservationStateAsync(userId, state, now);
            var dailyKey = GetDailySizeKey(userId, now);
            return new UploadStatisticsSnapshot(
                state.Reservations.Count,
                (int)await GetInt64Async(GetRateKey(userId, now)),
                await GetInt64Async(dailyKey),
                state.Reservations.Values
                    .Where(reservation => reservation.DailyKey == dailyKey)
                    .Sum(static reservation => reservation.FileSize));
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

    private async Task<UploadReservationState> LoadReservationStateAsync(long userId)
    {
        var value = await _cache.Cache.GetStringAsync(GetReservationKey(userId));
        return string.IsNullOrWhiteSpace(value)
            ? new UploadReservationState()
            : JsonConvert.DeserializeObject<UploadReservationState>(value) ?? new UploadReservationState();
    }

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
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
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

    private static bool TryParseReservation(string value, out ParsedReservation reservation)
    {
        var segments = value.Split('|');
        if (segments.Length == 4 &&
            long.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileSize) &&
            long.TryParse(segments[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresAt))
        {
            reservation = new ParsedReservation(fileSize, segments[1], segments[2], expiresAt);
            return true;
        }

        reservation = default;
        return false;
    }

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
