using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Hubs;
using Radish.Api.Services;
using Radish.Common.CacheTool;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.Extension.AopExtension;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using Radish.Shared;
using Radish.Shared.Constants;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class CommentHighlightLoggingTests
{
    private const string Secret = "COMMENT_HIGHLIGHT_PRIVATE_SENTINEL";
    private const long PostId = 812345679;
    private const long CommentId = 923456781;
    private const long ParentId = 734567891;
    private const long UserId = 623456789;
    private const long HighlightId = 512345678;
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ReplacementAndReplay_ShouldKeepRankRewardsCacheOrderingAndQuietSuccess(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var sofa in new[] { false, true })
        {
            using var f = new Fixture(sofa);
            f.Top.Add(f.NewComment(CommentId + 1));
            var result = await f.Proxy.TriggerHighlightRecheckAsync(PostId, f.Parent);
            Assert.True(result.VoChanged);
            Assert.Equal(new[] { CommentId, CommentId + 1 }, result.VoCurrentCommentIds);
            Assert.False(f.Highlights[0].IsCurrent);
            Assert.Equal(new[] { "begin", "count", "top", "existing", "settings", "retire", "insert", "base-reward", "base-reward", "cache", "commit" }, f.Steps);
            var created = f.Highlights.Where(h => h.IsCurrent).ToList();
            Assert.Equal(new[] { 1, 2 }, created.Select(h => h.Rank));
            for (var i = 0; i < created.Count; i++)
            {
                var row = created[i];
                Assert.Equal(f.Parent, row.ParentCommentId);
                Assert.Equal(f.Type, row.HighlightType);
                Assert.Equal(Secret, row.ContentSnapshot);
                Assert.Equal(Secret, row.AuthorName);
                Assert.Equal(UserId, row.AuthorId);
                Assert.Equal("CommentService.RealTime", row.CreateBy);
                var draft = f.Drafts[i];
                Assert.Equal($"task:highlight-base:{row.Id}", draft.IdempotencyKey);
                Assert.Equal(new HighlightBaseRewardTaskPayload(row.Id, row.CommentId, UserId, f.Type, 9), JsonSerializer.Deserialize<HighlightBaseRewardTaskPayload>(draft.PayloadJson));
            }
            Assert.Equal(f.CacheKey, Assert.Single(f.CacheKeys));
            f.Steps.Clear();
            var replay = await f.Proxy.TriggerHighlightRecheckAsync(PostId, f.Parent);
            Assert.False(replay.VoChanged);
            Assert.Equal(result.VoCurrentCommentIds, replay.VoCurrentCommentIds);
            Assert.Equal(new[] { "begin", "count", "top", "existing", "commit" }, f.Steps);
            Assert.Equal(2, f.Drafts.Count);
            capture.AssertEvents();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ConsumedRecheckFailures_ShouldKeepPartialWritesNoChangeAndTransactionCompletion(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var sofa in new[] { false, true })
        foreach (var stage in new[] { "count", "top", "existing", "settings", "retire", "insert", "base-reward", "cache" })
        {
            using var f = new Fixture(sofa) { FailedStage = stage };
            var result = await f.Proxy.TriggerHighlightRecheckAsync(PostId, f.Parent);
            Assert.False(result.VoChanged);
            Assert.Empty(result.VoCurrentCommentIds);
            Assert.Equal(PostId, result.VoPostId);
            Assert.Equal(f.Parent, result.VoParentCommentId);
            Assert.Equal(f.Type, result.VoHighlightType);
            Assert.Equal(stage is not ("insert" or "base-reward" or "cache"), f.Highlights[0].IsCurrent);
            Assert.Equal(stage is "base-reward" or "cache" ? 2 : 1, f.Highlights.Count);
            Assert.Equal(stage == "cache" ? 1 : 0, f.Drafts.Count);
            f.Unit.Verify(u => u.CommitTran(It.IsAny<MethodInfo>()), Times.Once);
            f.Unit.Verify(u => u.RollbackTran(It.IsAny<MethodInfo>()), Times.Never);
            capture.AssertEvents((f.RecheckEvent, "io"));
            capture.Clear();
        }
        foreach (var (exception, kind) in new (Exception, string)[]
        {
            (new OperationCanceledException(Secret), "cancelled"), (new TimeoutException(Secret), "timeout"),
            (new OpaqueException(), "other")
        })
        {
            using var f = new Fixture(false) { FailedStage = "count", Failure = exception };
            Assert.False((await f.Service.TriggerHighlightRecheckAsync(PostId)).VoChanged);
            capture.AssertEvents((f.RecheckEvent, kind));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ExistingHighlightBonus_ShouldStayBeforeSnapshotAndPreserveFailureProgress(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var sofa in new[] { false, true })
        foreach (var stage in new string?[] { null, "bonus-reward", "snapshot", "cache" })
        {
            using var f = new Fixture(sofa) { FailedStage = stage };
            f.Highlights[0].CommentId = CommentId;
            f.Highlights[0].LikeCount = 6;
            var result = await f.Service.TriggerHighlightRecheckAsync(PostId, f.Parent);
            Assert.Equal(stage == null, result.VoChanged);
            Assert.Equal(stage is null or "cache" ? 9 : 6, f.Highlights[0].LikeCount);
            Assert.Equal(stage == "bonus-reward" ? 0 : 1, f.Drafts.Count);
            if (f.Drafts.Count > 0)
            {
                var draft = Assert.Single(f.Drafts);
                Assert.Equal($"task:highlight-bonus:{HighlightId}:to-like:9", draft.IdempotencyKey);
                Assert.Equal(new HighlightBonusRewardTaskPayload(HighlightId, UserId, f.Type, 3, 9), JsonSerializer.Deserialize<HighlightBonusRewardTaskPayload>(draft.PayloadJson));
            }
            if (stage is null or "cache")
                Assert.Equal(new[] { "count", "top", "existing", "bonus-reward", "snapshot", "cache" }, f.Steps);
            capture.AssertEvents(stage == null ? [] : [(f.RecheckEvent, "io")]);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DisabledEmptyAndClearPaths_ShouldKeepThresholdAndCacheFailureSemantics(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var sofa in new[] { false, true })
        {
            using (var disabled = new Fixture(sofa))
            {
                disabled.Options.RealtimeUpdate = false;
                Assert.False((await disabled.Service.TriggerHighlightRecheckAsync(PostId, disabled.Parent)).VoChanged);
                Assert.Empty(disabled.Steps);
            }
            using (var empty = new Fixture(sofa))
            {
                empty.Top.Clear();
                Assert.False((await empty.Service.TriggerHighlightRecheckAsync(PostId, empty.Parent)).VoChanged);
                Assert.Equal(new[] { "count", "top" }, empty.Steps);
            }
            foreach (var belowThreshold in new[] { false, true })
            foreach (var cacheFails in new[] { false, true })
            {
                using var f = new Fixture(sofa) { FailedStage = cacheFails ? "cache" : null };
                if (belowThreshold) f.Count = sofa ? f.Options.MinChildCommentCount : f.Options.MinParentCommentCount;
                else f.Top[0].LikeCount = 0;
                var result = await f.Service.TriggerHighlightRecheckAsync(PostId, f.Parent);
                Assert.Equal(!cacheFails, result.VoChanged);
                Assert.False(f.Highlights[0].IsCurrent);
                Assert.Empty(f.Drafts);
                capture.AssertEvents(cacheFails ? [(f.RecheckEvent, "io")] : []);
                capture.Clear();
            }
        }
        capture.AssertEvents();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task FillFailure_ShouldReturnRootPageWhileSingleDetailAndOuterFailuresStillPropagate(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "fill-1", "fill-2", "duplicate" })
        {
            using var f = new Fixture(false) { HighlightQueryStage = "fill", FailedStage = stage == "duplicate" ? null : stage };
            f.Highlights[0].CommentId = CommentId;
            if (stage == "duplicate") f.Highlights.Add(f.Highlights[0]);
            var response = await f.Controller.GetRootComments(PostId);
            Assert.True(response.IsSuccess);
            var page = Assert.IsType<VoPagedResult<CommentVo>>(response.ResponseData);
            Assert.Equal(10, page.VoTotal);
            var comment = Assert.Single(page.VoItems);
            Assert.Equal(CommentId, comment.VoId);
            Assert.Equal(Secret, comment.VoContent);
            Assert.False(comment.VoIsGodComment);
            Assert.Contains("profiles", f.Steps);
            capture.AssertEvents(("comment.highlight_fill_failed", stage == "duplicate" ? "argument" : "io"));
            capture.Clear();
        }
        foreach (var stage in new[] { "top", "single", "profiles" })
        {
            using var f = new Fixture(false) { HighlightQueryStage = "fill", FailedStage = stage };
            Func<Task> call = stage == "single" ? () => f.Service.GetCommentDetailAsync(CommentId) : () => f.Controller.GetRootComments(PostId);
            Assert.Same(f.Failure, await Assert.ThrowsAsync<IOException>(call));
            capture.AssertEvents();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SuccessfulFill_ShouldPreserveRootAndRecursiveSofaFlagsWithoutLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(false) { HighlightQueryStage = "fill" };
        f.Highlights[0].CommentId = CommentId;
        f.Highlights[0].Rank = 2;
        f.Highlights.Add(new CommentHighlight { Id = HighlightId + 1, PostId = PostId, ParentCommentId = CommentId, CommentId = CommentId + 1, IsCurrent = true, LikeCount = 3, HighlightType = 2, Rank = 1 });
        var response = await f.Controller.GetRootComments(PostId);
        var page = Assert.IsType<VoPagedResult<CommentVo>>(response.ResponseData);
        var root = Assert.Single(page.VoItems);
        Assert.True(root.VoIsGodComment);
        Assert.Equal(2, root.VoHighlightRank);
        Assert.Empty(root.VoChildren!);
        Assert.Equal(4, root.VoChildrenTotal);
        // The current public root-page path clears children; exercise the retained recursive helper separately.
        root.VoChildren = [new CommentVo { VoId = CommentId + 1, VoParentId = CommentId, VoContent = Secret }];
        var fill = typeof(CommentService).GetMethod("FillHighlightStatusAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)fill.Invoke(f.Service, [PostId, new List<CommentVo> { root }])!;
        Assert.True(root.VoChildren[0].VoIsSofa);
        Assert.Equal(1, root.VoChildren[0].VoHighlightRank);
        capture.AssertEvents();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DirectConsumers_ShouldKeepSuccessfulWritesAndSuppressUnchangedHighlightBroadcasts(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in new[] { "delete", "like", "create" })
        {
            using var f = new Fixture(false) { FailedStage = "count" };
            if (operation == "create")
            {
                var result = await f.Proxy.AddCommentAsync(f.NewComment(CommentId + 1));
                Assert.Equal(CommentId + 1, result.commentId);
                Assert.False(result.highlightRecheckResult.VoChanged);
                Assert.Equal(ReliableTaskTypes.CommentPublished, Assert.Single(f.Drafts).TaskType);
                Assert.Contains("post-count", f.Steps);
                f.Unit.Verify(u => u.CommitTran(It.IsAny<MethodInfo>()), Times.Once);
                f.Unit.Verify(u => u.RollbackTran(It.IsAny<MethodInfo>()), Times.Never);
            }
            else
            {
                var response = operation == "delete" ? await f.Controller.Delete(CommentId) : await f.Controller.ToggleLike(CommentId);
                Assert.True(response.IsSuccess);
                Assert.Equal(operation == "delete", f.Top[0].IsDeleted);
                Assert.Equal(operation == "delete" ? "CommentDeleted" : "CommentLikeChanged", Assert.Single(f.Pushes));
                if (operation == "like") Assert.False(Assert.IsType<CommentLikeResultDto>(response.ResponseData).HighlightRecheckResult!.VoChanged);
            }
            capture.AssertEvents((f.RecheckEvent, "io"));
            capture.Clear();
        }
        using (var f = new Fixture(false) { LikeDelta = 0 })
        {
            Assert.True((await f.Controller.ToggleLike(CommentId)).IsSuccess);
            Assert.DoesNotContain("count", f.Steps);
            Assert.Equal("CommentLikeChanged", Assert.Single(f.Pushes));
            capture.AssertEvents();
        }
    }

    private sealed class OpaqueException : Exception
    {
        public override string Message => throw new InvalidOperationException("Exception text must not be evaluated");
        public override string ToString() => throw new InvalidOperationException("Exception text must not be evaluated");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ILoggerFactory _factory;
        private int _highlightQueries;
        public long? Parent { get; }
        public int Type => Parent.HasValue ? 2 : 1;
        public string RecheckEvent => Parent.HasValue ? "comment.sofa_recheck_failed" : "comment.god_recheck_failed";
        public string CacheKey => Parent.HasValue ? $"sofas:parent:{ParentId}" : $"god_comments:post:{PostId}";
        public string? FailedStage { get; set; }
        public string HighlightQueryStage { get; set; } = "existing";
        public Exception Failure { get; set; } = new IOException(Secret);
        public int Count { get; set; } = 10;
        public int LikeDelta { get; set; } = 1;
        public CommentHighlightOptions Options { get; } = new();
        public List<Comment> Top { get; } = [];
        public List<CommentHighlight> Highlights { get; } = [];
        public List<ReliableOutboxDraft> Drafts { get; } = [];
        public List<string> Steps { get; } = [];
        public List<string> CacheKeys { get; } = [];
        public List<string> Pushes { get; } = [];
        public Mock<IUnitOfWorkManage> Unit { get; } = new();
        public CommentService Service { get; }
        public ICommentService Proxy { get; }
        public CommentController Controller { get; }

        public Fixture(bool sofa)
        {
            Parent = sofa ? ParentId : null;
            Top.Add(NewComment(CommentId));
            Highlights.Add(new CommentHighlight
            {
                Id = HighlightId, PostId = PostId, ParentCommentId = Parent, CommentId = CommentId + 99,
                AuthorId = UserId, AuthorName = Secret, ContentSnapshot = Secret, LikeCount = 2, Rank = 1,
                IsCurrent = true, HighlightType = Type, TenantId = 9, CreateTime = DateTime.Now
            });
            var comments = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
            comments.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
                .Callback(() => Step("count")).ReturnsAsync(() => Count);
            comments.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<Comment, bool>>?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<Expression<Func<Comment, object>>?>(), It.IsAny<OrderByType>(), It.IsAny<Expression<Func<Comment, object>>?>(), It.IsAny<OrderByType>()))
                .Callback(() => Step("top")).ReturnsAsync(() => (Top.ToList(), Count));
            comments.Setup(r => r.QueryByIdAsync(CommentId)).Callback(() => Step("detail")).ReturnsAsync(() => Top[0]);
            comments.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
                .Callback(() => Step("comment-first")).ReturnsAsync(() => Top[0]);
            comments.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Comment, Comment>>>(), It.IsAny<Expression<Func<Comment, bool>>>()))
                .Returns<Expression<Func<Comment, Comment>>, Expression<Func<Comment, bool>>>((columns, where) =>
                {
                    Step("delete");
                    return Task.FromResult(ApplyColumns(Top, columns, where));
                });
            comments.Setup(r => r.AddAsync(It.IsAny<Comment>())).Returns<Comment>(c => { Step("create"); return Task.FromResult(c.Id); });
            var highlights = new Mock<IBaseRepository<CommentHighlight>>(MockBehavior.Strict);
            highlights.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>?>()))
                .Returns<Expression<Func<CommentHighlight, bool>>?>(where =>
                {
                    Step(HighlightQueryStage == "fill" ? $"fill-{++_highlightQueries}" : "existing");
                    return Task.FromResult(Highlights.Where(where!.Compile()).ToList());
                });
            highlights.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>?>()))
                .Returns<Expression<Func<CommentHighlight, bool>>?>(where => { Step("single"); return Task.FromResult(Highlights.FirstOrDefault(where!.Compile())); });
            highlights.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<CommentHighlight, CommentHighlight>>>(), It.IsAny<Expression<Func<CommentHighlight, bool>>>()))
                .Returns<Expression<Func<CommentHighlight, CommentHighlight>>, Expression<Func<CommentHighlight, bool>>>((columns, where) =>
                {
                    Step(((MemberInitExpression)columns.Body).Bindings.Any(b => b.Member.Name == "IsCurrent") ? "retire" : "snapshot");
                    return Task.FromResult(ApplyColumns(Highlights, columns, where));
                });
            highlights.Setup(r => r.AddRangeAsync(It.IsAny<List<CommentHighlight>>())).Returns<List<CommentHighlight>>(rows =>
            {
                Step("insert");
                foreach (var row in rows) { row.Id = HighlightId + Highlights.Count; Highlights.Add(row); }
                return Task.FromResult(rows.Count);
            });
            var cache = new Mock<ICaching>(MockBehavior.Strict);
            cache.Setup(c => c.RemoveAsync(It.IsAny<string>())).Returns<string>(key => { Step("cache"); CacheKeys.Add(key); return Task.CompletedTask; });
            var outbox = new Mock<IReliableOutboxRepository>(MockBehavior.Strict);
            outbox.Setup(o => o.AddAsync(It.IsAny<ReliableOutboxDraft>())).Returns<ReliableOutboxDraft>(draft =>
            {
                Step(draft.TaskType switch { ReliableTaskTypes.HighlightBaseReward => "base-reward", ReliableTaskTypes.HighlightBonusReward => "bonus-reward", _ => "published" });
                Assert.Equal(ReliableOutboxSources.Main, draft.SourceDatabase);
                Assert.Equal(9, draft.TenantId);
                Assert.Equal(DateTimeKind.Utc, draft.OccurredAtUtc.Kind);
                Drafts.Add(draft);
                return Task.FromResult((long)Drafts.Count);
            });
            var settings = new Mock<ISystemSettingProvider>(MockBehavior.Strict);
            settings.Setup(s => s.GetInt32Async(SystemConfigDefaults.CommentHighlightStabilityWindowMinutesKey))
                .Callback(() => Step("settings")).ReturnsAsync(0);
            settings.Setup(s => s.GetInt32Async(SystemConfigDefaults.CommentBodyMinLengthKey)).ReturnsAsync(1);
            settings.Setup(s => s.GetInt32Async(SystemConfigDefaults.CommentBodyMaxLengthKey)).ReturnsAsync(2000);
            var mapper = new Mock<IMapper>(MockBehavior.Strict);
            mapper.Setup(m => m.Map<List<CommentVo>>(It.IsAny<object>())).Returns<object>(rows => ((List<Comment>)rows).Select(ToVo).ToList());
            mapper.Setup(m => m.Map<CommentVo>(It.IsAny<object>())).Returns<object>(c => ToVo((Comment)c));
            var likes = new Mock<IBaseRepository<UserCommentLike>>(MockBehavior.Strict);
            likes.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserCommentLike, bool>>?>())).ReturnsAsync([]);
            var custom = new Mock<ICommentRepository>(MockBehavior.Strict);
            custom.Setup(r => r.ToggleCommentLikeAsync(UserId, Secret, CommentId)).Callback(() => Step("like"))
                .ReturnsAsync(() => new CommentLikePersistenceResult(CommentId, 9, PostId, Parent, UserId, Secret, true, 9, LikeDelta));
            var post = new Mock<IPostService>(MockBehavior.Strict);
            post.Setup(p => p.UpdateCommentCountAsync(PostId, 1)).Callback(() => Step("post-count")).Returns(Task.CompletedTask);
            var adornments = new Mock<IUserAdornmentService>(MockBehavior.Strict);
            adornments.Setup(a => a.GetUserAdornmentsAsync(It.IsAny<IReadOnlyCollection<long>>())).Callback(() => Step("profiles"))
                .ReturnsAsync(new Dictionary<long, UserAdornmentVo>());
            Service = new CommentService(mapper.Object, comments.Object, likes.Object, highlights.Object, post.Object, cache.Object,
                Mock.Of<ICoinRewardService>(), Mock.Of<INotificationService>(), Mock.Of<INotificationDedupService>(), Mock.Of<IExperienceService>(),
                Mock.Of<IAttachmentUrlResolver>(), Microsoft.Extensions.Options.Options.Create(Options), Mock.Of<IBaseRepository<CommentEditHistory>>(),
                Microsoft.Extensions.Options.Options.Create(new ForumEditHistoryOptions()), settings.Object,
                commentCustomRepository: custom.Object, reliableOutboxService: new ReliableOutboxService(outbox.Object), userAdornmentService: adornments.Object);
            Unit.Setup(u => u.BeginTran(It.IsAny<MethodInfo>())).Callback(() => Steps.Add("begin"));
            Unit.Setup(u => u.CommitTran(It.IsAny<MethodInfo>())).Callback(() => Steps.Add("commit"));
            Unit.Setup(u => u.RollbackTran(It.IsAny<MethodInfo>())).Callback(() => Steps.Add("rollback"));
            Proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<ICommentService>(Service, new TranAop(Unit.Object));
            _factory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Trace).AddSerilog(Log.Logger, dispose: false));
            var client = new Mock<IClientProxy>(MockBehavior.Strict);
            client.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((name, _, _) => Pushes.Add(name)).Returns(Task.CompletedTask);
            var hub = new Mock<IHubContext<CommentHub>>(MockBehavior.Strict);
            var clients = new Mock<IHubClients>(MockBehavior.Strict);
            clients.Setup(c => c.Group($"post-comments:{PostId}")).Returns(client.Object);
            hub.SetupGet(h => h.Clients).Returns(clients.Object);
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = UserId, UserName = Secret, TenantId = 9, IsAuthenticated = true });
            Controller = new CommentController(Proxy, post.Object, Mock.Of<IUserService>(), Mock.Of<IContentModerationService>(), current.Object,
                new CommentRealtimePushService(hub.Object, _factory.CreateLogger<CommentRealtimePushService>()), Mock.Of<IForumContentWriteService>());
        }

        public Comment NewComment(long id) => new()
        {
            Id = id, PostId = PostId, ParentId = Parent, RootId = Parent, AuthorId = UserId, AuthorName = Secret,
            Content = Secret, TenantId = 9, LikeCount = 9, IsEnabled = true, ReplyCount = 4, CreateTime = DateTime.Now
        };
        private static CommentVo ToVo(Comment comment) => new()
        {
            VoId = comment.Id, VoPostId = comment.PostId, VoParentId = comment.ParentId, VoRootId = comment.RootId,
            VoAuthorId = comment.AuthorId, VoAuthorName = comment.AuthorName, VoContent = comment.Content, VoLikeCount = comment.LikeCount
        };
        private static int ApplyColumns<T>(List<T> rows, Expression<Func<T, T>> columns, Expression<Func<T, bool>> where)
        {
            var selected = rows.Where(where.Compile()).ToList();
            var getValues = columns.Compile();
            foreach (var row in selected)
            {
                var values = getValues(row);
                foreach (var binding in ((MemberInitExpression)columns.Body).Bindings)
                {
                    var property = (PropertyInfo)binding.Member;
                    property.SetValue(row, property.GetValue(values));
                }
            }
            return selected.Count;
        }
        private void Step(string stage) { Steps.Add(stage); if (FailedStage == stage) throw Failure; }
        public void Dispose() => _factory.Dispose();
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        private readonly StringWriter _output = new();
        private readonly string? _mode;
        private readonly Serilog.Core.Logger _logger;
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate)
            {
                _mode = environment;
                var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RadishLogging:Mode"] = environment,
                    ["RadishLogging:Diagnostics"] = environment == "Development" ? "true" : "false"
                }).Build();
                RuntimeLoggingConfiguration.Configure(config, settings, environment, "api", _output, _output);
            }
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(_output));
            _logger = config.CreateLogger();
            Log.Logger = _logger;
        }
        public void AssertEvents(params (string Code, string Kind)[] events)
        {
            var text = _output.ToString();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(events.Length, lines.Length);
            for (var i = 0; i < lines.Length; i++)
            {
                Assert.Contains(events[i].Code, lines[i]);
                Assert.Contains("Error", lines[i]);
                if (_mode != null)
                {
                    using var json = JsonDocument.Parse(lines[i]);
                    Assert.Equal(_mode, json.RootElement.GetProperty("mode").GetString());
                    Assert.Equal(events[i].Code, json.RootElement.GetProperty("eventCode").GetString());
                    Assert.Equal("Error", json.RootElement.GetProperty("level").GetString());
                    var properties = json.RootElement.GetProperty("properties");
                    Assert.Equal(events[i].Kind, properties.GetProperty("failureKind").GetString());
                    Assert.Single(properties.EnumerateObject());
                }
                else Assert.Contains($"\"failureKind\":\"{events[i].Kind}\"", lines[i]);
            }
            foreach (var forbidden in new[] { Secret, PostId.ToString(), CommentId.ToString(), ParentId.ToString(), UserId.ToString(), HighlightId.ToString(), "System.IO.IOException", "runtime.unclassified", "god_comments:", "sofas:parent:", "task:highlight-" })
                Assert.DoesNotContain(forbidden, text);
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
