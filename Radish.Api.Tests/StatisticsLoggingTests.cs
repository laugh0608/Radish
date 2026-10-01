using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Common.Exceptions;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Service;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class StatisticsLoggingTests
{
    private const string Secret = "STATISTICS_PRIVATE_SENTINEL";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Aggregates_ShouldKeepValuesMappingAndDefaultLimitsQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        var dashboard = (await f.Controller.GetDashboardStats()).ResponseData!;
        Assert.Equal(5, dashboard.VoTotalUsers);
        Assert.Equal(34, dashboard.VoTotalOrders);
        Assert.Equal(6, dashboard.VoTotalProducts);
        Assert.Equal(123.45m, dashboard.VoTotalRevenue);
        foreach (var (input, expected) in new[] { (0, 10), (999, 50), (2, 2) })
        {
            var row = Assert.Single((await f.Controller.GetProductSalesRanking(input)).ResponseData!);
            Assert.Equal(Secret, row.VoProductName);
            Assert.Equal(7, row.VoSalesCount);
            Assert.Equal(87.65m, row.VoRevenue);
            f.Repository.Verify(r => r.GetProductSalesRankingAsync(expected), Times.Once);
        }
        var levels = (await f.Controller.GetUserLevelDistribution()).ResponseData!;
        Assert.Equal(new[] { 0, 2 }, levels.Select(l => l.VoLevel));
        Assert.Equal(3, levels[0].VoUserCount);
        Assert.Equal(Secret, levels[0].VoLevelName);
        Assert.Equal("Lv.2", levels[1].VoLevelName);
        Assert.Equal(2, levels[1].VoUserCount);
        f.Users.Verify(r => r.QueryCountAsync(It.Is<Expression<Func<User, bool>>>(p => p.Compile()(new User { IsDeleted = false }) && !p.Compile()(new User { IsDeleted = true }))), Times.Exactly(2));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Trend_ShouldKeepLocalDayWindowsAndRangeNormalizationQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var (input, count) in new[] { (0, 30), (999, 90), (2, 2) })
        {
            var f = new Fixture();
            var windows = new List<(DateTime Start, DateTime End)>();
            f.Repository.Setup(r => r.GetOrderCountAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .Callback<DateTime?, DateTime?>((start, end) => windows.Add((start!.Value, end!.Value))).ReturnsAsync(3);
            var before = DateTime.Today;
            var rows = (await f.Controller.GetOrderTrend(input)).ResponseData!;
            var after = DateTime.Today;
            Assert.Equal(count, rows.Count);
            Assert.Equal(count, windows.Count);
            Assert.InRange(windows[^1].Start, before, after);
            for (var i = 0; i < count; i++)
            {
                var (start, end) = windows[i];
                Assert.Equal(DateTimeKind.Local, start.Kind);
                Assert.Equal(TimeSpan.Zero, start.TimeOfDay);
                Assert.Equal(start.AddDays(1), end);
                Assert.Equal(start.ToString("yyyy-MM-dd"), rows[i].VoDate);
                Assert.Equal(3, rows[i].VoOrderCount);
                Assert.Equal(123.45m, rows[i].VoRevenue);
                if (i > 0) Assert.Equal(windows[i - 1].End, start);
                f.Repository.Verify(r => r.GetCompletedOrderRevenueAsync(start, end), Times.Once);
            }
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task AggregateFailures_ShouldKeepWrapperAndOneFinalError(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "users", "orders", "products", "revenue", "sales", "distribution", "experience-count", "levels" })
        {
            var f = new Fixture();
            var failure = new IOException(Secret);
            Func<Task> service = () => f.Service.GetDashboardStatsAsync();
            Func<Task> controller = () => f.Controller.GetDashboardStats();
            switch (stage)
            {
                case "users": f.Users.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<User, bool>>>())).ThrowsAsync(failure); break;
                case "orders": f.Repository.Setup(r => r.GetTotalOrderCountAsync()).ThrowsAsync(failure); break;
                case "products": f.Repository.Setup(r => r.GetTotalProductCountAsync()).ThrowsAsync(failure); break;
                case "revenue": f.Repository.Setup(r => r.GetCompletedOrderRevenueAsync(null, null)).ThrowsAsync(failure); break;
                case "sales":
                    f.Repository.Setup(r => r.GetProductSalesRankingAsync(It.IsAny<int>())).ThrowsAsync(failure);
                    service = () => f.Service.GetProductSalesRankingAsync(); controller = () => f.Controller.GetProductSalesRanking(); break;
                case "distribution": f.Repository.Setup(r => r.GetUserExperienceLevelDistributionAsync()).ThrowsAsync(failure); break;
                case "experience-count": f.Repository.Setup(r => r.GetUserExperienceUserCountAsync()).ThrowsAsync(failure); break;
                case "levels": f.Repository.Setup(r => r.GetLevelConfigsAsync()).ThrowsAsync(failure); break;
            }
            if (stage is "distribution" or "experience-count" or "levels")
            {
                service = () => f.Service.GetUserLevelDistributionAsync();
                controller = () => f.Controller.GetUserLevelDistribution();
            }
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(service));
            var wrapper = await Assert.ThrowsAsync<BusinessException>(controller);
            Assert.Same(failure, wrapper.InnerException);
            Assert.Equal(500, wrapper.StatusCode);
            capture.AssertQuiet();
            using var body = await InvokeApiAsync(controller, capture.Logger, 500);
            Assert.Equal("System.UnexpectedError", body.RootElement.GetProperty("code").GetString());
            Assert.Equal("error.system.unexpected_error", body.RootElement.GetProperty("messageKey").GetString());
            Assert.DoesNotContain(Secret, body.RootElement.GetRawText());
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task TrendFailureAfterProgress_ShouldNotReturnPartialDataOrContinueQuerying(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var failRevenue in new[] { false, true })
        {
            var f = new Fixture();
            if (failRevenue)
                f.Repository.SetupSequence(r => r.GetCompletedOrderRevenueAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(1m).ThrowsAsync(new IOException(Secret));
            else
                f.Repository.SetupSequence(r => r.GetOrderCountAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(1).ThrowsAsync(new IOException(Secret));
            using var body = await InvokeApiAsync(() => f.Controller.GetOrderTrend(3), capture.Logger, 500);
            f.Repository.Verify(r => r.GetOrderCountAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Exactly(2));
            f.Repository.Verify(r => r.GetCompletedOrderRevenueAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Exactly(failRevenue ? 2 : 1));
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task BusinessFailures_ShouldKeepExisting500WrappingForEveryEndpoint(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var status in new[] { 400, 409, 503 })
        {
            var f = new Fixture();
            var failure = new BusinessException(Secret, status, "Statistics.Test", "error.statistics.test");
            f.Users.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<User, bool>>>())).ThrowsAsync(failure);
            f.Repository.Setup(r => r.GetOrderCountAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ThrowsAsync(failure);
            f.Repository.Setup(r => r.GetProductSalesRankingAsync(It.IsAny<int>())).ThrowsAsync(failure);
            foreach (var call in new Func<Task>[] { () => f.Controller.GetDashboardStats(), () => f.Controller.GetOrderTrend(),
                () => f.Controller.GetProductSalesRanking(), () => f.Controller.GetUserLevelDistribution() })
            {
                var wrapper = await Assert.ThrowsAsync<BusinessException>(call);
                Assert.Same(failure, wrapper.InnerException);
                Assert.Equal(500, wrapper.StatusCode);
                capture.AssertQuiet();
                using var body = await InvokeApiAsync(call, capture.Logger, 500);
                Assert.Equal("System.UnexpectedError", body.RootElement.GetProperty("code").GetString());
                capture.AssertSingle("http.failed", "Error");
                capture.Clear();
            }
        }
    }

    private sealed class Fixture
    {
        public Mock<IBaseRepository<User>> Users { get; } = new();
        public Mock<IStatisticsRepository> Repository { get; } = new();
        public StatisticsService Service { get; }
        public StatisticsController Controller { get; }
        public Fixture()
        {
            Users.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(5);
            Repository.Setup(r => r.GetTotalOrderCountAsync()).ReturnsAsync(34);
            Repository.Setup(r => r.GetTotalProductCountAsync()).ReturnsAsync(6);
            Repository.Setup(r => r.GetCompletedOrderRevenueAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(123.45m);
            Repository.Setup(r => r.GetProductSalesRankingAsync(It.IsAny<int>())).ReturnsAsync([new() { ProductName = Secret, SalesCount = 7, Revenue = 87.65m }]);
            Repository.Setup(r => r.GetUserExperienceLevelDistributionAsync()).ReturnsAsync([new() { Level = 2, UserCount = 2 }]);
            Repository.Setup(r => r.GetUserExperienceUserCountAsync()).ReturnsAsync(2);
            Repository.Setup(r => r.GetLevelConfigsAsync()).ReturnsAsync([new() { Level = 0, LevelName = Secret }]);
            Service = new(Users.Object, Repository.Object);
            Controller = new(Service);
        }
    }

    private static async Task<JsonDocument> InvokeApiAsync(Func<Task> action, Serilog.ILogger logger, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        app.Run(async _ => await action());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Statistics/GetDashboardStats";
        context.Request.QueryString = new QueryString("?query=" + Secret);
        using var output = new MemoryStream();
        context.Response.Body = output;
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
        output.Position = 0;
        return await JsonDocument.ParseAsync(output, cancellationToken: TestContext.Current.CancellationToken);
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        public Serilog.Core.Logger Logger { get; }
        public string[] Lines => Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate) RuntimeLoggingConfiguration.Configure(config, new ConfigurationBuilder().Build(), environment, "api", Output, Output);
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(Output));
            Logger = config.CreateLogger(); Log.Logger = Logger;
        }
        public void AssertQuiet() => Assert.Equal("", Output.ToString());
        public void AssertSingle(string code, string level)
        {
            Assert.Single(Lines);
            Assert.Contains(code, Output.ToString());
            Assert.Contains(level, Output.ToString());
            AssertSafe();
        }
        public void AssertSafe()
        {
            Assert.DoesNotContain(Secret, Output.ToString());
            Assert.DoesNotContain("runtime.unclassified", Output.ToString());
        }
        public void Clear() => Output.GetStringBuilder().Clear();
        public void Dispose() { Log.Logger = _previous; Logger.Dispose(); Output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
