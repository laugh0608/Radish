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
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Filters;
using Radish.Common.CoreTool;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.LogTool;
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
using Serilog;
using Serilog.Events;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class ReactionControllerLoggingTests
{
    private const string Secret = "REACTION_PRIVATE_SENTINEL";
    private const string StickerValue = "private_group/private_sticker";
    private const long TargetId = 912345678;
    private const long UserId = 623456789;
    private const long AttachmentId = 823456789;
    private static readonly string[] Operations = ["query", "batch", "toggle"];
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ControllerConsumers_ShouldKeepResponsesAndSingleFailureOwnership(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var fromCurrent in new[] { false, true })
        foreach (var status in new[] { 400, 401, 403, 404, 409, 429, 500, 502, 503 })
        {
            var failure = new UnsafeBusiness(status);
            failure.Data[Secret] = Secret;
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IReactionService>(new ThrowingInterceptor(failure));
            var controller = CreateController(service, fromCurrent ? () => throw failure : null);
            var response = await InvokeApiAsync(() => CallControllerAsync(controller, operation), status, environment);
            Assert.False(response!.IsSuccess);
            Assert.Equal(Secret, response.MessageInfo);
            Assert.Equal("Private.Code", response.Code);
            // Reaction 原先未复制消息键与格式参数，日志治理不扩充这些响应字段。
            Assert.Null(response.MessageKey);
            Assert.Null(response.MessageArguments);
            Assert.Null(response.ResponseData);
            if (status >= 500) capture.AssertEvents(("http.failed", "Error", "other", status));
            else capture.AssertQuiet();
            capture.Clear();
        }
        foreach (var operation in Operations)
        foreach (var (failure, kind) in new (Exception, string)[]
        {
            (new IOException(Secret), "io"), (new ArgumentException(Secret), "argument"),
            (new InvalidOperationException(Secret), "invalid-operation"), (new TimeoutException(Secret), "timeout"),
            (new OperationCanceledException(Secret), "cancelled"), (new HostileFailure(), "other"),
            (new UnreadableBusiness(), "io"), (new AggregateException(new IOException(Secret)), "aggregate")
        })
        {
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IReactionService>(new ThrowingInterceptor(failure));
            var controller = CreateController(service);
            Assert.Null(await InvokeApiAsync(() => CallControllerAsync(controller, operation), 500, environment));
            capture.AssertEvents(("http.failed", "Error", kind, 500));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ValidationAndTargetRejections_ShouldRemainQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var (operation, configure, status, code) in new (string, Action<Fixture>, int, string)[]
        {
            ("query", f => f.Dto.TargetType = "invalid", 400, "InvalidArgument"),
            ("query", f => f.Dto.TargetId = 0, 400, "InvalidArgument"),
            ("batch", f => f.Dto.TargetType = "invalid", 400, "InvalidArgument"),
            ("batch", f => f.TargetIds = [], 400, "InvalidArgument"),
            ("batch", f => f.TargetIds = [0, -1], 400, "InvalidArgument"),
            ("batch", f => f.TargetIds = Enumerable.Range(1, 101).Select(x => (long)x).ToList(), 400, "BatchSizeExceeded"),
            ("toggle", f => f.CurrentUserId = 0, 401, "AuthRequired"),
            ("toggle", f => f.Dto.TargetType = "invalid", 400, "InvalidArgument"),
            ("toggle", f => f.Dto.TargetId = -1, 400, "InvalidArgument"),
            ("toggle", f => f.Dto.EmojiType = "invalid", 400, "InvalidArgument"),
            ("toggle", f => f.Dto.EmojiValue = " ", 400, "InvalidArgument"),
            ("toggle", f => { f.Dto.EmojiType = "sticker"; f.Dto.EmojiValue = "invalid"; }, 400, "InvalidArgument"),
            ("toggle", f => f.Dto.TargetId = TargetId + 1, 404, "TargetNotFound"),
            ("toggle", f => f.Db.Updateable<Post>().Where(x => x.Id == TargetId).SetColumns(p => p.IsPublished == false).ExecuteCommand(), 404, "TargetNotFound"),
            ("toggle", f => { f.Dto.TargetType = "Comment"; f.Db.Updateable<Comment>().Where(x => x.Id == TargetId).SetColumns(c => c.IsEnabled == false).ExecuteCommand(); }, 404, "TargetNotFound"),
            ("toggle", f => { f.UseSticker(); f.Db.Updateable<StickerGroup>().Where(x => x.Id == 1).SetColumns(g => g.IsEnabled == false).ExecuteCommand(); }, 404, "StickerNotAvailable"),
            ("toggle", f => { f.UseSticker(); f.Db.Updateable<Sticker>().Where(x => x.Id == 1).SetColumns(s => s.IsDeleted == true).ExecuteCommand(); }, 404, "StickerNotAvailable"),
            ("toggle", f => { for (var i = 0; i < 10; i++) f.InsertReaction(i + 1, "limit-" + i); }, 400, "ReactionLimitExceeded")
        })
        {
            using var f = new Fixture(operation);
            configure(f);
            var before = f.Snapshot();
            var result = await InvokeApiAsync(f.CallAsync, status, environment);
            Assert.Equal(code, result!.Code);
            Assert.Equal(before, f.Snapshot());
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertQuiet();
        }
        foreach (var operation in new[] { "batch", "toggle" })
        {
            var service = new Mock<IReactionService>(MockBehavior.Strict);
            var controller = CreateController(service.Object, () => throw new IOException(Secret));
            controller.ModelState.AddModelError("private", Secret);
            var result = await InvokeApiAsync(() => CallControllerAsync(controller, operation), 400, environment);
            Assert.Equal("InvalidArgument", result!.Code);
            Assert.Equal("请求参数验证失败", result.MessageInfo);
            service.VerifyNoOtherCalls();
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Queries_ShouldPreserveGroupingFilteringAndEmptyTargets(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture("query");
        f.InsertReaction(1, Secret);
        f.InsertReaction(2, Secret, user: UserId + 1);
        f.InsertReaction(3, "zzz");
        f.InsertReaction(4, "hidden", deleted: true);
        f.InsertReaction(5, StickerValue, emojiType: "sticker", attachment: AttachmentId);
        f.Dto.TargetType = " POST ";
        var before = f.Snapshot();
        var result = await InvokeApiAsync(f.CallAsync, 200, environment);
        var summary = Assert.IsType<List<ReactionSummaryVo>>(result!.ResponseData);
        Assert.Equal(new[] { Secret, StickerValue, "zzz" }, summary.Select(s => s.VoEmojiValue));
        Assert.Equal(new[] { 2, 1, 1 }, summary.Select(s => s.VoCount));
        Assert.All(summary, s => Assert.True(s.VoIsReacted));
        Assert.Equal("https://private.invalid/" + Secret, summary[1].VoThumbnailUrl);
        Assert.Equal(new[] { "reactions.QueryAsync", "thumbnail" }, f.Steps);
        f.CurrentUserId = 0;
        f.Steps.Clear();
        var batch = await InvokeApiAsync(() => f.Controller.BatchGetSummary(new BatchGetReactionSummaryDto
        {
            TargetType = "post", TargetIds = [TargetId, -1, TargetId, 0, TargetId + 1]
        }), 200, environment);
        var groups = Assert.IsType<Dictionary<string, List<ReactionSummaryVo>>>(batch!.ResponseData);
        Assert.Equal(2, groups.Count);
        Assert.Empty(groups[(TargetId + 1).ToString()]);
        Assert.All(groups[TargetId.ToString()], s => Assert.False(s.VoIsReacted));
        Assert.Equal(before, f.Snapshot());
        // 汇总原本不验证目标可见性，禁用目标仍按 Reaction 数据返回。
        f.Db.Updateable<Post>().Where(x => x.Id == TargetId).SetColumns(p => p.IsEnabled == false).ExecuteCommand();
        Assert.Equal(3, Assert.IsType<List<ReactionSummaryVo>>((await f.CallAsync()).ResponseData).Count);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Toggle_ShouldCommitCreateAndCancelAndExposeExistingRestoreBoundary(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var target in new[] { "Post", "Comment" })
        foreach (var sticker in new[] { false, true })
        {
            using var f = new Fixture("toggle");
            f.Dto.TargetType = " " + target.ToUpperInvariant() + " ";
            f.CurrentUserName = sticker ? "  " : " " + Secret + " ";
            if (sticker) f.UseSticker();
            var started = DateTime.UtcNow.AddSeconds(-1);
            var added = await InvokeApiAsync(f.CallAsync, 200, environment);
            var summary = Assert.Single(Assert.IsType<List<ReactionSummaryVo>>(added!.ResponseData));
            Assert.True(summary.VoIsReacted);
            Assert.Equal(1, summary.VoCount);
            var row = f.Db.Queryable<Reaction>().Single();
            Assert.Equal(target, row.TargetType);
            Assert.Equal(UserId, row.UserId);
            Assert.Equal(sticker ? StickerValue : Secret, row.EmojiValue);
            Assert.Equal(sticker ? "sticker" : "unicode", row.EmojiType);
            Assert.Equal(sticker ? AttachmentId : (long?)null, row.StickerAttachmentId);
            Assert.Equal(sticker ? "System" : Secret, row.UserName);
            Assert.Equal(row.UserName, row.CreateBy);
            Assert.Equal(UserId, row.CreateId);
            Assert.InRange(row.CreateTime, started, DateTime.UtcNow.AddSeconds(1));
            Assert.Equal(DateTimeKind.Utc, f.Inserted!.CreateTime.Kind);
            Assert.Equal(0, row.TenantId);
            Assert.False(row.IsDeleted);
            var cancelled = await InvokeApiAsync(f.CallAsync, 200, environment);
            Assert.Empty(Assert.IsType<List<ReactionSummaryVo>>(cancelled!.ResponseData));
            var deleted = f.Db.Queryable<Reaction>().Single();
            Assert.True(deleted.IsDeleted);
            Assert.Equal(row.Id, deleted.Id);
            Assert.Equal(row.CreateTime, deleted.CreateTime);
            Assert.Equal(row.UserName, deleted.DeletedBy);
            Assert.Equal(UserId, deleted.ModifyId);
            Assert.InRange(deleted.DeletedAt!.Value, started, DateTime.UtcNow.AddSeconds(1));
            Assert.InRange(deleted.ModifyTime!.Value, started, DateTime.UtcNow.AddSeconds(1));
            capture.AssertQuiet();
            var before = f.Snapshot();
            // 真实 BaseRepository 默认软删除过滤使恢复查询不可见，插入两次冲突后返回 409。
            var restore = await InvokeApiAsync(f.CallAsync, 409, environment);
            Assert.Equal("ConcurrentConflict", restore!.Code);
            Assert.Equal(before, f.Snapshot());
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertEvents(("reaction.retrying", "Warning", "database", null));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task StageFailures_ShouldKeepRollbackAndFinalConsumer(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var stage in operation == "toggle"
            ? new[] { "posts.QueryFirstAsync", "groups.QueryFirstAsync", "stickers.QueryFirstAsync", "reactions.QueryFirstAsync", "reactions.QueryCountAsync", "reactions.AddAsync", "inserted", "reactions.QueryAsync", "thumbnail" }
            : new[] { "reactions.QueryAsync", "thumbnail" })
        foreach (var failure in new Exception[] { new IOException(Secret), new BusinessException(Secret, 503, "Private.Code") })
        {
            using var f = new Fixture(operation) { FailedStage = stage, Failure = failure };
            f.UseSticker();
            if (operation != "toggle") f.InsertReaction(1, StickerValue, emojiType: "sticker", attachment: AttachmentId);
            var before = f.Snapshot();
            var status = failure is BusinessException ? 503 : 500;
            var response = await InvokeApiAsync(f.CallAsync, status, environment);
            if (failure is BusinessException) Assert.Equal("Private.Code", response!.Code);
            else Assert.Null(response);
            Assert.Equal(before, f.Snapshot());
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertEvents(("http.failed", "Error", failure is BusinessException ? "other" : "io", status));
            capture.Clear();
        }
        foreach (var stage in new[] { "reactions.UpdateColumnsAsync", "updated", "reactions.QueryAsync" })
        {
            using var f = new Fixture("toggle") { FailedStage = stage };
            f.InsertReaction(1, Secret);
            var before = f.Snapshot();
            await InvokeApiAsync(f.CallAsync, 500, environment);
            Assert.Equal(before, f.Snapshot());
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertEvents(("http.failed", "Error", "io", 500));
            capture.Clear();
        }
        using var rejectedRefresh = new Fixture("toggle")
        {
            FailedStage = "reactions.QueryAsync",
            Failure = new BusinessException(Secret, 409, "Private.Code")
        };
        var rejected = await InvokeApiAsync(rejectedRefresh.CallAsync, 409, environment);
        Assert.Equal("Private.Code", rejected!.Code);
        Assert.Empty(rejectedRefresh.Db.Queryable<Reaction>().ToList());
        Assert.Contains("inserted", rejectedRefresh.Steps);
        Assert.Equal(0, rejectedRefresh.Unit.TranCount);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ConflictRetry_ShouldKeepOneRetryAndSeparateFinalFailureOwnership(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var hideQueries in new[] { 2, 4 })
        {
            using var f = new Fixture("toggle") { HideFirstQueries = hideQueries };
            f.InsertReaction(1, Secret);
            var before = f.Snapshot();
            var result = await InvokeApiAsync(f.CallAsync, hideQueries == 2 ? 200 : 409, environment);
            if (hideQueries == 2)
            {
                Assert.Empty(Assert.IsType<List<ReactionSummaryVo>>(result!.ResponseData));
                Assert.True(f.Db.Queryable<Reaction>().Single().IsDeleted);
            }
            else
            {
                Assert.Equal("ConcurrentConflict", result!.Code);
                Assert.Equal(before, f.Snapshot());
            }
            Assert.Equal(hideQueries == 2 ? 1 : 2, f.Steps.Count(s => s == "reactions.AddAsync"));
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertEvents(("reaction.retrying", "Warning", "database", null));
            capture.Clear();
        }
        foreach (var outcome in new[] { "success", "second-conflict", "io", "business", "refresh" })
        {
            using var f = new Fixture("toggle");
            var first = new CountingConflict();
            f.AddFailures.Enqueue(first);
            if (outcome == "second-conflict") f.AddFailures.Enqueue(new CountingConflict());
            if (outcome == "io") f.AddFailures.Enqueue(new IOException(Secret));
            if (outcome == "business") f.AddFailures.Enqueue(new BusinessException(Secret, 503, "Private.Code"));
            if (outcome == "refresh") f.FailedStage = "reactions.QueryAsync";
            var status = outcome switch { "success" => 200, "second-conflict" => 409, "business" => 503, _ => 500 };
            var result = await InvokeApiAsync(f.CallAsync, status, environment);
            Assert.Equal(1, first.RenderCount); // 原分类器必须读一次；日志不得再渲染异常。
            Assert.Equal(2, f.Steps.Count(s => s == "reactions.AddAsync"));
            Assert.Equal(outcome == "success" ? 1 : 0, f.Db.Queryable<Reaction>().Count());
            Assert.Equal(0, f.Unit.TranCount);
            if (status >= 500) capture.AssertEvents(
                ("reaction.retrying", "Warning", "other", null),
                ("http.failed", "Error", outcome == "business" ? "other" : "io", status));
            else capture.AssertEvents(("reaction.retrying", "Warning", "other", null));
            if (status == 409) Assert.Equal("ConcurrentConflict", result!.Code);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task RestoreBranch_WhenRepositorySuppliesDeletedRow_ShouldKeepWritesAndRollback(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new string?[] { null, "updated", "reactions.QueryAsync" })
        {
            // 仅单独验证既有 Service 恢复分支；显式代理返回被通用仓储过滤的已删除行。
            using var f = new Fixture("toggle") { SupplyDeletedRow = true, FailedStage = stage };
            f.UseSticker();
            f.InsertReaction(1, StickerValue, deleted: true);
            var before = f.Snapshot();
            await InvokeApiAsync(f.CallAsync, stage == null ? 200 : 500, environment);
            if (stage == null)
            {
                var row = f.Db.Queryable<Reaction>().Single();
                Assert.False(row.IsDeleted);
                Assert.Null(row.DeletedAt);
                Assert.Null(row.DeletedBy);
                Assert.Equal("sticker", row.EmojiType);
                Assert.Equal(AttachmentId, row.StickerAttachmentId);
                Assert.Equal(UserId, row.ModifyId);
                Assert.Equal(Secret, row.UserName);
                Assert.Equal(Secret, row.ModifyBy);
                Assert.Equal(42, row.CreateId);
                capture.AssertQuiet();
            }
            else
            {
                Assert.Equal(before, f.Snapshot());
                capture.AssertEvents(("http.failed", "Error", "io", 500));
                capture.Clear();
            }
            Assert.Equal(0, f.Unit.TranCount);
        }
    }

    private sealed class UnsafeBusiness(int status) : BusinessException(Secret, new IOException(Secret), status, "Private.Code", "Private.Key", Secret)
    {
        public override string ToString() => throw new InvalidOperationException("日志不得渲染异常");
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
    private sealed class CountingConflict : Exception
    {
        public int RenderCount { get; private set; }
        public override string ToString()
        {
            if (++RenderCount > 1) throw new InvalidOperationException("不得重复渲染冲突");
            return "UNIQUE constraint failed " + Secret;
        }
    }
    private sealed class ThrowingInterceptor(Exception failure) : IInterceptor
    {
        public void Intercept(Castle.DynamicProxy.IInvocation invocation) => throw failure;
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly IMapper Mapper = new MapperConfiguration(c => c.AddProfile<ForumProfile>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly string _operation;
        private readonly ILoggerFactory _logs;
        private int _firstQueries;
        public SqlSugarScope Db { get; }
        public UnitOfWorkManage Unit { get; }
        public ReactionController Controller { get; }
        public List<string> Steps { get; } = [];
        public string? FailedStage { get; set; }
        public Exception Failure { get; set; } = new IOException(Secret);
        public Queue<Exception> AddFailures { get; } = new();
        public int HideFirstQueries { get; set; }
        public bool SupplyDeletedRow { get; set; }
        public long CurrentUserId { get; set; } = UserId;
        public string CurrentUserName { get; set; } = Secret;
        public Reaction? Inserted { get; private set; }
        public ToggleReactionDto Dto { get; } = new() { TargetType = "Post", TargetId = TargetId, EmojiType = " UNICODE ", EmojiValue = " " + Secret + " " };
        public List<long> TargetIds { get; set; } = [TargetId];
        public Fixture(string operation)
        {
            _operation = operation;
            new ServiceCollection().ConfigureApplication();
            Db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", DbType = DbType.Sqlite, ConnectionString = "Data Source=:memory:",
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            Db.CodeFirst.InitTables<Reaction, Post, Comment, StickerGroup, Sticker>();
            Unit = new UnitOfWorkManage(Db, NullLogger<UnitOfWorkManage>.Instance);
            Db.Insertable(new Post(Secret, Secret) { Id = TargetId, IsEnabled = true, IsPublished = true, PublishTime = DateTime.UtcNow }).ExecuteCommand();
            Db.Insertable(new Comment { Id = TargetId, PostId = TargetId, Content = Secret, IsEnabled = true }).ExecuteCommand();
            Db.Insertable(new StickerGroup { Id = 1, Code = "private_group", Name = Secret }).ExecuteCommand();
            Db.Insertable(new Sticker { Id = 1, GroupId = 1, Code = "private_sticker", Name = Secret, AttachmentId = AttachmentId }).ExecuteCommand();
            _logs = LoggerFactory.Create(b => b.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddSerilog(Log.Logger, dispose: false));
            var urls = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
            urls.Setup(u => u.ResolveAttachmentUrl(AttachmentId, AttachmentUrlVariant.Thumbnail)).Returns(() =>
            {
                Step("thumbnail");
                return "https://private.invalid/" + Secret;
            });
            var service = new ReactionService(Mapper, Repository<Reaction>("reactions"), Repository<Post>("posts"),
                Repository<Comment>("comments"), Repository<StickerGroup>("groups"), Repository<Sticker>("stickers"),
                _logs.CreateLogger<ReactionService>(), urls.Object);
            var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<IReactionService>(service, new TranAop(Unit));
            Controller = CreateController(proxy, () => new CurrentUser { UserId = CurrentUserId, UserName = CurrentUserName, IsAuthenticated = CurrentUserId > 0 });
        }
        private IBaseRepository<T> Repository<T>(string name) where T : class, new() =>
            new ProxyGenerator().CreateInterfaceProxyWithTarget<IBaseRepository<T>>(new BaseRepository<T>(Unit), new RepositoryInterceptor(this, name));
        private void Step(string stage)
        {
            Steps.Add(stage);
            if (stage == FailedStage) throw Failure;
        }
        private sealed class RepositoryInterceptor(Fixture f, string name) : IInterceptor
        {
            public void Intercept(Castle.DynamicProxy.IInvocation invocation)
            {
                var stage = name + "." + invocation.Method.Name;
                f.Step(stage);
                if (stage == "reactions.QueryFirstAsync")
                {
                    if (++f._firstQueries <= f.HideFirstQueries)
                    {
                        invocation.ReturnValue = Task.FromResult<Reaction?>(null);
                        return;
                    }
                    if (f.SupplyDeletedRow)
                    {
                        var predicate = (Expression<Func<Reaction, bool>>)invocation.Arguments[0];
                        invocation.ReturnValue = Task.FromResult<Reaction?>(f.Db.Queryable<Reaction>().Where(predicate).First());
                        return;
                    }
                }
                if (stage == "reactions.AddAsync")
                {
                    if (f.AddFailures.TryDequeue(out var failure)) throw failure;
                    f.Inserted = (Reaction)invocation.Arguments[0];
                }
                invocation.Proceed();
                if (stage == "reactions.AddAsync") invocation.ReturnValue = f.AfterAsync((Task<long>)invocation.ReturnValue, "inserted");
                if (stage == "reactions.UpdateColumnsAsync") invocation.ReturnValue = f.AfterAsync((Task<int>)invocation.ReturnValue, "updated");
            }
        }
        private async Task<T> AfterAsync<T>(Task<T> task, string stage)
        {
            var value = await task;
            Step(stage);
            return value;
        }
        public void UseSticker() { Dto.EmojiType = " STICKER "; Dto.EmojiValue = " / PRIVATE_GROUP / PRIVATE_STICKER / "; }
        public void InsertReaction(long id, string value, long user = UserId, bool deleted = false, string emojiType = "unicode", long? attachment = null) =>
            Db.Insertable(new Reaction
            {
                Id = id, UserId = user, UserName = Secret, TargetId = TargetId, TargetType = "Post", EmojiType = emojiType,
                EmojiValue = value, StickerAttachmentId = attachment, IsDeleted = deleted, CreateId = 42,
                DeletedAt = deleted ? DateTime.UtcNow.AddDays(-1) : null, DeletedBy = deleted ? Secret : null
            }).ExecuteCommand();
        public Task<MessageModel> CallAsync() => _operation switch
        {
            "query" => Controller.GetSummary(Dto.TargetType, Dto.TargetId),
            "batch" => Controller.BatchGetSummary(new BatchGetReactionSummaryDto { TargetType = Dto.TargetType, TargetIds = TargetIds }),
            _ => Controller.Toggle(Dto)
        };
        public string Snapshot() => JsonSerializer.Serialize(Db.Queryable<Reaction>().OrderBy(r => r.Id).ToList());
        public void Dispose() { _logs.Dispose(); Db.Dispose(); }
    }

    private static ReactionController CreateController(IReactionService service, Func<CurrentUser>? current = null)
    {
        var accessor = new Mock<ICurrentUserAccessor>();
        accessor.SetupGet(a => a.Current).Returns(current ?? (() => new CurrentUser { UserId = UserId, UserName = Secret }));
        return new ReactionController(service, accessor.Object);
    }
    private static Task<MessageModel> CallControllerAsync(ReactionController controller, string operation) => operation switch
    {
        "query" => controller.GetSummary("Post", TargetId),
        "batch" => controller.BatchGetSummary(new BatchGetReactionSummaryDto { TargetType = "Post", TargetIds = [TargetId] }),
        _ => controller.Toggle(new ToggleReactionDto { TargetType = "Post", TargetId = TargetId, EmojiType = "unicode", EmojiValue = Secret })
    };

    private static async Task<MessageModel?> InvokeApiAsync(Func<Task<MessageModel>> action, int status, string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        MessageModel? result = null;
        app.Run(async context => { result = await action(); await ApplyResultAsync(context, result); });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Reaction/Toggle";
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
        private readonly List<LogEvent> _events = [];
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
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(_output, _events));
            _logger = config.CreateLogger();
            Log.Logger = _logger;
        }
        public void AssertQuiet() => Assert.Equal("", _output.ToString());
        public void AssertEvents(params (string Code, string Level, string Kind, int? Status)[] expected)
        {
            var text = _output.ToString();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(expected.Length, lines.Length);
            foreach (var forbidden in new[] { Secret, StickerValue, TargetId.ToString(), UserId.ToString(), AttachmentId.ToString(),
                "Private.Code", "Private.Key", "runtime.unclassified", "UNIQUE constraint", "System.IO.IOException", "idx_reaction", "ConcurrentConflict" })
                Assert.DoesNotContain(forbidden, text);
            for (var i = 0; i < expected.Length; i++)
            {
                var e = expected[i];
                if (_mode != null)
                {
                    using var json = JsonDocument.Parse(lines[i]);
                    var root = json.RootElement;
                    Assert.Equal(_mode, root.GetProperty("mode").GetString());
                    Assert.Equal(e.Code, root.GetProperty("eventCode").GetString());
                    Assert.Equal(e.Level, root.GetProperty("level").GetString());
                    Assert.False(root.GetProperty("diagnostic").GetBoolean());
                    var properties = root.GetProperty("properties");
                    Assert.Equal(e.Status.HasValue ? 2 : 1, properties.EnumerateObject().Count());
                    Assert.Equal(e.Kind, properties.GetProperty("failureKind").GetString());
                    if (e.Status.HasValue) Assert.Equal(e.Status.Value, properties.GetProperty("statusCode").GetInt32());
                }
                else
                {
                    var logged = _events[i];
                    Assert.Equal(e.Level, logged.Level.ToString());
                    Assert.Null(logged.Exception);
                    Assert.Equal(e.Code, Assert.IsType<ScalarValue>(logged.Properties["EventCode"]).Value);
                    Assert.Equal(e.Kind, Assert.IsType<ScalarValue>(logged.Properties["failureKind"]).Value);
                    if (e.Status.HasValue) Assert.Equal(e.Status.Value, Assert.IsType<ScalarValue>(logged.Properties["statusCode"]).Value);
                    Assert.All(logged.Properties.Keys, k => Assert.Contains(k, new[] { "EventCode", "SourceCategory", "SourceContext", "failureKind", "statusCode" }));
                }
            }
        }
        public void Clear() { _output.GetStringBuilder().Clear(); _events.Clear(); }
        public void Dispose() { Log.Logger = _previous; _logger.Dispose(); _output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output, List<LogEvent> events) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(LogEvent value) { events.Add(value); _formatter.Format(value, output); }
    }
}
