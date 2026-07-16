using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.IService;
using Radish.Service;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Radish.Api.Tests.Services;

public class UploadRateLimitServiceTest
{
    private readonly ICaching _cache;
    private readonly UploadRateLimitOptions _options;
    private readonly MutableTimeProvider _timeProvider;
    private readonly BusinessCalendar _businessCalendar;
    private readonly IUploadRateLimitService _rateLimitService;

    public UploadRateLimitServiceTest()
    {
        _cache = new Caching(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        _options = new UploadRateLimitOptions
        {
            Enable = true,
            MaxConcurrentUploads = 3,
            MaxUploadsPerMinute = 5,
            MaxDailyUploadSize = 10 * 1024 * 1024
        };
        _timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 7, 11, 15, 0, 0, TimeSpan.Zero));
        _businessCalendar = new BusinessCalendar(
            _timeProvider,
            Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" }));
        _rateLimitService = CreateService(_options);
    }

    [Fact(DisplayName = "并发 Acquire 只允许原子取得配置数量的预留")]
    public async Task AcquireUploadAsync_ShouldAtomicallyEnforceConcurrentLimit()
    {
        const long userId = 10001;
        var attempts = Enumerable.Range(0, 20)
            .Select(index => _rateLimitService.AcquireUploadAsync(userId, $"upload-{index}", 1024));

        var results = await Task.WhenAll(attempts);

        results.Count(result => result.IsAllowed).ShouldBe(3);
        results.Where(result => !result.IsAllowed)
            .ShouldAllBe(result => result.FailureKind == UploadRateLimitFailureKind.ConcurrentUploads);
        var statistics = await _rateLimitService.GetUploadStatisticsAsync(userId);
        statistics.CurrentConcurrentUploads.ShouldBe(3);
        statistics.UploadsThisMinute.ShouldBe(3);
        statistics.ReservedUploadSizeToday.ShouldBe(3 * 1024);
        statistics.UploadedSizeToday.ShouldBe(0);
    }

    [Fact(DisplayName = "失败释放并发与日容量但保留分钟尝试次数")]
    public async Task FailUploadAsync_ShouldReleaseReservationButKeepAttemptCount()
    {
        var service = CreateService(new UploadRateLimitOptions
        {
            Enable = true,
            MaxConcurrentUploads = 1,
            MaxUploadsPerMinute = 2,
            MaxDailyUploadSize = 10_000
        });
        const long userId = 10002;

        (await service.AcquireUploadAsync(userId, "upload-a", 4000)).IsAllowed.ShouldBeTrue();
        await service.FailUploadAsync(userId, "upload-a");
        var afterFailure = await service.GetUploadStatisticsAsync(userId);
        afterFailure.CurrentConcurrentUploads.ShouldBe(0);
        afterFailure.ReservedUploadSizeToday.ShouldBe(0);
        afterFailure.UploadsThisMinute.ShouldBe(1);

        (await service.AcquireUploadAsync(userId, "upload-b", 4000)).IsAllowed.ShouldBeTrue();
        await service.FailUploadAsync(userId, "upload-b");
        var rejected = await service.AcquireUploadAsync(userId, "upload-c", 4000);
        rejected.IsAllowed.ShouldBeFalse();
        rejected.FailureKind.ShouldBe(UploadRateLimitFailureKind.UploadFrequency);
        rejected.MessageArguments.ShouldBe(new object[] { 2L, 2L });
    }

    [Fact(DisplayName = "完成上传把预留转为已用日容量且重复完成幂等")]
    public async Task CompleteUploadAsync_ShouldSettleDailySizeIdempotently()
    {
        const long userId = 10003;
        const long fileSize = 3 * 1024 * 1024;
        (await _rateLimitService.AcquireUploadAsync(userId, "upload-a", fileSize)).IsAllowed.ShouldBeTrue();

        await _rateLimitService.CompleteUploadAsync(userId, "upload-a");
        await _rateLimitService.CompleteUploadAsync(userId, "upload-a");

        var statistics = await _rateLimitService.GetUploadStatisticsAsync(userId);
        statistics.CurrentConcurrentUploads.ShouldBe(0);
        statistics.ReservedUploadSizeToday.ShouldBe(0);
        statistics.UploadedSizeToday.ShouldBe(fileSize);
        statistics.UploadsThisMinute.ShouldBe(1);
    }

    [Fact(DisplayName = "日容量检查包含进行中的预留并在失败后释放")]
    public async Task AcquireUploadAsync_ShouldIncludeReservedDailySize()
    {
        var service = CreateService(new UploadRateLimitOptions
        {
            Enable = true,
            MaxConcurrentUploads = 5,
            MaxUploadsPerMinute = 10,
            MaxDailyUploadSize = 10 * 1024 * 1024
        });
        const long userId = 10004;
        const long sixMegabytes = 6 * 1024 * 1024;

        (await service.AcquireUploadAsync(userId, "upload-a", sixMegabytes)).IsAllowed.ShouldBeTrue();
        var rejected = await service.AcquireUploadAsync(userId, "upload-b", sixMegabytes);
        rejected.FailureKind.ShouldBe(UploadRateLimitFailureKind.DailyUploadSize);
        rejected.MessageArguments.ShouldBe(new object[] { "6 MB", "4 MB", "6 MB" });

        await service.FailUploadAsync(userId, "upload-a");
        (await service.AcquireUploadAsync(userId, "upload-c", sixMegabytes)).IsAllowed.ShouldBeTrue();
        var statistics = await service.GetUploadStatisticsAsync(userId);
        statistics.OccupiedUploadSizeToday.ShouldBe(sixMegabytes);
    }

    [Fact(DisplayName = "跨业务日完成仍结算到取得预留时所属日期")]
    public async Task CompleteUploadAsync_ShouldSettleToReservationBusinessDate()
    {
        const long userId = 10005;
        const long fileSize = 2048;
        var beforeMidnight = new DateTimeOffset(2026, 7, 11, 15, 59, 59, TimeSpan.Zero);
        var afterMidnight = new DateTimeOffset(2026, 7, 11, 16, 0, 1, TimeSpan.Zero);
        _timeProvider.SetUtcNow(beforeMidnight);
        (await _rateLimitService.AcquireUploadAsync(userId, "upload-a", fileSize, TimeSpan.FromHours(2)))
            .IsAllowed.ShouldBeTrue();

        _timeProvider.SetUtcNow(afterMidnight);
        await _rateLimitService.CompleteUploadAsync(userId, "upload-a");
        (await _rateLimitService.GetUploadStatisticsAsync(userId)).UploadedSizeToday.ShouldBe(0);

        _timeProvider.SetUtcNow(beforeMidnight);
        (await _rateLimitService.GetUploadStatisticsAsync(userId)).UploadedSizeToday.ShouldBe(fileSize);
    }

    [Fact(DisplayName = "过期预留不再占并发和日容量")]
    public async Task AcquireUploadAsync_ShouldPruneExpiredReservations()
    {
        const long userId = 10006;
        (await _rateLimitService.AcquireUploadAsync(userId, "upload-a", 1024, TimeSpan.FromSeconds(1)))
            .IsAllowed.ShouldBeTrue();
        _timeProvider.SetUtcNow(_timeProvider.GetUtcNow().AddSeconds(2));

        var statistics = await _rateLimitService.GetUploadStatisticsAsync(userId);

        statistics.CurrentConcurrentUploads.ShouldBe(0);
        statistics.ReservedUploadSizeToday.ShouldBe(0);
        statistics.UploadsThisMinute.ShouldBe(1);
    }

    [Fact(DisplayName = "禁用限流时 Acquire Complete Fail 均为空操作")]
    public async Task DisabledRateLimit_ShouldAllowWithoutAccounting()
    {
        var service = CreateService(new UploadRateLimitOptions
        {
            Enable = false,
            MaxConcurrentUploads = 1,
            MaxUploadsPerMinute = 1,
            MaxDailyUploadSize = 1
        });
        const long userId = 10007;

        (await service.AcquireUploadAsync(userId, "upload-a", 10_000)).IsAllowed.ShouldBeTrue();
        await service.CompleteUploadAsync(userId, "upload-a");
        await service.FailUploadAsync(userId, "upload-a");

        var statistics = await service.GetUploadStatisticsAsync(userId);
        statistics.CurrentConcurrentUploads.ShouldBe(0);
        statistics.UploadsThisMinute.ShouldBe(0);
        statistics.UploadedSizeToday.ShouldBe(0);
    }

    [Fact(DisplayName = "重置会清空当前用户的预留与计数")]
    public async Task ResetUserLimitsAsync_ShouldClearCurrentState()
    {
        const long userId = 10008;
        (await _rateLimitService.AcquireUploadAsync(userId, "upload-a", 1024)).IsAllowed.ShouldBeTrue();
        await _rateLimitService.CompleteUploadAsync(userId, "upload-a");
        (await _rateLimitService.AcquireUploadAsync(userId, "upload-b", 2048)).IsAllowed.ShouldBeTrue();

        await _rateLimitService.ResetUserLimitsAsync(userId);

        var statistics = await _rateLimitService.GetUploadStatisticsAsync(userId);
        statistics.CurrentConcurrentUploads.ShouldBe(0);
        statistics.UploadsThisMinute.ShouldBe(0);
        statistics.UploadedSizeToday.ShouldBe(0);
        statistics.ReservedUploadSizeToday.ShouldBe(0);
    }

    private UploadRateLimitService CreateService(UploadRateLimitOptions options)
    {
        return new UploadRateLimitService(
            _cache,
            Options.Create(options),
            Options.Create(new RedisOptions { Enable = false }),
            _timeProvider,
            _businessCalendar,
            Array.Empty<IConnectionMultiplexer>());
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset value)
        {
            _utcNow = value.ToUniversalTime();
        }
    }
}
