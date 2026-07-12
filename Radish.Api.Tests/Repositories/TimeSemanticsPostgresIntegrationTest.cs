using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.DbMigrate;
using Radish.Model;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class TimeSemanticsPostgresIntegrationTest
{
    private const string ConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task Audit_ShouldRecognizePostgreSqlDateAndTimestampContracts()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {ConnectionStringEnvironmentVariable}，跳过 PostgreSQL 时间语义集成测试");

        var schema = $"q2a_time_{Guid.NewGuid():N}";
        using var adminDb = CreateClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = CreateClient(connectionString);
            db.CodeFirst.InitTables<ExpTransaction, UserExpDailyStats, CommentHighlight, UserExperienceGovernanceAction>();

            var businessDateStartUtc = new DateTime(2026, 7, 11, 16, 0, 0, DateTimeKind.Utc);
            db.Insertable(new ExpTransaction
            {
                Id = 1,
                UserId = 1,
                ExpType = "DAILY_LOGIN",
                CreatedDate = businessDateStartUtc,
                CreateTime = businessDateStartUtc
            }).ExecuteCommand();
            db.Insertable(new UserExpDailyStats
            {
                Id = 2,
                UserId = 1,
                StatDate = businessDateStartUtc,
                CreateTime = businessDateStartUtc
            }).ExecuteCommand();
            db.Insertable(new CommentHighlight
            {
                Id = 3,
                PostId = 10,
                CommentId = 20,
                HighlightType = 1,
                StatDate = new DateTime(2026, 7, 12),
                AuthorId = 1,
                AuthorName = "tester",
                CreateTime = businessDateStartUtc
            }).ExecuteCommand();

            var calendar = new BusinessCalendar(
                new FixedTimeProvider(new DateTimeOffset(businessDateStartUtc)),
                Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" }));
            var result = TimeSemanticsAudit.Inspect(db, calendar);

            Assert.Empty(result.Warnings);
            Assert.Contains(result.Summaries, summary =>
                summary.Contains("ExpTransaction.CreatedDate", StringComparison.Ordinal) &&
                summary.Contains("timestamp", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Summaries, summary =>
                summary.Contains("CommentHighlight.StatDate", StringComparison.Ordinal) &&
                summary.Contains("类型 date", StringComparison.Ordinal));

            var dailyStatsColumn = db.DbMaintenance
                .GetColumnInfosByTableName("UserExpDailyStats", false)
                .Single(column => string.Equals(column.DbColumnName, "StatDate", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("timestamp", dailyStatsColumn.DataType, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static SqlSugarClient CreateClient(string connectionString)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
