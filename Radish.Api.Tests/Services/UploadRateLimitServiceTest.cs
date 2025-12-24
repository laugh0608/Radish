using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.IService;
using Radish.Service;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>
/// 上传限流服务测试
/// </summary>
public class UploadRateLimitServiceTest
{
    private readonly IUploadRateLimitService _rateLimitService;
    private readonly ICaching _cache;
    private readonly UploadRateLimitOptions _options;

    public UploadRateLimitServiceTest()
    {
        // 使用内存缓存进行测试
        var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _cache = new Caching(distributedCache);

        // 配置测试选项（降低限制便于测试）
        _options = new UploadRateLimitOptions
        {
            Enable = true,
            MaxConcurrentUploads = 3,
            MaxUploadsPerMinute = 5,
            MaxDailyUploadSize = 10 * 1024 * 1024 // 10MB
        };

        _rateLimitService = new UploadRateLimitService(_cache, Options.Create(_options));
    }

    [Fact(DisplayName = "测试并发上传限制")]
    public async Task TestConcurrentUploadLimit()
    {
        // Arrange
        long userId = 10001;
        long fileSize = 1024; // 1KB

        // Act & Assert - 前 3 个上传应该成功
        for (int i = 1; i <= 3; i++)
        {
            var uploadId = $"upload-{i}";
            var (isAllowed, errorMessage) = await _rateLimitService.CheckUploadAllowedAsync(userId, fileSize);
            isAllowed.ShouldBeTrue($"第 {i} 个上传应该被允许");
            errorMessage.ShouldBeNull();

            await _rateLimitService.RecordUploadStartAsync(userId, uploadId);
        }

        // 第 4 个上传应该被拒绝（超过并发限制）
        var (isAllowed4, errorMessage4) = await _rateLimitService.CheckUploadAllowedAsync(userId, fileSize);
        isAllowed4.ShouldBeFalse("第 4 个上传应该被拒绝");
        errorMessage4.ShouldNotBeNull();
        errorMessage4.ShouldContain("并发上传限制");

        // 完成一个上传后，应该可以开始新的上传
        await _rateLimitService.RecordUploadCompleteAsync(userId, "upload-1", fileSize);

        var (isAllowed5, errorMessage5) = await _rateLimitService.CheckUploadAllowedAsync(userId, fileSize);
        isAllowed5.ShouldBeTrue("完成一个上传后，新上传应该被允许");
    }

    [Fact(DisplayName = "测试速率限制")]
    public async Task TestRateLimit()
    {
        // Arrange
        long userId = 10002;
        long fileSize = 1024; // 1KB

        // Act & Assert - 前 5 个上传应该成功
        for (int i = 1; i <= 5; i++)
        {
            var uploadId = $"upload-{i}";
            var (isAllowed, errorMessage) = await _rateLimitService.CheckUploadAllowedAsync(userId, fileSize);
            isAllowed.ShouldBeTrue($"第 {i} 个上传应该被允许");

            await _rateLimitService.RecordUploadStartAsync(userId, uploadId);
            await _rateLimitService.RecordUploadCompleteAsync(userId, uploadId, fileSize);
        }

        // 第 6 个上传应该被拒绝（超过速率限制）
        var (isAllowed6, errorMessage6) = await _rateLimitService.CheckUploadAllowedAsync(userId, fileSize);
        isAllowed6.ShouldBeFalse("第 6 个上传应该被拒绝");
        errorMessage6.ShouldNotBeNull();
        errorMessage6.ShouldContain("速率限制");
    }

    [Fact(DisplayName = "测试日上传大小限制")]
    public async Task TestDailySizeLimit()
    {
        // Arrange
        long userId = 10003;
        long fileSize = 3 * 1024 * 1024; // 3MB

        // Act & Assert - 前 3 个上传应该成功（共 9MB）
        for (int i = 1; i <= 3; i++)
        {
            var uploadId = $"upload-{i}";
            var (isAllowed, errorMessage) = await _rateLimitService.CheckUploadAllowedAsync(userId, fileSize);
            isAllowed.ShouldBeTrue($"第 {i} 个上传应该被允许");

            await _rateLimitService.RecordUploadStartAsync(userId, uploadId);
            await _rateLimitService.RecordUploadCompleteAsync(userId, uploadId, fileSize);
        }

        // 第 4 个上传应该被拒绝（9MB + 3MB = 12MB > 10MB 限制）
        var (isAllowed4, errorMessage4) = await _rateLimitService.CheckUploadAllowedAsync(userId, fileSize);
        isAllowed4.ShouldBeFalse("第 4 个上传应该被拒绝");
        errorMessage4.ShouldNotBeNull();
        errorMessage4.ShouldContain("今日");
    }

    [Fact(DisplayName = "测试上传失败时减少并发计数")]
    public async Task TestUploadFailedReducesConcurrentCount()
    {
        // Arrange
        long userId = 10004;
        long fileSize = 1024;

        // Act - 开始 3 个上传
        for (int i = 1; i <= 3; i++)
        {
            var uploadId = $"upload-{i}";
            await _rateLimitService.RecordUploadStartAsync(userId, uploadId);
        }

        // 第 4 个上传应该被拒绝
        var (isAllowed1, _) = await _rateLimitService.CheckUploadAllowedAsync(userId, fileSize);
        isAllowed1.ShouldBeFalse();

        // 标记第 1 个上传失败
        await _rateLimitService.RecordUploadFailedAsync(userId, "upload-1");

        // Assert - 现在应该可以开始新的上传
        var (isAllowed2, _) = await _rateLimitService.CheckUploadAllowedAsync(userId, fileSize);
        isAllowed2.ShouldBeTrue("上传失败后，新上传应该被允许");
    }

    [Fact(DisplayName = "测试获取上传统计信息")]
    public async Task TestGetUploadStatistics()
    {
        // Arrange
        long userId = 10005;
        long fileSize = 2 * 1024 * 1024; // 2MB

        // Act - 开始 2 个上传，完成 1 个
        await _rateLimitService.RecordUploadStartAsync(userId, "upload-1");
        await _rateLimitService.RecordUploadStartAsync(userId, "upload-2");
        await _rateLimitService.RecordUploadCompleteAsync(userId, "upload-1", fileSize);

        // 获取统计信息
        var statistics = await _rateLimitService.GetUploadStatisticsAsync(userId);

        // Assert
        statistics.ShouldNotBeNull();
        statistics.CurrentConcurrentUploads.ShouldBe(1, "应该有 1 个正在上传的文件");
        statistics.UploadsThisMinute.ShouldBe(1, "本分钟应该有 1 个已完成的上传");
        statistics.UploadedSizeToday.ShouldBe(fileSize, "今日上传大小应该等于已完成文件的大小");
        statistics.MaxConcurrentUploads.ShouldBe(_options.MaxConcurrentUploads);
        statistics.MaxUploadsPerMinute.ShouldBe(_options.MaxUploadsPerMinute);
        statistics.MaxDailyUploadSize.ShouldBe(_options.MaxDailyUploadSize);
    }

    [Fact(DisplayName = "测试重置用户限流计数")]
    public async Task TestResetUserLimits()
    {
        // Arrange
        long userId = 10006;
        long fileSize = 1024;

        // Act - 开始一些上传
        await _rateLimitService.RecordUploadStartAsync(userId, "upload-1");
        await _rateLimitService.RecordUploadStartAsync(userId, "upload-2");
        await _rateLimitService.RecordUploadCompleteAsync(userId, "upload-1", fileSize);

        // 重置限流计数
        await _rateLimitService.ResetUserLimitsAsync(userId);

        // 获取统计信息
        var statistics = await _rateLimitService.GetUploadStatisticsAsync(userId);

        // Assert - 所有计数应该被重置为 0
        statistics.CurrentConcurrentUploads.ShouldBe(0);
        statistics.UploadsThisMinute.ShouldBe(0);
        statistics.UploadedSizeToday.ShouldBe(0);
    }

    [Fact(DisplayName = "测试不同用户的限流独立")]
    public async Task TestDifferentUsersAreIndependent()
    {
        // Arrange
        long user1 = 10007;
        long user2 = 10008;
        long fileSize = 1024;

        // Act - 用户 1 达到并发限制
        for (int i = 1; i <= 3; i++)
        {
            await _rateLimitService.RecordUploadStartAsync(user1, $"user1-upload-{i}");
        }

        var (user1Allowed, _) = await _rateLimitService.CheckUploadAllowedAsync(user1, fileSize);
        var (user2Allowed, _) = await _rateLimitService.CheckUploadAllowedAsync(user2, fileSize);

        // Assert
        user1Allowed.ShouldBeFalse("用户 1 应该被限流");
        user2Allowed.ShouldBeTrue("用户 2 不应该被限流");
    }

    [Fact(DisplayName = "测试限流禁用时允许所有上传")]
    public async Task TestDisabledRateLimitAllowsAllUploads()
    {
        // Arrange
        var disabledOptions = new UploadRateLimitOptions
        {
            Enable = false,
            MaxConcurrentUploads = 1,
            MaxUploadsPerMinute = 1,
            MaxDailyUploadSize = 1024
        };

        var disabledService = new UploadRateLimitService(_cache, Options.Create(disabledOptions));
        long userId = 10009;
        long fileSize = 10 * 1024 * 1024; // 10MB

        // Act - 即使超过所有限制，也应该被允许
        var (isAllowed, errorMessage) = await disabledService.CheckUploadAllowedAsync(userId, fileSize);

        // Assert
        isAllowed.ShouldBeTrue("禁用限流时应该允许所有上传");
        errorMessage.ShouldBeNull();
    }

    [Fact(DisplayName = "测试文件大小格式化")]
    public async Task TestFileSizeFormatting()
    {
        // Arrange
        long userId = 10010;
        long fileSize = 5 * 1024 * 1024; // 5MB

        // Act
        await _rateLimitService.RecordUploadStartAsync(userId, "upload-1");
        await _rateLimitService.RecordUploadCompleteAsync(userId, "upload-1", fileSize);

        var statistics = await _rateLimitService.GetUploadStatisticsAsync(userId);

        // Assert
        statistics.UploadedSizeTodayFormatted.ShouldContain("MB");
        statistics.MaxDailyUploadSizeFormatted.ShouldContain("MB");
    }
}
