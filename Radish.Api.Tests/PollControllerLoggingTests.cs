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
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using Radish.Shared.Constants;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class PollControllerLoggingTests
{
    private const string Secret = "POLL_PRIVATE_SENTINEL";
    private const long PostId = 912345678;
    private const long UserId = 623456789;
    private const long PollId = 734567891;
    private const long OptionId = 823456789;
    private const long OtherOptionId = 823456788;
    // 详情读取使用系统 UTC；固定本轮秒级快照，避免预设日期过期后使正常投票被判已关闭。
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    private static readonly string[] Operations = ["query", "vote", "close"];
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
            new UnsafeArgument(), new PollInputValidationException(Secret, "postId"),
            new BusinessException(Secret, 400, "Private.Code", "Private.Key", Secret),
            new BusinessException(Secret, 403, "Private.Code", "Private.Key", Secret),
            new BusinessException(Secret, 404, "Private.Code", "Private.Key", Secret),
            new BusinessException(Secret, 409, "Private.Code", "Private.Key", Secret),
            new BusinessException(Secret, 500, "Private.Code", "Private.Key", Secret),
            new BusinessException(Secret, 503, "Private.Code", "Private.Key", Secret)
        })
        {
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IPostPollService>(new ThrowingInterceptor(failure));
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
            if (failure is UnsafeArgument) capture.AssertSingle("poll.request_failed", "argument");
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
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IPostPollService>(new ThrowingInterceptor(failure));
            var controller = CreateController(service, () => new CurrentUser { UserId = UserId, UserName = Secret });
            Assert.Null(await InvokeApiAsync(() => CallControllerAsync(controller, operation), 500));
            capture.AssertSingle("http.failed", kind, 500);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ExplicitIdValidationAndControllerPrechecks_ShouldStayQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in new[] { "query", "vote", "vote-option", "close" })
        foreach (var invalid in new[] { 0L, -1L })
        {
            using var f = new Fixture(operation == "vote-option" ? "vote" : operation)
            {
                RequestPostId = operation == "vote-option" ? PostId : invalid,
                RequestOptionId = operation == "vote-option" ? invalid : OptionId
            };
            var error = await Assert.ThrowsAsync<PollInputValidationException>(f.CallServiceAsync);
            var param = operation is "vote" or "vote-option" ? "request" : "postId";
            Assert.Equal(param, error.ParamName);
            Assert.Equal(new ArgumentException(operation == "vote-option" ? "投票选项ID必须大于0" : "帖子ID必须大于0", param).Message, error.Message);
            await InvokeApiAsync(f.CallAsync, 400);
            Assert.DoesNotContain(f.Steps, s => s.StartsWith("posts.", StringComparison.Ordinal));
            f.AssertBaseline();
            capture.AssertQuiet();
        }
        foreach (var operation in new[] { "vote", "close" })
        foreach (var modelInvalid in new[] { false, true })
        {
            using var f = new Fixture(operation) { CurrentUserId = 0 };
            if (modelInvalid) f.Controller.ModelState.AddModelError("request", Secret);
            await InvokeApiAsync(f.CallAsync, modelInvalid ? 400 : 401);
            Assert.Empty(f.Steps);
            var error = await Assert.ThrowsAsync<BusinessException>(f.CallServiceAsync);
            Assert.Equal(401, error.StatusCode);
            Assert.Equal("Auth.Unauthorized", error.ErrorCode);
            f.AssertBaseline();
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task NormalBusinessRejections_ShouldPreserveCodesAndRollbackLateVisibilityFailure(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        {
            var scenarios = new List<string> { "post-missing", "post-deleted", "poll-missing", "poll-deleted", "disabled", "unpublished" };
            if (operation != "query") scenarios.AddRange(["closed", "deadline", "expired"]);
            if (operation == "vote") scenarios.AddRange(["option-missing", "option-deleted", "option-other-poll", "duplicate"]);
            if (operation == "close") scenarios.Add("not-author");
            foreach (var scenario in scenarios)
            {
                using var f = new Fixture(operation);
                var status = 404;
                var code = "Forum.PostNotFound";
                var deadline = Now.UtcDateTime.AddSeconds(scenario == "expired" ? -1 : 0);
                switch (scenario)
                {
                    case "post-missing": f.Db.Deleteable<Post>().Where(p => p.Id == PostId).ExecuteCommand(); break;
                    case "post-deleted": f.Db.Updateable<Post>().Where(p => p.Id == PostId).SetColumns(p => p.IsDeleted == true).ExecuteCommand(); break;
                    case "poll-missing": f.Db.Deleteable<PostPoll>().Where(p => p.Id == PollId).ExecuteCommand(); code = "Poll.NotFound"; break;
                    case "poll-deleted": f.Db.Updateable<PostPoll>().Where(p => p.Id == PollId).SetColumns(p => p.IsDeleted == true).ExecuteCommand(); code = "Poll.NotFound"; break;
                    case "disabled": f.Db.Updateable<Post>().Where(p => p.Id == PostId).SetColumns(p => p.IsEnabled == false).ExecuteCommand(); break;
                    case "unpublished": f.Db.Updateable<Post>().Where(p => p.Id == PostId).SetColumns(p => p.IsPublished == false).ExecuteCommand(); break;
                    case "closed": f.Db.Updateable<PostPoll>().Where(p => p.Id == PollId).SetColumns(p => p.IsClosed == true).ExecuteCommand(); code = "Poll.Closed"; status = 409; break;
                    case "deadline": f.Db.Updateable<PostPoll>().Where(p => p.Id == PollId).SetColumns(p => p.EndTime == deadline).ExecuteCommand(); code = "Poll.Closed"; status = 409; break;
                    case "expired": f.Db.Updateable<PostPoll>().Where(p => p.Id == PollId).SetColumns(p => p.EndTime == deadline).ExecuteCommand(); code = "Poll.Closed"; status = 409; break;
                    case "option-missing": f.Db.Deleteable<PostPollOption>().Where(o => o.Id == OptionId).ExecuteCommand(); code = "Poll.OptionNotFound"; break;
                    case "option-deleted": f.Db.Updateable<PostPollOption>().Where(o => o.Id == OptionId).SetColumns(o => o.IsDeleted == true).ExecuteCommand(); code = "Poll.OptionNotFound"; break;
                    case "option-other-poll": f.Db.Updateable<PostPollOption>().Where(o => o.Id == OptionId).SetColumns(o => o.PollId == 99).ExecuteCommand(); code = "Poll.OptionNotFound"; break;
                    case "duplicate": f.AddExistingVote(); code = "Poll.AlreadyVoted"; status = 409; break;
                    case "not-author": f.Db.Updateable<Post>().Where(p => p.Id == PostId).SetColumns(p => p.AuthorId == 99).ExecuteCommand(); code = "Poll.CloseForbidden"; status = 403; break;
                }
                var baseline = f.Snapshot();
                var result = await InvokeApiAsync(f.CallAsync, status);
                Assert.Equal(code, result!.Code);
                Assert.Equal(code switch
                {
                    "Forum.PostNotFound" => "error.forum.post_not_found", "Poll.NotFound" => "error.poll.not_found",
                    "Poll.Closed" => "error.poll.closed", "Poll.OptionNotFound" => "error.poll.option_not_found",
                    "Poll.AlreadyVoted" => "error.poll.already_voted", _ => "error.poll.close_forbidden"
                }, result.MessageKey);
                Assert.Equal(baseline, f.Snapshot());
                if (operation != "query" && scenario is "disabled" or "unpublished") Assert.Contains("polls.UpdateAsync", f.Steps);
                Assert.Equal(0, f.Unit.TranCount);
                capture.AssertQuiet();
            }
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ConsumedDependencyFailures_ShouldLogOnceAndRollbackActualWrites(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        {
            var stages = new List<string> { "detail.GetPostDetailAsync", "posts.QueryByIdAsync", "post-tags.QueryAsync", "polls.QueryFirstAsync", "options.QueryAsync", "votes.QueryFirstAsync", "questions.QueryFirstAsync" };
            if (operation != "query") stages.AddRange(["posts.QueryFirstAsync", "clock", "polls.UpdateAsync"]);
            if (operation == "vote") stages.AddRange(["options.QueryFirstAsync", "votes.QueryExistsAsync", "votes.AddAsync", "options.UpdateAsync"]);
            foreach (var stage in stages)
            {
                using var f = new Fixture(operation) { FailedStage = stage, Failure = new UnsafeArgument() };
                var result = await InvokeApiAsync(f.CallAsync, 400);
                Assert.Equal(f.Failure.Message, result!.MessageInfo);
                Assert.Contains(stage, f.Steps);
                f.AssertBaseline();
                capture.AssertSingle("poll.request_failed", "argument");
                capture.Clear();
            }
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task LateFailuresAndUniqueConstraint_ShouldRetainApiOwnershipAndRollback(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in new[] { "vote", "close" })
        foreach (var (failure, status, kind) in new (Exception, int, string?)[]
        {
            (new IOException(Secret), 500, "io"), (new InvalidOperationException(Secret), 500, "invalid-operation"),
            (new TimeoutException(Secret), 500, "timeout"), (new OperationCanceledException(Secret), 500, "cancelled"),
            (new HostileFailure(), 500, "other"), (new UnreadableArgument(), 500, "io"),
            (new BusinessException(Secret, 503, "Private.Code", "Private.Key"), 503, "other"),
            (new BusinessException(Secret, 409, "Private.Code", "Private.Key"), 409, null),
            (new AggregateException(new UnsafeArgument()), 400, "argument")
        })
        {
            using var f = new Fixture(operation) { FailedStage = "options.QueryAsync", Failure = failure };
            var result = await InvokeApiAsync(f.CallAsync, status);
            Assert.Contains("polls.UpdateAsync", f.Steps);
            f.AssertBaseline();
            if (failure is BusinessException expected) Assert.Equal(expected.ErrorCode, result!.Code);
            if (kind == null) capture.AssertQuiet();
            else capture.AssertSingle(status == 400 ? "poll.request_failed" : "http.failed", kind, status == 400 ? null : status);
            capture.Clear();
        }
        using var direct = new Fixture("vote") { FailedStage = "options.UpdateAsync", Failure = new UnsafeArgument() };
        var caught = await Assert.ThrowsAsync<UnsafeArgument>(direct.CallServiceAsync);
        Assert.Same(direct.Failure, caught);
        direct.AssertBaseline();
        capture.AssertQuiet();

        using var conflict = new Fixture("vote") { HideExistingVote = true };
        conflict.AddExistingVote();
        var baseline = conflict.Snapshot();
        await InvokeApiAsync(conflict.CallAsync, 500);
        Assert.Equal(baseline, conflict.Snapshot());
        Assert.DoesNotContain("options.UpdateAsync", conflict.Steps);
        Assert.Equal(0, conflict.Unit.TranCount);
        capture.AssertSingle("http.failed", "database", 500);
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SuccessfulVoteCloseAndQuery_ShouldKeepCountsAuditOrderingAndRepeatRejection(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in new[] { "vote", "close" })
        foreach (var name in new[] { " " + Secret + " ", " " })
        {
            using var f = new Fixture(operation) { CurrentUserName = name };
            var response = await InvokeApiAsync(f.CallAsync, 200);
            var poll = Assert.IsType<PostPollVo>(response!.ResponseData);
            var stored = f.Db.Queryable<PostPoll>().InSingle(PollId);
            var selected = f.Db.Queryable<PostPollOption>().InSingle(OptionId);
            var safeName = string.IsNullOrWhiteSpace(name) ? $"User-{UserId}" : Secret;
            Assert.Equal(PollId, poll.VoPollId);
            Assert.Equal(PostId, poll.VoPostId);
            Assert.Equal(Secret, poll.VoQuestion);
            Assert.Equal(new[] { OtherOptionId, OptionId }, poll.VoOptions.Select(o => o.VoOptionId));
            Assert.Equal(operation == "vote" ? 4 : 3, poll.VoTotalVoteCount);
            Assert.Equal(poll.VoTotalVoteCount, stored.TotalVoteCount);
            Assert.Equal(operation == "close", stored.IsClosed);
            Assert.Equal(stored.IsClosed, poll.VoIsClosed);
            Assert.Equal(safeName, stored.ModifyBy);
            Assert.Equal(UserId, stored.ModifyId);
            Assert.Equal(Now.UtcDateTime, stored.ModifyTime);
            if (operation == "vote")
            {
                Assert.True(poll.VoHasVoted);
                Assert.Equal(OptionId, poll.VoSelectedOptionId);
                Assert.Equal(3, selected.VoteCount);
                Assert.Equal(75m, poll.VoOptions[1].VoVotePercent);
                Assert.Equal(safeName, selected.ModifyBy);
                Assert.Equal(UserId, selected.ModifyId);
                Assert.Equal(Now.UtcDateTime, selected.ModifyTime);
                var vote = f.Db.Queryable<PostPollVote>().Single();
                Assert.Equal(UserId, vote.UserId);
                Assert.Equal(0, vote.TenantId);
                Assert.Equal(OptionId, vote.OptionId);
                Assert.Equal(PostId, vote.PostId);
                Assert.Equal(PollId, vote.PollId);
                Assert.Equal(safeName, vote.CreateBy);
                Assert.Equal(safeName, vote.UserName);
                Assert.Equal(UserId, vote.CreateId);
                Assert.Equal(Now.UtcDateTime, vote.CreateTime);
                Assert.Equal(DateTimeKind.Utc, f.InsertedVote!.CreateTime.Kind);
                Assert.True(f.Steps.IndexOf("votes.AddAsync") < f.Steps.IndexOf("options.UpdateAsync"));
                Assert.True(f.Steps.IndexOf("options.UpdateAsync") < f.Steps.IndexOf("polls.UpdateAsync"));
            }
            else
            {
                Assert.False(poll.VoHasVoted);
                Assert.Null(poll.VoSelectedOptionId);
                Assert.Empty(f.Db.Queryable<PostPollVote>().ToList());
                Assert.Equal(66.67m, poll.VoOptions[1].VoVotePercent);
            }
            var baseline = f.Snapshot();
            var repeated = await InvokeApiAsync(f.CallAsync, 409);
            Assert.Equal(operation == "vote" ? "Poll.AlreadyVoted" : "Poll.Closed", repeated!.Code);
            Assert.Equal(baseline, f.Snapshot());
            f.Steps.Clear();
            f.CurrentUserId = 0;
            var anonymous = await InvokeApiAsync(() => f.Controller.GetByPostId(PostId), 200);
            var anonymousPoll = Assert.IsType<PollVoteResultVo>(anonymous!.ResponseData).VoPoll!;
            Assert.False(anonymousPoll.VoHasVoted);
            Assert.Null(anonymousPoll.VoSelectedOptionId);
            Assert.DoesNotContain("votes.QueryFirstAsync", f.Steps);
            Assert.Equal(baseline, f.Snapshot());
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertQuiet();
        }
        using var fallback = new Fixture("query") { CurrentUserId = 0 };
        fallback.Db.Updateable<PostPoll>().Where(p => p.Id == PollId).SetColumns(p => p.TotalVoteCount == 0).ExecuteCommand();
        var query = await InvokeApiAsync(fallback.CallAsync, 200);
        Assert.Equal(3, Assert.IsType<PollVoteResultVo>(query!.ResponseData).VoPoll!.VoTotalVoteCount);
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
        public SqlSugarScope Db { get; }
        public UnitOfWorkManage Unit { get; }
        public IPostPollService Service { get; }
        public PollController Controller { get; }
        public List<string> Steps { get; } = [];
        public string? FailedStage { get; set; }
        public Exception Failure { get; set; } = new UnsafeArgument();
        public long CurrentUserId { get; set; } = UserId;
        public string CurrentUserName { get; set; } = Secret;
        public long RequestPostId { get; set; } = PostId;
        public long RequestOptionId { get; set; } = OptionId;
        public bool HideExistingVote { get; set; }
        public PostPollVote? InsertedVote { get; private set; }
        public Fixture(string operation)
        {
            _operation = operation;
            new ServiceCollection().ConfigureApplication();
            Db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", DbType = DbType.Sqlite, ConnectionString = "Data Source=:memory:",
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            Db.CodeFirst.InitTables<Post, PostPoll, PostPollOption, PostPollVote>();
            Unit = new UnitOfWorkManage(Db, NullLogger<UnitOfWorkManage>.Instance);
            Db.Insertable(new Post(Secret, Secret)
            {
                Id = PostId, PublicId = "pst_" + Secret, AuthorId = UserId, AuthorName = Secret, CategoryId = 0,
                IsPublished = true, PublishTime = Now.UtcDateTime
            }).ExecuteCommand();
            Db.Insertable(new PostPoll
            {
                Id = PollId, PostId = PostId, Question = Secret, TotalVoteCount = 3, EndTime = Now.UtcDateTime.AddHours(1)
            }).ExecuteCommand();
            Db.Insertable(new List<PostPollOption>
            {
                new() { Id = OptionId, PollId = PollId, OptionText = Secret, VoteCount = 2, SortOrder = 2 },
                new() { Id = OtherOptionId, PollId = PollId, OptionText = Secret, VoteCount = 1, SortOrder = 1 }
            }).ExecuteCommand();
            var posts = Wrap<IBaseRepository<Post>>(new BaseRepository<Post>(Unit), "posts");
            var polls = Wrap<IBaseRepository<PostPoll>>(new BaseRepository<PostPoll>(Unit), "polls");
            var options = Wrap<IBaseRepository<PostPollOption>>(new BaseRepository<PostPollOption>(Unit), "options");
            var votes = Wrap<IBaseRepository<PostPollVote>>(new BaseRepository<PostPollVote>(Unit), "votes");
            var postTags = new Mock<IBaseRepository<PostTag>>();
            postTags.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>())).ReturnsAsync(new List<PostTag>());
            var detail = new PostService(Mapper, posts, Mock.Of<IBaseRepository<UserPostLike>>(),
                Wrap(postTags.Object, "post-tags"), Mock.Of<IBaseRepository<Category>>(), Mock.Of<IBaseRepository<Tag>>(),
                polls, options, votes, Wrap(Mock.Of<IBaseRepository<PostQuestion>>(), "questions"), Mock.Of<IBaseRepository<PostAnswer>>(),
                Mock.Of<ITagService>(), Mock.Of<ICoinRewardService>(), Mock.Of<INotificationService>(), Mock.Of<INotificationDedupService>(),
                Mock.Of<IExperienceService>(), Mock.Of<IBaseRepository<PostEditHistory>>(), Mock.Of<IAttachmentService>(),
                Options.Create(new ForumEditHistoryOptions()), Mock.Of<ISystemSettingProvider>());
            var service = new PostPollService(Wrap<IPostService>(detail, "detail"), posts, polls, options, votes, new FixedClock(() => Step("clock")));
            Service = new ProxyGenerator().CreateInterfaceProxyWithTarget<IPostPollService>(service, new TranAop(Unit));
            Controller = CreateController(Service, () => new CurrentUser { UserId = CurrentUserId, UserName = CurrentUserName, IsAuthenticated = CurrentUserId > 0 });
        }
        private T Wrap<T>(T target, string name) where T : class => new ProxyGenerator().CreateInterfaceProxyWithTarget(target, new StageInterceptor(this, name));
        private void Step(string stage)
        {
            Steps.Add(stage);
            if (stage == FailedStage) throw Failure;
        }
        private sealed class StageInterceptor(Fixture fixture, string name) : IInterceptor
        {
            public void Intercept(Castle.DynamicProxy.IInvocation invocation)
            {
                var stage = name + "." + invocation.Method.Name;
                fixture.Step(stage);
                if (stage == "votes.QueryExistsAsync" && fixture.HideExistingVote)
                {
                    invocation.ReturnValue = Task.FromResult(false);
                    return;
                }
                if (stage == "votes.AddAsync") fixture.InsertedVote = (PostPollVote)invocation.Arguments[0];
                invocation.Proceed();
            }
        }
        public Task<MessageModel> CallAsync() => CallControllerAsync(Controller, _operation, RequestPostId, RequestOptionId);
        public async Task CallServiceAsync()
        {
            if (_operation == "query") await Service.GetByPostIdAsync(RequestPostId, CurrentUserId);
            else if (_operation == "vote") await Service.VoteAsync(CurrentUserId, CurrentUserName, new VotePollDto { PostId = RequestPostId, OptionId = RequestOptionId });
            else await Service.CloseAsync(RequestPostId, CurrentUserId, CurrentUserName);
        }
        public void AddExistingVote() => Db.Insertable(new PostPollVote
        {
            Id = 501, PollId = PollId, PostId = PostId, OptionId = OptionId, UserId = UserId, UserName = Secret
        }).ExecuteCommand();
        public string Snapshot() => JsonSerializer.Serialize(new
        {
            Polls = Db.Queryable<PostPoll>().OrderBy(p => p.Id).ToList(),
            Options = Db.Queryable<PostPollOption>().OrderBy(o => o.Id).ToList(),
            Votes = Db.Queryable<PostPollVote>().OrderBy(v => v.Id).ToList()
        });
        public void AssertBaseline()
        {
            Assert.Empty(Db.Queryable<PostPollVote>().ToList());
            var poll = Db.Queryable<PostPoll>().InSingle(PollId);
            Assert.False(poll.IsClosed);
            Assert.Equal(3, poll.TotalVoteCount);
            Assert.Null(poll.ModifyTime);
            Assert.Null(poll.ModifyId);
            var option = Db.Queryable<PostPollOption>().InSingle(OptionId);
            Assert.Equal(2, option.VoteCount);
            Assert.Null(option.ModifyTime);
            Assert.Null(option.ModifyId);
            Assert.Equal(0, Unit.TranCount);
        }
        public void Dispose() => Db.Dispose();
    }

    private static PollController CreateController(IPostPollService service, Func<CurrentUser> current)
    {
        var accessor = new Mock<ICurrentUserAccessor>();
        accessor.SetupGet(a => a.Current).Returns(current);
        return new PollController(service, accessor.Object);
    }
    private static Task<MessageModel> CallControllerAsync(PollController controller, string operation, long postId = PostId, long optionId = OptionId) => operation switch
    {
        "query" => controller.GetByPostId(postId),
        "vote" => controller.Vote(new VotePollDto { PostId = postId, OptionId = optionId }),
        "close" => controller.Close(new ClosePollDto { PostId = postId }),
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
        context.Request.Path = "/api/v1/Poll/Vote";
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
            foreach (var forbidden in new[] { Secret, PostId.ToString(), UserId.ToString(), PollId.ToString(), OptionId.ToString(), "runtime.unclassified", "System.IO.IOException", "System.ArgumentException", "System.InvalidOperationException", "PostPollVote", "OptionText", "VoteCount" })
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
        public void Clear() => _output.GetStringBuilder().Clear();
        public void Dispose() { Log.Logger = _previous; _logger.Dispose(); _output.Dispose(); }
    }

    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
