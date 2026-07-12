using System;
using System.IO;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.DbMigrate;
using Radish.Model;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class TimeSemanticsAuditTest
{
    [Fact]
    public void Inspect_ShouldReportOnlyMisalignedNaturalDates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-time-audit-{Guid.NewGuid():N}.db");
        var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

        try
        {
            db.CodeFirst.InitTables<ExpTransaction, UserExpDailyStats, CommentHighlight, UserExperienceGovernanceAction>();
            var businessDateStartUtc = new DateTime(2026, 7, 11, 16, 0, 0, DateTimeKind.Utc);
            var naturalDate = new DateTime(2026, 7, 12);
            db.Insertable(new ExpTransaction
            {
                Id = 1,
                UserId = 1,
                ExpType = "TEST",
                CreatedDate = naturalDate,
                CreateTime = businessDateStartUtc
            }).ExecuteCommand();
            db.Insertable(new ExpTransaction
            {
                Id = 2,
                UserId = 1,
                ExpType = "TEST",
                CreatedDate = naturalDate.AddHours(1),
                CreateTime = businessDateStartUtc
            }).ExecuteCommand();
            db.Insertable(new UserExpDailyStats
            {
                Id = 3,
                UserId = 1,
                StatDate = naturalDate,
                CreateTime = businessDateStartUtc
            }).ExecuteCommand();
            db.Insertable(new CommentHighlight
            {
                Id = 4,
                PostId = 10,
                CommentId = 20,
                HighlightType = 1,
                StatDate = new DateTime(2026, 7, 12),
                AuthorId = 1,
                AuthorName = "tester",
                CreateTime = businessDateStartUtc
            }).ExecuteCommand();
            db.Insertable(new UserExperienceGovernanceAction
            {
                Id = 5,
                TargetUserId = 1,
                ActionType = 1,
                Remark = "review",
                StatDate = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc),
                CreateTime = businessDateStartUtc
            }).ExecuteCommand();
            var timeProvider = new FixedTimeProvider(new DateTimeOffset(businessDateStartUtc));
            var calendar = new BusinessCalendar(
                timeProvider,
                Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" }));

            var result = TimeSemanticsAudit.Inspect(db, calendar);

            Assert.Single(result.Warnings);
            Assert.Contains("ExpTransaction.CreatedDate", result.Warnings[0], StringComparison.Ordinal);
            Assert.Contains("2", result.Warnings[0], StringComparison.Ordinal);
            Assert.Contains(result.Summaries, summary =>
                summary.Contains("UserExpDailyStats.StatDate", StringComparison.Ordinal) &&
                summary.Contains("类型 date", StringComparison.Ordinal) &&
                summary.Contains("异常 0 行", StringComparison.Ordinal));
            Assert.Contains(result.Summaries, summary =>
                summary.Contains("UserExperienceGovernanceAction.StatDate", StringComparison.Ordinal) &&
                summary.Contains("date 自然日", StringComparison.Ordinal));
        }
        finally
        {
            db.Dispose();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
