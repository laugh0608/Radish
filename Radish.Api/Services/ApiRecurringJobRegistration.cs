using Hangfire;
using Radish.Extension.Log;
using Radish.IService;
using Radish.Service.Jobs;
using Serilog;

namespace Radish.Api.Services;

/// <summary>按既有顺序注册周期任务；只在 AddOrUpdate 返回后报告，不消费注册失败。</summary>
public static class ApiRecurringJobRegistration
{
    public static void Register(IConfiguration configuration, TimeZoneInfo appDefaultTimeZone)
    {
        // 注册 Hangfire 定时任务
        RecurringJob.AddOrUpdate<Radish.Api.Services.ReliableOutboxDispatcherJob>(
            "reliable-outbox-dispatch",
            job => job.DispatchAsync(50),
            "*/1 * * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        HostProgramLog.RecurringJobRegistered(Log.Logger, "outbox-dispatch");

        RecurringJob.AddOrUpdate<ChatMessageReactionOperationCleanupJob>(
            "cleanup-chat-reaction-operations",
            job => job.ExecuteAsync(500),
            "15 4 * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        HostProgramLog.RecurringJobRegistered(Log.Logger, "chat-reactions");

        var wikiDraftCleanupConfig = configuration.GetSection("Hangfire:WikiDraftPayloadCleanup");
        if (wikiDraftCleanupConfig.GetValue<bool>("Enable", true))
        {
            var schedule = wikiDraftCleanupConfig["Schedule"] ?? "45 3 * * *";
            var batchSize = wikiDraftCleanupConfig.GetValue<int>("BatchSize", 200);
            var retentionDays = configuration.GetValue<int>("Document:Authoring:TerminalDraftRetentionDays", 90);
            RecurringJob.AddOrUpdate<WikiDraftPayloadCleanupJob>(
                "cleanup-wiki-terminal-draft-payloads",
                job => job.ExecuteAsync(retentionDays, batchSize),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "wiki-drafts");
        }

        var notificationCleanupConfig = configuration.GetSection("Hangfire:NotificationInboxCleanup");
        if (notificationCleanupConfig.GetValue<bool>("Enable", true))
        {
            var schedule = notificationCleanupConfig["Schedule"] ?? "30 3 * * *";
            var batchSize = notificationCleanupConfig.GetValue<int>("BatchSize", 200);
            var softRelationLimitPerUser = notificationCleanupConfig.GetValue<int>("SoftRelationLimitPerUser", 5000);
            RecurringJob.AddOrUpdate<NotificationInboxCleanupJob>(
                "cleanup-notification-inbox",
                job => job.ExecuteAsync(batchSize, softRelationLimitPerUser),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "notification-inbox");
        }

        var fileCleanupConfig = configuration.GetSection("Hangfire:FileCleanup");

        // 1. 软删除文件清理任务
        if (fileCleanupConfig.GetValue<bool>("DeletedFiles:Enable", true))
        {
            var retentionDays = fileCleanupConfig.GetValue<int>("DeletedFiles:RetentionDays", 30);
            var schedule = fileCleanupConfig["DeletedFiles:Schedule"] ?? "0 3 * * *";

            RecurringJob.AddOrUpdate<FileCleanupJob>(
                "cleanup-deleted-files",
                job => job.CleanupDeletedFilesAsync(retentionDays),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "file-deleted");
        }

        // 2. 临时文件清理任务
        if (fileCleanupConfig.GetValue<bool>("TempFiles:Enable", true))
        {
            var retentionHours = fileCleanupConfig.GetValue<int>("TempFiles:RetentionHours", 2);
            var schedule = fileCleanupConfig["TempFiles:Schedule"] ?? "0 * * * *";

            RecurringJob.AddOrUpdate<FileCleanupJob>(
                "cleanup-temp-files",
                job => job.CleanupTempFilesAsync(retentionHours),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "file-temp");
        }

        // 3. 回收站清理任务
        if (fileCleanupConfig.GetValue<bool>("RecycleBin:Enable", true))
        {
            var retentionDays = fileCleanupConfig.GetValue<int>("RecycleBin:RetentionDays", 90);
            var schedule = fileCleanupConfig["RecycleBin:Schedule"] ?? "0 4 * * *";

            RecurringJob.AddOrUpdate<FileCleanupJob>(
                "cleanup-recycle-bin",
                job => job.CleanupRecycleBinAsync(retentionDays),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "file-recycle");
        }

        // 4. 孤立附件清理任务
        if (fileCleanupConfig.GetValue<bool>("OrphanAttachments:Enable", true))
        {
            var retentionHours = fileCleanupConfig.GetValue<int>("OrphanAttachments:RetentionHours", 24);
            var schedule = fileCleanupConfig["OrphanAttachments:Schedule"] ?? "0 5 * * *";

            RecurringJob.AddOrUpdate<FileCleanupJob>(
                "cleanup-orphan-attachments",
                job => job.CleanupOrphanAttachmentsAsync(retentionHours),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "file-orphan");
        }

        // 注册分片上传会话清理任务；即使禁用新上传，也要继续回收历史会话。
        {
            // 必须短于会话接近到期时仍可剩余的 30 分钟配额宽限，
            // 让终态会话的幂等配额结算在预留自然过期前获得重试机会并保留调度抖动余量。
            var schedule = "*/15 * * * *"; // 每 15 分钟

            RecurringJob.AddOrUpdate<IChunkedUploadService>(
                "cleanup-expired-upload-sessions",
                service => service.CleanupExpiredSessionsAsync(),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "upload-sessions");
        }

        // 注册文件访问令牌清理任务
        {
            var schedule = "0 7 * * *"; // 每天凌晨 7 点

            RecurringJob.AddOrUpdate<IFileAccessTokenService>(
                "cleanup-expired-access-tokens",
                service => service.CleanupExpiredTokensAsync(),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "file-tokens");
        }

        // 神评/沙发统计任务
        var commentHighlightConfig = configuration.GetSection("Hangfire:CommentHighlight");
        if (commentHighlightConfig.GetValue<bool>("Enable", true))
        {
            var schedule = commentHighlightConfig["Schedule"] ?? "0 1 * * *";

            RecurringJob.AddOrUpdate<CommentHighlightJob>(
                "comment-highlight-stat",
                job => job.ExecuteAsync(null),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "comment-highlight");
        }

        // 保留奖励任务（神评/沙发每周奖励）
        var retentionRewardConfig = configuration.GetSection("Hangfire:RetentionReward");
        if (retentionRewardConfig.GetValue<bool>("Enable", true))
        {
            var schedule = retentionRewardConfig["Schedule"] ?? "0 2 * * 0";

            RecurringJob.AddOrUpdate<RetentionRewardJob>(
                "retention-reward",
                job => job.ExecuteAsync(),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "retention-rewards");
        }

        // 论坛抽奖自动开奖任务
        var postLotteryConfig = configuration.GetSection("Hangfire:PostLottery");
        if (postLotteryConfig.GetValue<bool>("Enable", true))
        {
            var schedule = postLotteryConfig["Schedule"] ?? "*/1 * * * *";
            var batchSize = postLotteryConfig.GetValue<int>("BatchSize", 20);

            RecurringJob.AddOrUpdate<PostLotteryJob>(
                "forum-post-lottery-auto-draw",
                job => job.ExecuteAutoDrawAsync(batchSize),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "post-lottery");
        }

        // 商城订单超时取消任务
        var shopConfig = configuration.GetSection("Hangfire:Shop");
        if (shopConfig.GetValue<bool>("TimeoutOrders:Enable", true))
        {
            var timeoutMinutes = shopConfig.GetValue<int>("TimeoutOrders:TimeoutMinutes", 30);
            var schedule = shopConfig["TimeoutOrders:Schedule"] ?? "*/10 * * * *"; // 默认每 10 分钟执行一次

            RecurringJob.AddOrUpdate<ShopJob>(
                "shop-cancel-timeout-orders",
                job => job.CancelTimeoutOrdersAsync(timeoutMinutes),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "shop-timeouts");
        }

        // 商城权益过期处理任务
        if (shopConfig.GetValue<bool>("ExpiredBenefits:Enable", true))
        {
            var schedule = shopConfig["ExpiredBenefits:Schedule"] ?? "0 0 * * *"; // 默认每天 00:00 执行

            RecurringJob.AddOrUpdate<ShopJob>(
                "shop-mark-expired-benefits",
                job => job.MarkExpiredBenefitsAsync(),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "shop-benefits");
        }

        // 商城每日统计任务
        if (shopConfig.GetValue<bool>("DailyStats:Enable", false))
        {
            var schedule = shopConfig["DailyStats:Schedule"] ?? "0 1 * * *"; // 默认每天 01:00 执行

            RecurringJob.AddOrUpdate<ShopJob>(
                "shop-daily-stats",
                job => job.GenerateDailyStatsAsync(),
                schedule,
                new RecurringJobOptions
                {
                    TimeZone = appDefaultTimeZone
                });

            HostProgramLog.RecurringJobRegistered(Log.Logger, "shop-stats");
        }
    }
}
