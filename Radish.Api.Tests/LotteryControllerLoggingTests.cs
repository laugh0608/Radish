using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Filters;
using Radish.Common.CoreTool;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.Extension.AopExtension;
using Radish.Extension.AutoMapperExtension.CustomProfiles;
using Radish.Extension.Log;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Repository;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using Radish.Service.Jobs;
using Radish.Shared.Constants;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class LotteryControllerLoggingTests
{
    private const string Secret = "LOTTERY_PRIVATE_SENTINEL";
    private const long PostId = 912345678;
    private const long UserId = 623456789;
    private const long LotteryId = 734567891;
    private const long WinnerA = 823456789;
    private const long WinnerB = 823456788;
    // Job 使用系统 UTC 扫描；本轮固定秒级快照，让自动截止时间始终在过去。
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    private static readonly string[] Operations = ["query", "draw"];
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Consumers_ShouldKeepResponseAndOwnOnlyConsumedFailures(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var failure in new Exception[]
        {
            new UnsafeArgument(), new LotteryInputValidationException(Secret, "postId"),
            new BusinessException(Secret, 400, "Private.Code", "Private.Key", Secret),
            new BusinessException(Secret, 403, "Private.Code", "Private.Key", Secret),
            new BusinessException(Secret, 404, "Private.Code", "Private.Key", Secret),
            new BusinessException(Secret, 409, "Private.Code", "Private.Key", Secret),
            new BusinessException(Secret, 500, "Private.Code", "Private.Key", Secret),
            new BusinessException(Secret, 503, "Private.Code", "Private.Key", Secret)
        })
        {
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IPostLotteryService>(new ThrowingInterceptor(failure));
            var controller = CreateController(service, () => new CurrentUser { UserId = UserId, UserName = Secret });
            var status = failure is BusinessException business ? business.StatusCode : 400;
            var result = await InvokeApiAsync(() => CallControllerAsync(controller, operation), status);
            Assert.NotNull(result);
            Assert.Equal(failure.Message, result.MessageInfo);
            if (failure is BusinessException expected)
            {
                Assert.Equal(expected.ErrorCode, result.Code);
                Assert.Equal(expected.MessageKey, result.MessageKey);
                Assert.Null(result.MessageArguments);
            }
            if (failure is UnsafeArgument) capture.AssertSingle("lottery.request_failed", "argument");
            else if (status >= 500) capture.AssertSingle("http.failed", "other", status);
            else capture.AssertQuiet();
            capture.Clear();
        }
        foreach (var operation in Operations)
        foreach (var (failure, kind) in new (Exception, string)[]
        {
            (new IOException(Secret), "io"), (new InvalidOperationException(Secret), "invalid-operation"),
            (new TimeoutException(Secret), "timeout"), (new OperationCanceledException(Secret), "cancelled"),
            (new HostileFailure(), "other"), (new UnreadableArgument(), "io"), (new UnreadableBusiness(), "io"),
            (new AggregateException(new UnsafeArgument()), "aggregate"),
            (new AggregateException(new UnsafeArgument(), new IOException(Secret)), "aggregate")
        })
        {
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IPostLotteryService>(new ThrowingInterceptor(failure));
            var controller = CreateController(service, () => new CurrentUser { UserId = UserId, UserName = Secret });
            Assert.Null(await InvokeApiAsync(() => CallControllerAsync(controller, operation), 500));
            capture.AssertSingle("http.failed", kind, 500);
            capture.Clear();
        }
    }


    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ExplicitValidationAndBusinessRejections_ShouldStayQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var invalid in new[] { 0L, -1L })
        {
            using var f = new Fixture(operation) { RequestPostId = invalid };
            var error = await Assert.ThrowsAsync<LotteryInputValidationException>(f.CallServiceAsync);
            Assert.Equal("postId", error.ParamName);
            Assert.Equal(new ArgumentException("帖子ID必须大于0", "postId").Message, error.Message);
            await InvokeApiAsync(f.CallAsync, 400);
            Assert.Empty(f.Steps);
            f.AssertBaseline();
            var automatic = await Assert.ThrowsAsync<ArgumentException>(() => f.Service.AutoDrawByPostIdAsync(invalid));
            Assert.Equal(error.Message, automatic.Message);
            capture.AssertQuiet();
        }
        foreach (var invalidModel in new[] { false, true })
        {
            using var f = new Fixture("draw") { CurrentUserId = 0 };
            if (invalidModel) f.Controller.ModelState.AddModelError("request", Secret);
            await InvokeApiAsync(f.CallAsync, invalidModel ? 400 : 401);
            Assert.Empty(f.Steps);
            var error = await Assert.ThrowsAsync<BusinessException>(f.CallServiceAsync);
            Assert.Equal(401, error.StatusCode);
            Assert.Equal("Auth.Unauthorized", error.ErrorCode);
            f.AssertBaseline();
            capture.AssertQuiet();
        }
        foreach (var operation in Operations)
        {
            var scenarios = new List<string> { "post-missing", "post-deleted", "unpublished", "disabled", "lottery-missing", "lottery-deleted" };
            if (operation == "draw") scenarios.AddRange(["not-author", "drawn", "deadline-missing", "too-early", "at-deadline", "expired", "empty"]);
            foreach (var scenario in scenarios)
            {
                using var f = new Fixture(operation);
                var status = 404;
                var code = "Forum.PostNotFound";
                var key = "error.forum.post_not_found";
                var tooRecent = Now.UtcDateTime.AddMinutes(-59);
                var deadline = Now.UtcDateTime.AddSeconds(scenario == "expired" ? -1 : 0);
                switch (scenario)
                {
                    case "post-missing": f.Db.Deleteable<Post>().Where(p => p.Id == PostId).ExecuteCommand(); break;
                    case "post-deleted": f.Db.Updateable<Post>().Where(p => p.Id == PostId).SetColumns(p => p.IsDeleted == true).ExecuteCommand(); break;
                    case "unpublished": f.Db.Updateable<Post>().Where(p => p.Id == PostId).SetColumns(p => p.IsPublished == false).ExecuteCommand(); break;
                    case "disabled": f.Db.Updateable<Post>().Where(p => p.Id == PostId).SetColumns(p => p.IsEnabled == false).ExecuteCommand(); break;
                    case "lottery-missing": f.Db.Deleteable<PostLottery>().Where(l => l.Id == LotteryId).ExecuteCommand(); code = "Lottery.NotFound"; key = "error.lottery.not_found"; break;
                    case "lottery-deleted": f.Db.Updateable<PostLottery>().Where(l => l.Id == LotteryId).SetColumns(l => l.IsDeleted == true).ExecuteCommand(); code = "Lottery.NotFound"; key = "error.lottery.not_found"; break;
                    case "not-author": f.CurrentUserId = 99; status = 403; code = "Lottery.DrawForbidden"; key = "error.lottery.draw_forbidden"; break;
                    case "drawn": f.Db.Updateable<PostLottery>().Where(l => l.Id == LotteryId).SetColumns(l => l.IsDrawn == true).ExecuteCommand(); status = 409; code = "Lottery.AlreadyDrawn"; key = "error.lottery.already_drawn"; break;
                    case "deadline-missing": f.Db.Updateable<PostLottery>().Where(l => l.Id == LotteryId).SetColumns(l => l.DrawTime == null).ExecuteCommand(); status = 409; code = "Lottery.DeadlineMissing"; key = "error.lottery.deadline_missing"; break;
                    case "too-early": f.Db.Updateable<Post>().Where(p => p.Id == PostId).SetColumns(p => p.PublishTime == tooRecent).ExecuteCommand(); status = 409; code = "Lottery.DrawTooEarly"; key = "error.lottery.draw_too_early"; break;
                    case "at-deadline":
                    case "expired": f.Db.Updateable<PostLottery>().Where(l => l.Id == LotteryId).SetColumns(l => l.DrawTime == deadline).ExecuteCommand(); status = 409; code = "Lottery.AutomaticDrawPending"; key = "error.lottery.automatic_draw_pending"; break;
                    case "empty": f.RemoveComments(); status = 409; code = "Lottery.NoEligibleParticipant"; key = "error.lottery.no_eligible_participant"; break;
                }
                var baseline = f.Snapshot();
                var response = await InvokeApiAsync(f.CallAsync, status);
                Assert.Equal(code, response!.Code);
                Assert.Equal(key, response.MessageKey);
                Assert.Equal(baseline, f.Snapshot());
                if (operation == "draw" && scenario == "disabled")
                {
                    Assert.Contains("outbox.AddAsync", f.Steps);
                    Assert.Equal(1, f.OutboxWrites);
                }
                Assert.Equal(0, f.Unit.TranCount);
                capture.AssertQuiet();
            }
        }
        using var future = new Fixture("draw");
        var tooEarly = await Assert.ThrowsAsync<BusinessException>(() => future.Service.AutoDrawByPostIdAsync(PostId));
        Assert.Equal("Lottery.DrawTooEarly", tooEarly.ErrorCode);
        future.AssertBaseline();
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DependencyArgumentFailures_ShouldBeOwnedByControllerAndRollback(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        {
            var stages = new List<string> { "detail.GetPostDetailAsync", "posts.QueryByIdAsync", "post-tags.QueryAsync", "polls.QueryFirstAsync", "lotteries.QueryFirstAsync", "comments.QueryAsync", "winners.QueryAsync", "questions.QueryFirstAsync" };
            if (operation == "draw") stages.AddRange(["posts.QueryFirstAsync", "clock", "winners.AddRangeAsync", "lotteries.UpdateAsync", "outbox.AddAsync", "outbox.after-write"]);
            foreach (var stage in stages)
            {
                using var f = new Fixture(operation) { FailedStage = stage };
                var response = await InvokeApiAsync(f.CallAsync, 400);
                Assert.Equal(f.Failure.Message, response!.MessageInfo);
                Assert.Contains(stage, f.Steps);
                f.AssertBaseline();
                if (operation == "draw" && (stage.StartsWith("detail.", StringComparison.Ordinal) || stage == "outbox.after-write"))
                    Assert.Equal(1, f.OutboxWrites);
                capture.AssertSingle("lottery.request_failed", "argument");
                capture.Clear();
            }
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task LateFailures_ShouldKeepDirectPropagationHttpOwnershipAndJobOwnership(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var (failure, kind) in new (Exception, string)[]
        {
            (new UnsafeArgument(), "argument"), (new IOException(Secret), "io"),
            (new InvalidOperationException(Secret), "invalid-operation"), (new TimeoutException(Secret), "timeout"),
            (new OperationCanceledException(Secret), "cancelled"), (new BusinessException(Secret, 409), "other"),
            (new BusinessException(Secret, 503), "other"), (new AggregateException(new UnsafeArgument()), "aggregate"),
            (new HostileFailure(), "other"), (new UnreadableArgument(), "argument"), (new UnreadableBusiness(), "other")
        })
        foreach (var stage in new[] { "outbox.after-write", "detail.GetPostDetailAsync" })
        {
            // TranAop 原有单一 AggregateException 解包，直接 Controller mock 则不解包。
            var propagated = failure is AggregateException aggregate ? aggregate.InnerExceptions.Single() : failure;
            var propagatedKind = failure is AggregateException ? "argument" : kind;
            using (var direct = new Fixture("draw") { FailedStage = stage, Failure = failure })
            {
                Exception? actual = null;
                try { await direct.CallServiceAsync(); }
                catch (Exception caught) { actual = caught; }
                Assert.True(ReferenceEquals(propagated, actual), $"异常实例应保留，实际类型：{actual?.GetType().Name}");
                direct.AssertBaseline();
                Assert.Equal(1, direct.OutboxWrites);
                capture.AssertQuiet();
            }
            using (var manual = new Fixture("draw") { FailedStage = stage, Failure = failure })
            {
                var unreadable = propagated is UnreadableArgument or UnreadableBusiness;
                var status = unreadable ? 500 : propagated is ArgumentException ? 400 : propagated is BusinessException b ? b.StatusCode : 500;
                await InvokeApiAsync(manual.CallAsync, status);
                if (failure is BusinessException business && business.StatusCode < 500) capture.AssertQuiet();
                else capture.AssertSingle(status == 400 ? "lottery.request_failed" : "http.failed", unreadable ? "io" : propagatedKind, status == 400 ? null : status);
                manual.AssertBaseline();
                Assert.Equal(1, manual.OutboxWrites);
                capture.Clear();
            }
            using (var automatic = new Fixture("auto") { FailedStage = stage, Failure = failure })
            {
                Assert.Equal(0, await automatic.Job.ExecuteAutoDrawAsync());
                automatic.AssertBaseline();
                Assert.Equal(1, automatic.OutboxWrites);
                capture.AssertJob(0, 1, propagatedKind);
                capture.Clear();
            }
        }
        foreach (var operation in new[] { "draw", "auto" })
        {
            using var missingOutbox = new Fixture(operation, hasOutbox: false);
            if (operation == "draw")
            {
                await InvokeApiAsync(missingOutbox.CallAsync, 500);
                capture.AssertSingle("http.failed", "invalid-operation", 500);
            }
            else
            {
                Assert.Equal(0, await missingOutbox.Job.ExecuteAutoDrawAsync());
                capture.AssertJob(0, 1, "invalid-operation");
            }
            missingOutbox.AssertBaseline();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SuccessfulDraws_ShouldPreservePoolAuditOutboxAndResult(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in new[] { "draw", "auto" })
        foreach (var fallback in new[] { false, true })
        {
            using var f = new Fixture(operation) { CurrentUserName = fallback ? "  " : "  " + Secret + "  " };
            // 满一小时可手动开奖；姓名、标题和奖品的空白回退仍使用原规则。
            var publishedAt = Now.UtcDateTime.AddHours(-1);
            var title = fallback ? "  " : "  " + Secret + "  ";
            f.Db.Updateable<Post>().Where(p => p.Id == PostId).SetColumns(p => new Post { PublishTime = publishedAt, CreateTime = publishedAt, Title = title }).ExecuteCommand();
            f.Db.Updateable<PostLottery>().Where(l => l.Id == LotteryId).SetColumns(l => l.PrizeName == (fallback ? "  " : "  " + Secret + "  ")).ExecuteCommand();
            var result = operation == "auto" ? await f.Service.AutoDrawByPostIdAsync(PostId) : Assert.IsType<PostLotteryVo>((await InvokeApiAsync(f.CallAsync, 200))!.ResponseData);
            var operatorName = operation == "auto" ? "LotteryAutoDrawJob" : fallback ? $"User-{UserId}" : Secret;
            Assert.True(result.VoIsDrawn);
            Assert.Equal(LotteryId, result.VoLotteryId);
            Assert.Equal(PostId, result.VoPostId);
            Assert.Equal(2, result.VoParticipantCount);
            Assert.Equal(99, result.VoWinnerCount);
            Assert.Equal(f.Cutoff, result.VoDrawnAt);
            Assert.Equal(new[] { WinnerB, WinnerA }, result.VoWinners.Select(w => w.VoUserId).Order().ToArray());
            var winners = f.Db.Queryable<PostLotteryWinner>().OrderBy(w => w.UserId).ToList();
            Assert.Equal(2, winners.Count);
            foreach (var winner in winners)
            {
                Assert.Equal(LotteryId, winner.LotteryId);
                Assert.Equal(PostId, winner.PostId);
                Assert.Equal(0, winner.TenantId);
                Assert.Equal(f.Cutoff, winner.DrawnAt);
                Assert.Equal(f.Cutoff, winner.CreateTime);
                Assert.Equal(operatorName, winner.CreateBy);
                Assert.Equal(operation == "auto" ? 0 : UserId, winner.CreateId);
                Assert.Equal(winner.UserId == WinnerA ? 101 : 103, winner.CommentId);
                Assert.Equal(winner.UserId == WinnerA ? Secret : $"User-{WinnerB}", winner.UserName);
                Assert.Equal(winner.UserId == WinnerA ? new string('x', 500) : null, winner.CommentContentSnapshot);
            }
            var lottery = f.Db.Queryable<PostLottery>().InSingle(LotteryId);
            Assert.True(lottery.IsDrawn);
            Assert.Equal(2, lottery.ParticipantCount);
            Assert.Equal(f.Cutoff, lottery.DrawnAt);
            Assert.Equal(f.Cutoff, lottery.ModifyTime);
            Assert.Equal(operatorName, lottery.ModifyBy);
            Assert.Equal(operation == "auto" ? (long?)null : UserId, lottery.ModifyId);
            var outbox = Assert.Single(f.Db.Queryable<ReliableOutboxMessage>().ToList());
            Assert.Equal(ReliableTaskTypes.NotificationRequested, outbox.TaskType);
            Assert.Equal($"task:notification:lottery-won:lottery:{LotteryId}", outbox.IdempotencyKey);
            Assert.Equal("PostLottery", outbox.AggregateType);
            Assert.Equal(LotteryId.ToString(), outbox.AggregateId);
            Assert.Equal(ReliableOutboxStatuses.Pending, outbox.Status);
            Assert.Equal(0, outbox.AttemptCount);
            Assert.Equal(0, outbox.TenantId);
            // 既有边界：SQLite 读回 DrawTime 为 Unspecified，Outbox 把它按本地时区转 UTC。
            // 锁定本批未改的行为；时区修复须独立确认，不能将此断言解释为 UTC 正确性验收。
            var expectedOutboxTime = operation == "auto"
                ? DateTime.SpecifyKind(f.Cutoff, DateTimeKind.Unspecified).ToUniversalTime()
                : f.Cutoff;
            if (operation == "auto") Assert.Equal(DateTimeKind.Unspecified, lottery.DrawTime!.Value.Kind);
            Assert.Equal(expectedOutboxTime, outbox.OccurredAtUtc);
            var notification = JsonSerializer.Deserialize<NotificationRequestedTaskPayload>(outbox.PayloadJson)!.Notification;
            Assert.Equal(NotificationType.LotteryWon, notification.Type);
            Assert.Equal("抽奖开奖结果", notification.Title);
            Assert.Equal((int)NotificationPriority.High, notification.Priority);
            Assert.Equal(BusinessType.Post, notification.BusinessType);
            Assert.Equal(PostId, notification.BusinessId);
            Assert.Equal($"notification:lottery-won:lottery:{LotteryId}", notification.BusinessKey);
            Assert.Equal(new[] { WinnerB, WinnerA }, notification.ReceiverUserIds.Order().ToArray());
            Assert.Equal(operation == "auto" ? 0 : UserId, notification.TriggerId);
            Assert.Equal(operatorName, notification.TriggerName);
            Assert.Null(notification.TriggerAvatar);
            Assert.Equal(0, notification.TenantId);
            Assert.Equal(NotificationTargetKind.ForumPost, notification.TargetKind);
            Assert.Equal(PostId, notification.Target!.PostId);
            Assert.Equal("pst_" + Secret, notification.Target.PostPublicId);
            Assert.Equal(f.Cutoff, notification.OccurredAtUtc);
            Assert.Equal(fallback ? $"帖子 {PostId}" : Secret, notification.TemplateArguments!["targetTitle"]);
            Assert.Equal(fallback ? "抽奖奖品" : Secret, notification.TemplateArguments["prizeName"]);
            Assert.Equal("2", notification.TemplateArguments["winnerCount"]);
            Assert.True(f.Steps.IndexOf("winners.AddRangeAsync") < f.Steps.IndexOf("lotteries.UpdateAsync"));
            Assert.True(f.Steps.IndexOf("lotteries.UpdateAsync") < f.Steps.IndexOf("outbox.AddAsync"));
            Assert.True(f.Steps.IndexOf("outbox.after-write") < f.Steps.IndexOf("detail.GetPostDetailAsync"));
            var baseline = f.Snapshot();
            var repeat = await Assert.ThrowsAsync<BusinessException>(f.CallServiceAsync);
            Assert.Equal("Lottery.AlreadyDrawn", repeat.ErrorCode);
            await InvokeApiAsync(() => f.Controller.Draw(new DrawLotteryDto { PostId = PostId }), 409);
            foreach (var viewer in new[] { 0L, UserId })
            {
                f.CurrentUserId = viewer;
                var query = Assert.IsType<LotteryResultVo>((await InvokeApiAsync(() => f.Controller.GetByPostId(PostId), 200))!.ResponseData);
                Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(query.VoLottery));
            }
            Assert.Equal(baseline, f.Snapshot());
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertQuiet();
        }
        using var minimum = new Fixture("draw");
        minimum.Db.Updateable<PostLottery>().Where(l => l.Id == LotteryId).SetColumns(l => l.WinnerCount == 0).ExecuteCommand();
        var minimumResult = await minimum.Service.DrawAsync(PostId, UserId, Secret);
        Assert.Equal(2, minimumResult.VoParticipantCount);
        Assert.Contains(Assert.Single(minimumResult.VoWinners).VoUserId, new[] { WinnerA, WinnerB });
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Job_ShouldKeepBatchSummaryContinuationAndEmptyPoolSemantics(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var empty in new[] { false, true })
        {
            using var f = new Fixture("auto", hasOutbox: !empty);
            if (empty) f.RemoveComments();
            Assert.Equal(1, await f.Job.ExecuteAutoDrawAsync());
            var lottery = f.Db.Queryable<PostLottery>().InSingle(LotteryId);
            Assert.True(lottery.IsDrawn);
            Assert.Equal(f.Cutoff, lottery.DrawnAt);
            Assert.Equal(empty ? 0 : 2, lottery.ParticipantCount);
            Assert.Equal(empty ? 0 : 2, f.Db.Queryable<PostLotteryWinner>().Count());
            Assert.Equal(empty ? 0 : 1, f.Db.Queryable<ReliableOutboxMessage>().Count());
            capture.AssertJob(1, 0);
            capture.Clear();
            Assert.Equal(0, await f.Job.ExecuteAutoDrawAsync());
            capture.AssertQuiet();
        }
        using (var partial = new Fixture("auto") { FailedStage = "outbox.after-write" })
        {
            partial.SeedLottery(PostId + 1, LotteryId + 1, Now.UtcDateTime.AddMinutes(-1));
            Assert.Equal(1, await partial.Job.ExecuteAutoDrawAsync());
            Assert.False(partial.Db.Queryable<PostLottery>().InSingle(LotteryId).IsDrawn);
            Assert.True(partial.Db.Queryable<PostLottery>().InSingle(LotteryId + 1).IsDrawn);
            Assert.Empty(partial.Db.Queryable<PostLotteryWinner>().ToList());
            Assert.Empty(partial.Db.Queryable<ReliableOutboxMessage>().ToList());
            Assert.Equal(0, partial.Unit.TranCount);
            capture.AssertJob(1, 1, "argument");
            capture.Clear();
        }
        using (var invalid = new Fixture("auto"))
        {
            invalid.Db.Updateable<PostLottery>().Where(l => l.Id == LotteryId).SetColumns(l => l.PostId == 0).ExecuteCommand();
            Assert.Equal(0, await invalid.Job.ExecuteAutoDrawAsync());
            invalid.AssertBaseline();
            capture.AssertJob(0, 1, "argument");
            capture.Clear();
        }
        using (var missing = new Fixture("auto"))
        {
            missing.Db.Deleteable<Post>().Where(p => p.Id == PostId).ExecuteCommand();
            Assert.Equal(0, await missing.Job.ExecuteAutoDrawAsync());
            missing.AssertBaseline();
            capture.AssertJob(0, 1, "other");
            capture.Clear();
        }
        using var scan = new Fixture("auto") { FailedStage = "lotteries.QueryPageAsync", Failure = new IOException(Secret) };
        Assert.Same(scan.Failure, await Record.ExceptionAsync(() => scan.Job.ExecuteAutoDrawAsync()));
        scan.AssertBaseline();
        capture.AssertQuiet();
    }

    private sealed class UnsafeArgument() : ArgumentException(Secret)
    {
        public override string ToString() => throw new InvalidOperationException("日志不得渲染异常");
    }
    private sealed class UnreadableArgument : ArgumentException
    {
        public override string Message => throw new IOException(Secret);
    }
    private sealed class UnreadableBusiness() : BusinessException(Secret, 503)
    {
        public override string Message => throw new IOException(Secret);
    }
    private sealed class HostileFailure : Exception
    {
        public override string Message => throw new InvalidOperationException("不可读取异常原文");
        public override string ToString() => throw new InvalidOperationException("不可渲染异常");
    }
    private sealed class ThrowingInterceptor(Exception failure) : IInterceptor
    {
        public void Intercept(Castle.DynamicProxy.IInvocation invocation) => throw failure;
    }
    private sealed class FixedClock(Action tick) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() { tick(); return Now; }
    }


    private sealed class Fixture : IDisposable
    {
        private static readonly IMapper Mapper = new MapperConfiguration(c => c.AddProfile<ForumProfile>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly string _operation;
        private readonly ILoggerFactory _loggerFactory;
        private int _failureMatches;
        public SqlSugarScope Db { get; }
        public UnitOfWorkManage Unit { get; }
        public IPostLotteryService Service { get; }
        public LotteryController Controller { get; }
        public PostLotteryJob Job { get; }
        public DateTime Cutoff { get; }
        public List<string> Steps { get; } = [];
        public int OutboxWrites { get; private set; }
        public string? FailedStage { get; set; }
        public Exception Failure { get; set; } = new UnsafeArgument();
        public long CurrentUserId { get; set; } = UserId;
        public string CurrentUserName { get; set; } = Secret;
        public long RequestPostId { get; set; } = PostId;
        public Fixture(string operation, bool hasOutbox = true)
        {
            _operation = operation;
            Cutoff = operation == "auto" ? Now.UtcDateTime.AddMinutes(-10) : Now.UtcDateTime;
            new ServiceCollection().ConfigureApplication();
            _loggerFactory = LoggerFactory.Create(b => b.AddSerilog(Log.Logger, dispose: false));
            Db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", DbType = DbType.Sqlite, ConnectionString = "Data Source=:memory:",
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            Db.CodeFirst.InitTables<Post, PostLottery, PostLotteryWinner, Comment, ReliableOutboxMessage>();
            Unit = new UnitOfWorkManage(Db, NullLogger<UnitOfWorkManage>.Instance);
            SeedLottery(PostId, LotteryId, operation == "auto" ? Cutoff : Now.UtcDateTime.AddHours(1));
            SeedComments();
            var posts = Wrap<IBaseRepository<Post>>(new BaseRepository<Post>(Unit), "posts");
            var lotteries = Wrap<IBaseRepository<PostLottery>>(new BaseRepository<PostLottery>(Unit), "lotteries");
            var winners = Wrap<IBaseRepository<PostLotteryWinner>>(new BaseRepository<PostLotteryWinner>(Unit), "winners");
            var comments = Wrap<IBaseRepository<Comment>>(new BaseRepository<Comment>(Unit), "comments");
            var postTags = new Mock<IBaseRepository<PostTag>>();
            postTags.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>())).ReturnsAsync(new List<PostTag>());
            var detail = new PostService(Mapper, posts, Mock.Of<IBaseRepository<UserPostLike>>(),
                Wrap(postTags.Object, "post-tags"), Mock.Of<IBaseRepository<Category>>(), Mock.Of<IBaseRepository<Tag>>(),
                Wrap(Mock.Of<IBaseRepository<PostPoll>>(), "polls"), Mock.Of<IBaseRepository<PostPollOption>>(), Mock.Of<IBaseRepository<PostPollVote>>(),
                Wrap(Mock.Of<IBaseRepository<PostQuestion>>(), "questions"), Mock.Of<IBaseRepository<PostAnswer>>(),
                Mock.Of<ITagService>(), Mock.Of<ICoinRewardService>(), Mock.Of<INotificationService>(), Mock.Of<INotificationDedupService>(),
                Mock.Of<IExperienceService>(), Mock.Of<IBaseRepository<PostEditHistory>>(), Mock.Of<IAttachmentService>(),
                Options.Create(new ForumEditHistoryOptions()), Mock.Of<ISystemSettingProvider>(),
                postLotteryRepository: lotteries, postLotteryWinnerRepository: winners, commentRepository: comments);
            var outbox = hasOutbox ? Wrap<IReliableOutboxService>(new ReliableOutboxService(new ReliableOutboxRepository(Db)), "outbox") : null;
            var service = new PostLotteryService(Wrap<IPostService>(detail, "detail"), posts, lotteries, winners, comments,
                Mock.Of<INotificationService>(), _loggerFactory.CreateLogger<PostLotteryService>(), new FixedClock(() => Step("clock")), outbox);
            Service = new ProxyGenerator().CreateInterfaceProxyWithTarget<IPostLotteryService>(service, new TranAop(Unit));
            Job = new PostLotteryJob(lotteries, Service, _loggerFactory.CreateLogger<PostLotteryJob>());
            Controller = CreateController(Service, () => new CurrentUser { UserId = CurrentUserId, UserName = CurrentUserName, IsAuthenticated = CurrentUserId > 0 });
        }
        public void SeedLottery(long postId, long lotteryId, DateTime drawTime)
        {
            Db.Insertable(new Post(Secret, Secret)
            {
                Id = postId, PublicId = "pst_" + Secret + (postId == PostId ? "" : postId.ToString()), AuthorId = UserId, AuthorName = Secret, CategoryId = 0,
                IsPublished = true, PublishTime = Now.UtcDateTime.AddHours(-2)
            }).ExecuteCommand();
            Db.Insertable(new PostLottery { Id = lotteryId, PostId = postId, PrizeName = Secret, WinnerCount = 99, DrawTime = drawTime }).ExecuteCommand();
        }
        private void SeedComments()
        {
            var comments = new List<Comment>
            {
                new() { Id = 101, AuthorId = WinnerA, AuthorName = "  " + Secret + "  ", Content = "  " + new string('x', 510) + "  ", CreateTime = Cutoff.AddMinutes(-20) },
                new() { Id = 102, AuthorId = WinnerA, CreateTime = Cutoff.AddMinutes(-20) },
                new() { Id = 103, AuthorId = WinnerB, AuthorName = "  ", Content = "  ", CreateTime = Cutoff },
                new() { Id = 104, AuthorId = 104, CreateTime = Cutoff.AddSeconds(1) },
                new() { Id = 105, AuthorId = 0 },
                new() { Id = 106, AuthorId = UserId },
                new() { Id = 107, AuthorId = 107, ParentId = 101 },
                new() { Id = 108, AuthorId = 108, IsEnabled = false },
                new() { Id = 109, AuthorId = 109, IsDeleted = true },
                new() { Id = 110, AuthorId = 110, PostId = PostId + 99 },
                new() { Id = 111, AuthorId = WinnerA, CreateTime = Cutoff.AddMinutes(-19) }
            };
            foreach (var comment in comments)
            {
                if (comment.PostId == 0) comment.PostId = PostId;
                if (comment.Id is >= 105 and <= 110) comment.CreateTime = Cutoff.AddMinutes(-20);
            }
            // 单行参数化插入与查询使用相同日期格式，避免 SQLite 批量字面值的毫秒后缀改变等时比较。
            foreach (var comment in comments) Db.Insertable(comment).ExecuteCommand();
        }
        private T Wrap<T>(T target, string name) where T : class => new ProxyGenerator().CreateInterfaceProxyWithTarget(target, new StageInterceptor(this, name));
        private void Step(string stage)
        {
            Steps.Add(stage);
            if (stage == FailedStage && ++_failureMatches == 1) throw Failure;
        }
        private sealed class StageInterceptor(Fixture fixture, string name) : IInterceptor
        {
            public void Intercept(Castle.DynamicProxy.IInvocation invocation)
            {
                var stage = name + "." + invocation.Method.Name;
                fixture.Step(stage);
                invocation.Proceed();
                if (stage == "outbox.AddAsync") invocation.ReturnValue = AfterOutboxWriteAsync((Task<long>)invocation.ReturnValue);
            }
            private async Task<long> AfterOutboxWriteAsync(Task<long> pending)
            {
                var result = await pending;
                fixture.OutboxWrites++;
                fixture.Step("outbox.after-write");
                return result;
            }
        }
        public Task<MessageModel> CallAsync() => CallControllerAsync(Controller, _operation, RequestPostId);
        public async Task CallServiceAsync()
        {
            if (_operation == "query") await Service.GetByPostIdAsync(RequestPostId, CurrentUserId);
            else if (_operation == "auto") await Service.AutoDrawByPostIdAsync(RequestPostId);
            else await Service.DrawAsync(RequestPostId, CurrentUserId, CurrentUserName);
        }
        public void RemoveComments() => Db.Deleteable<Comment>().Where(c => c.PostId == PostId).ExecuteCommand();
        public string Snapshot() => JsonSerializer.Serialize(new
        {
            Lotteries = Db.Queryable<PostLottery>().OrderBy(l => l.Id).ToList(),
            Winners = Db.Queryable<PostLotteryWinner>().OrderBy(w => w.Id).ToList(),
            Outbox = Db.Queryable<ReliableOutboxMessage>().OrderBy(m => m.Id).ToList()
        });
        public void AssertBaseline()
        {
            Assert.Empty(Db.Queryable<PostLotteryWinner>().ToList());
            Assert.Empty(Db.Queryable<ReliableOutboxMessage>().ToList());
            var lottery = Db.Queryable<PostLottery>().InSingle(LotteryId);
            Assert.False(lottery.IsDrawn);
            Assert.Equal(0, lottery.ParticipantCount);
            Assert.Null(lottery.DrawnAt);
            Assert.Null(lottery.ModifyTime);
            Assert.Null(lottery.ModifyId);
            Assert.Equal(0, Unit.TranCount);
        }
        public void Dispose() { Db.Dispose(); _loggerFactory.Dispose(); }
    }

    private static LotteryController CreateController(IPostLotteryService service, Func<CurrentUser> current)
    {
        var accessor = new Mock<ICurrentUserAccessor>();
        accessor.SetupGet(a => a.Current).Returns(current);
        return new LotteryController(service, accessor.Object);
    }
    private static Task<MessageModel> CallControllerAsync(LotteryController controller, string operation, long postId = PostId) => operation switch
    {
        "query" => controller.GetByPostId(postId),
        "draw" => controller.Draw(new DrawLotteryDto { PostId = postId }),
        _ => throw new InvalidOperationException(operation)
    };

    private static async Task<MessageModel?> InvokeApiAsync(Func<Task<MessageModel>> action, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        MessageModel? result = null;
        app.Run(async context => { result = await action(); await ApplyResultAsync(context, result); });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Lottery/Draw";
        context.Request.QueryString = new QueryString("?query=" + Secret);
        using var output = new MemoryStream();
        context.Response.Body = output;
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
        return result;
    }

    private static Task ApplyResultAsync(HttpContext context, MessageModel result)
    {
        var actionContext = new ActionContext(context, new RouteData(), new ActionDescriptor());
        var objectResult = new ObjectResult(result);
        var filters = new List<IFilterMetadata>();
        var controller = new object();
        return new ApiErrorContractAttribute().OnResultExecutionAsync(
            new ResultExecutingContext(actionContext, filters, objectResult, controller), () =>
            {
                context.Response.StatusCode = objectResult.StatusCode!.Value;
                return Task.FromResult(new ResultExecutedContext(actionContext, filters, objectResult, controller));
            });
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        private readonly StringWriter _output = new();
        private readonly Serilog.Core.Logger _logger;
        private readonly string? _mode;
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate)
            {
                _mode = environment;
                var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RadishLogging:Mode"] = environment,
                    ["RadishLogging:Diagnostics"] = (environment == "Development").ToString()
                }).Build();
                RuntimeLoggingConfiguration.Configure(config, settings, environment, "api", _output, _output);
            }
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(_output));
            _logger = config.CreateLogger();
            Log.Logger = _logger;
        }
        public void AssertQuiet() => Assert.Equal("", _output.ToString());
        public void AssertSingle(string code, string kind, int? status = null)
        {
            var text = _output.ToString();
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(code, text);
            Assert.Contains("Error", text);
            Assert.Contains(kind, text);
            foreach (var forbidden in new[] { Secret, PostId.ToString(), UserId.ToString(), LotteryId.ToString(), WinnerA.ToString(), WinnerB.ToString(), "runtime.unclassified", "System.IO.IOException", "System.ArgumentException", "System.InvalidOperationException", "PostLotteryWinner", "CommentContentSnapshot", "ReceiverUserIds" })
                Assert.DoesNotContain(forbidden, text);
            if (_mode != null)
            {
                using var json = JsonDocument.Parse(text);
                var root = json.RootElement;
                Assert.Equal(_mode, root.GetProperty("mode").GetString());
                Assert.Equal(code, root.GetProperty("eventCode").GetString());
                var properties = root.GetProperty("properties");
                Assert.Equal(status.HasValue ? 2 : 1, properties.EnumerateObject().Count());
                Assert.Equal(kind, properties.GetProperty("failureKind").GetString());
                if (status.HasValue) Assert.Equal(status.Value, properties.GetProperty("statusCode").GetInt32());
            }
        }
        public void AssertJob(int succeeded, int failed, string? kind = null)
        {
            var text = _output.ToString();
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            var code = failed > 0 ? "job.batch.failed" : "job.batch.completed";
            var outcome = failed > 0 ? succeeded > 0 ? "partial" : "failed" : "succeeded";
            Assert.Contains(code, text);
            Assert.Contains(failed > 0 ? "Error" : "Info", text);
            Assert.Contains(outcome, text);
            foreach (var forbidden in new[] { Secret, PostId.ToString(), UserId.ToString(), LotteryId.ToString(), WinnerA.ToString(), WinnerB.ToString(), "runtime.unclassified", "System.IO.IOException", "System.ArgumentException", "PostLotteryWinner", "ReceiverUserIds" })
                Assert.DoesNotContain(forbidden, text);
            if (_mode != null)
            {
                using var json = JsonDocument.Parse(text);
                var root = json.RootElement;
                Assert.Equal(_mode, root.GetProperty("mode").GetString());
                Assert.Equal(code, root.GetProperty("eventCode").GetString());
                var properties = root.GetProperty("properties");
                Assert.Equal(failed > 0 ? 6 : 5, properties.EnumerateObject().Count());
                Assert.Equal("post-lottery", properties.GetProperty("jobKind").GetString());
                Assert.Equal(outcome, properties.GetProperty("outcome").GetString());
                Assert.Equal(succeeded, properties.GetProperty("processedCount").GetInt32());
                Assert.Equal(failed, properties.GetProperty("failedCount").GetInt32());
                Assert.True(properties.GetProperty("durationMs").GetDouble() >= 0);
                if (kind != null) Assert.Equal(kind, properties.GetProperty("failureKind").GetString());
            }
        }
        public void Clear() => _output.GetStringBuilder().Clear();
        public void Dispose() { Log.Logger = _previous; _logger.Dispose(); _output.Dispose(); }
    }

    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
