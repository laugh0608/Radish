using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Filters;
using Radish.Api.Resources;
using Radish.Common.CoreTool;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.Extension.AopExtension;
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
public sealed class PostControllerLoggingTests
{
    private const string Secret = "POST_WRITE_PRIVATE_SENTINEL";
    private const long PostId = 912345678;
    private const long UserId = 623456789;
    private const long TenantId = 734567891;
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ExplicitRejections_ShouldKeepQuietStatusesAndTransactionDecisions(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var scenario in new[] { "empty-title", "empty-content", "tags", "short-title", "long-title", "short-body", "long-body", "limit", "category", "category-missing", "category-disabled", "category-deleted", "missing", "missing-later", "forbidden", "revision", "cas", "tag-denied" })
        {
            var f = new Fixture();
            var status = 400;
            switch (scenario)
            {
                case "empty-title": f.Title = " "; break;
                case "empty-content": f.Content = " "; break;
                case "tags": f.Tags = []; break;
                case "short-title": f.MinTitle = 80; break;
                case "long-title": f.MaxTitle = 3; break;
                case "short-body": f.MinBody = 80; break;
                case "long-body": f.MaxBody = 3; break;
                case "limit": f.Stored.EditCount = 3; status = 403; break;
                case "category": f.CategoryId = 0; status = 403; break;
                case "category-missing": f.Categories.Remove(102); status = 403; break;
                case "category-disabled": f.Categories[102].IsEnabled = false; status = 403; break;
                case "category-deleted": f.Categories[102].IsDeleted = true; status = 403; break;
                case "missing": f.Stored.IsDeleted = true; status = 404; break;
                case "missing-later": f.MissingRead = 2; status = 403; break;
                case "forbidden": f.Admin = false; f.Stored.AuthorId++; status = 403; break;
                case "revision": f.ExpectedRevision = 2; status = 409; break;
                case "cas": f.CasRows = 0; status = 409; break;
                case "tag-denied": f.Admin = false; f.AvailableTags.RemoveAt(1); status = 403; break;
            }
            var result = await InvokeApiAsync(() => f.CallAsync("edit"), status);
            if (result != null) Assert.False(result.IsSuccess);
            Assert.DoesNotContain("completion", f.Steps);
            if (f.Steps.Contains("begin")) f.AssertTransaction("rollback");
            capture.AssertQuiet();
        }
        foreach (var scenario in new[] { "invalid-id", "non-admin", "missing", "deleted", "unpublished", "disabled-detail" })
        {
            var f = new Fixture();
            var status = 404;
            switch (scenario)
            {
                case "invalid-id": f.RequestPostId = 0; status = 400; break;
                case "non-admin": f.Admin = false; status = 403; break;
                case "missing": f.MissingRead = 1; break;
                case "deleted": f.Stored.IsDeleted = true; break;
                case "unpublished": f.Stored.IsPublished = false; break;
                case "disabled-detail": f.Stored.IsEnabled = false; break;
            }
            var result = await InvokeApiAsync(() => f.CallAsync("top"), status);
            Assert.False(result!.IsSuccess);
            if (f.Steps.Contains("begin")) f.AssertTransaction("rollback");
            capture.AssertQuiet();
        }
        foreach (var state in new[] { ContentSubmissionBeginStatus.InvalidKey, ContentSubmissionBeginStatus.Conflict, ContentSubmissionBeginStatus.Processing, ContentSubmissionBeginStatus.FrequencyLimited })
        {
            var f = new Fixture { BeginStatus = state };
            await InvokeApiAsync(() => f.CallAsync("edit"), state == ContentSubmissionBeginStatus.InvalidKey ? 400 : state == ContentSubmissionBeginStatus.FrequencyLimited ? 429 : 409);
            f.AssertTransaction("rollback");
            Assert.DoesNotContain("columns", f.Steps);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ConsumedStageFailures_ShouldHaveOneSafeErrorAndKeepResponse(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var failure in new Exception[] { new ArgumentException(Secret), new InvalidOperationException(Secret) })
        foreach (var stage in new[] { "post-read", "snapshot", "submission", "settings", "category-write", "columns", "tag-resolve", "relation-delete", "relation-add", "tag-write", "revision", "completion" })
        {
            var f = new Fixture { FailedStage = stage, Failure = failure };
            var result = await InvokeApiAsync(() => f.CallAsync("edit"), failure is ArgumentException ? 400 : 403);
            Assert.Equal(Secret, result!.MessageInfo);
            f.AssertTransaction("rollback");
            capture.AssertSingle("post.edit_failed", RuntimeFailureSummary.Classify(failure));
            capture.Clear();
        }
        foreach (var stage in new[] { "post-read", "top-write", "category-read", "post-tags" })
        {
            var f = new Fixture { FailedStage = stage, Failure = new InvalidOperationException(Secret) };
            var result = await InvokeApiAsync(() => f.CallAsync("top"), 404);
            Assert.Equal(Secret, result!.MessageInfo);
            f.AssertTransaction("rollback");
            capture.AssertSingle("post.top_failed", "invalid-operation");
            capture.Clear();
        }
        foreach (var titleConfig in new[] { true, false })
        {
            var f = new Fixture();
            if (titleConfig) f.MinTitle = 200;
            else f.MinBody = 200;
            await InvokeApiAsync(() => f.CallAsync("edit"), 403);
            f.AssertTransaction("rollback");
            capture.AssertSingle("post.edit_failed", "invalid-operation");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task UnhandledAndDirectFailures_ShouldPreserveExceptionAndApiOwner(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in new[] { "edit", "top" })
        foreach (var failure in new Exception[] { new IOException(Secret), new TimeoutException(Secret), new OperationCanceledException(Secret), new BusinessException(Secret, 503), new HostileFailure() })
        {
            var direct = new Fixture { FailedStage = operation == "edit" ? "completion" : "top-write", Failure = failure };
            Exception? observed = null;
            try { await direct.CallServiceAsync(operation); }
            catch (Exception ex) { observed = ex; }
            Assert.Same(failure, observed);
            direct.AssertTransaction("rollback");
            capture.AssertQuiet();
            var f = new Fixture { FailedStage = direct.FailedStage, Failure = failure };
            var status = failure is BusinessException ? 503 : 500;
            await InvokeApiAsync(() => f.CallAsync(operation), status);
            f.AssertTransaction("rollback");
            capture.AssertSingle("http.failed", RuntimeFailureSummary.Classify(failure), status);
            capture.Clear();
        }
        foreach (var operation in new[] { "edit", "top" })
        {
            var failure = new ArgumentException(Secret);
            var f = new Fixture { FailedStage = operation == "edit" ? "pre-query" : "top-write", Failure = failure };
            await InvokeApiAsync(() => f.CallAsync(operation), 500);
            if (operation == "edit") Assert.DoesNotContain("begin", f.Steps);
            else f.AssertTransaction("rollback");
            capture.AssertSingle("http.failed", "argument", 500);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SuccessAndReplay_ShouldKeepAuditCategoryTagsAndCommitOrder(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        var before = DateTime.Now;
        var response = await InvokeApiAsync(() => f.CallAsync("edit"), 200);
        var edit = Assert.IsType<PostEditResult>(response!.ResponseData);
        Assert.Equal(PostId, edit.PostId);
        Assert.Equal(42, edit.RevisionId);
        Assert.Equal(2, edit.ContentRevision);
        Assert.Equal(f.Title.Trim(), f.Stored.Title);
        Assert.Equal(f.Content.Trim(), f.Stored.Content);
        Assert.Equal(1, f.Stored.EditCount);
        Assert.Equal(2, f.Stored.ContentRevision);
        Assert.Equal(UserId, f.Stored.ModifyId);
        Assert.Equal(Secret, f.Stored.ModifyBy);
        Assert.InRange(f.Stored.ModifyTime!.Value, before, DateTime.Now);
        Assert.Equal(2, f.Categories[101].PostCount);
        Assert.Equal(5, f.Categories[102].PostCount);
        Assert.Equal(202, Assert.Single(f.Relations).TagId);
        Assert.Equal(2, f.AvailableTags[0].PostCount);
        Assert.Equal(5, f.AvailableTags[1].PostCount);
        Assert.True(f.Steps.IndexOf("columns") < f.Steps.IndexOf("revision"));
        Assert.True(f.Steps.IndexOf("revision") < f.Steps.IndexOf("completion"));
        Assert.True(f.Steps.IndexOf("completion") < f.Steps.IndexOf("commit"));
        var completion = Assert.Single(f.Completions);
        Assert.Equal(ContentSubmissionResultTypes.PostContentRevision, completion.ResultType);
        Assert.Equal(42, completion.ResultId);
        f.AssertTransaction("commit");
        foreach (var scenario in new[] { "same", "replay", "duplicate", "revision-replay" })
        {
            var replay = new Fixture();
            if (scenario == "same")
            {
                replay.Title = replay.Stored.Title; replay.Content = replay.Stored.Content;
                replay.CategoryId = replay.Stored.CategoryId; replay.Tags = ["old"];
            }
            else replay.BeginStatus = scenario == "duplicate" ? ContentSubmissionBeginStatus.DuplicateContent : ContentSubmissionBeginStatus.Succeeded;
            replay.ReplayRevision = scenario == "revision-replay";
            await InvokeApiAsync(() => replay.CallAsync("edit"), 200);
            Assert.DoesNotContain("columns", replay.Steps);
            Assert.DoesNotContain("revision", replay.Steps);
            Assert.Equal(scenario == "same" ? 1 : 0, replay.Completions.Count);
            replay.AssertTransaction("commit");
        }
        var top = new Fixture();
        for (var index = 0; index < 3; index++)
        {
            top.Top = index < 2;
            var result = await InvokeApiAsync(() => top.CallAsync("top"), 200);
            Assert.Equal(top.Top, Assert.IsType<PostVo>(result!.ResponseData).VoIsTop);
        }
        Assert.Equal(2, top.Steps.Count(s => s == "top-write"));
        Assert.Equal(3, top.Steps.Count(s => s == "commit"));
        Assert.Equal(UserId, top.Stored.ModifyId);
        Assert.Equal(Secret, top.Stored.ModifyBy);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DirectValidation_ShouldKeepParentContractAndArgumentNames(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var scenario in new[] { "title", "content", "tags-null", "tags-count" })
        {
            var f = new Fixture();
            if (scenario == "title") f.Title = " ";
            if (scenario == "content") f.Content = " ";
            if (scenario == "tags-null") f.Tags = null;
            if (scenario == "tags-count") f.Tags = [];
            var error = await Assert.ThrowsAsync<PostContentValidationException>(() => f.Service.UpdatePostAsync(PostId, f.Title, f.Content, 102, f.Tags, false, UserId, Secret));
            Assert.IsAssignableFrom<ArgumentException>(error);
            Assert.Equal(scenario.StartsWith("tags", StringComparison.Ordinal) ? "tagNames" : scenario, error.ParamName);
            Assert.DoesNotContain("columns", f.Steps);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SqlitePostWrites_ShouldCommitSuccessAndRollbackConsumedFailures(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        new ServiceCollection().ConfigureApplication();
        foreach (var scenario in new[] { "top-success", "top-detail-failure", "top-disabled", "edit-completion-failure" })
        {
            using var db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", DbType = DbType.Sqlite, ConnectionString = "Data Source=:memory:",
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            db.CodeFirst.InitTables<Post>();
            var unit = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
            var f = new Fixture(new BaseRepository<Post>(unit), unit);
            f.Stored.TenantId = 0;
            if (scenario == "top-disabled") f.Stored.IsEnabled = false;
            await db.Insertable(f.Stored).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            if (scenario.EndsWith("failure", StringComparison.Ordinal))
            {
                f.FailedStage = scenario == "top-detail-failure" ? "category-read" : "completion";
                f.Failure = new InvalidOperationException(Secret);
            }
            var operation = scenario.StartsWith("top", StringComparison.Ordinal) ? "top" : "edit";
            await InvokeApiAsync(() => f.CallAsync(operation), scenario == "top-success" ? 200 : operation == "top" ? 404 : 403);
            var persisted = await db.Queryable<Post>().InSingleAsync(PostId);
            Assert.Equal(scenario == "top-success", persisted.IsTop);
            Assert.Equal("old " + Secret, persisted.Title);
            Assert.Equal(1, persisted.ContentRevision);
            Assert.Equal(0, unit.TranCount);
            if (scenario.EndsWith("failure", StringComparison.Ordinal)) capture.AssertSingle(operation == "top" ? "post.top_failed" : "post.edit_failed", "invalid-operation");
            else capture.AssertQuiet();
            capture.Clear();
        }
    }

    private sealed class HostileFailure : Exception
    {
        public override string Message => throw new InvalidOperationException("不可读取异常原文");
        public override string ToString() => throw new InvalidOperationException("不可渲染异常");
    }

    private sealed class Fixture
    {
        public List<string> Steps { get; } = [];
        public string? FailedStage { get; set; }
        public Exception Failure { get; set; } = new IOException(Secret);
        public bool Admin { get; set; } = true;
        public bool Top { get; set; } = true;
        public int MissingRead { get; set; }
        public int CasRows { get; set; } = 1;
        public long RequestPostId { get; set; } = PostId;
        public string Title { get; set; } = " new " + Secret + " ";
        public string Content { get; set; } = " new body " + Secret + " ";
        public List<string>? Tags { get; set; } = [" new ", "NEW"];
        public long? CategoryId { get; set; } = 102;
        public int ExpectedRevision { get; set; } = 1;
        public int MinTitle { get; set; } = 1;
        public int MaxTitle { get; set; } = 100;
        public int MinBody { get; set; } = 1;
        public int MaxBody { get; set; } = 100;
        public bool ReplayRevision { get; set; }
        public ContentSubmissionBeginStatus BeginStatus { get; set; } = ContentSubmissionBeginStatus.Started;
        public Post Stored { get; } = new("old " + Secret, "old body " + Secret)
        {
            Id = PostId, PublicId = Secret, TenantId = TenantId, AuthorId = UserId, AuthorName = Secret,
            CategoryId = 101, ContentRevision = 1, IsPublished = true, PublishTime = DateTime.Now, IsEnabled = true
        };
        public Dictionary<long, Category> Categories { get; } = new()
        {
            [101] = new Category("old") { Id = 101, IsEnabled = true, PostCount = 3 },
            [102] = new Category("new") { Id = 102, IsEnabled = true, PostCount = 4 }
        };
        public List<Tag> AvailableTags { get; } = [new Tag("old") { Id = 201, IsEnabled = true, PostCount = 3 }, new Tag("new") { Id = 202, IsEnabled = true, PostCount = 4 }];
        public List<PostTag> Relations { get; } = [new PostTag(PostId, 201) { Id = 301 }];
        public List<ContentSubmissionCompletionRequest> Completions { get; } = [];
        public PostService Service { get; }
        public IPostService Posts { get; }
        public IForumContentWriteService Writes { get; }
        public PostController Controller { get; }

        public Fixture(IBaseRepository<Post>? realPosts = null, IUnitOfWorkManage? realUnit = null)
        {
            var repository = new Mock<IBaseRepository<Post>>();
            var reads = 0;
            repository.Setup(r => r.QueryByIdAsync(PostId)).ReturnsAsync(() =>
            { Step("post-read"); return ++reads == MissingRead ? null : Stored; });
            repository.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Post, bool>>?>())).ReturnsAsync((Expression<Func<Post, bool>>? predicate) =>
            { Step("pre-query"); return predicate == null || predicate.Compile()(Stored) ? Stored : null; });
            repository.Setup(r => r.UpdateAsync(It.IsAny<Post>())).ReturnsAsync(() => { Step("top-write"); return true; });
            repository.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Post, Post>>>(), It.IsAny<Expression<Func<Post, bool>>>()))
                .ReturnsAsync(() => { Step("columns"); return CasRows; });
            var mapper = new Mock<IMapper>();
            mapper.Setup(m => m.Map<PostVo>(It.IsAny<Post>())).Returns((Post? post) => post == null ? null! : new PostVo
            {
                VoId = post.Id, VoPublicId = post.PublicId, VoTitle = post.Title, VoContent = post.Content,
                VoAuthorId = post.AuthorId, VoCategoryId = post.CategoryId, VoIsTop = post.IsTop, VoContentRevision = post.ContentRevision
            });
            var category = new Mock<IBaseRepository<Category>>();
            category.Setup(c => c.QueryByIdAsync(It.IsAny<long>())).ReturnsAsync((long id) => { Step("category-read"); return Categories.GetValueOrDefault(id); });
            category.Setup(c => c.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(() => { Step("category-write"); return true; });
            var tags = new Mock<IBaseRepository<Tag>>();
            tags.Setup(t => t.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>?>())).ReturnsAsync((Expression<Func<Tag, bool>>? predicate) =>
            { Step("tag-resolve"); return AvailableTags.Where(t => predicate == null || predicate.Compile()(t)).ToList(); });
            tags.Setup(t => t.UpdateAsync(It.IsAny<Tag>())).ReturnsAsync(() => { Step("tag-write"); return true; });
            var relations = new Mock<IBaseRepository<PostTag>>();
            relations.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>())).ReturnsAsync(() => { Step("post-tags"); return Relations.ToList(); });
#pragma warning disable CS0618
            relations.Setup(r => r.DeleteByIdAsync(It.IsAny<long>())).ReturnsAsync((long id) => { Step("relation-delete"); Relations.RemoveAll(r => r.Id == id); return true; });
#pragma warning restore CS0618
            relations.Setup(r => r.AddAsync(It.IsAny<PostTag>())).ReturnsAsync((PostTag relation) => { Step("relation-add"); Relations.Add(relation); return 302; });
            var tagService = new Mock<ITagService>();
            tagService.Setup(t => t.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>?>())).ReturnsAsync((Expression<Func<Tag, bool>>? predicate) =>
                AvailableTags.Where(t => predicate == null || predicate.Compile()(t)).Select(t => new TagVo { VoId = t.Id, VoName = t.Name }).ToList());
            var settings = new Mock<ISystemSettingProvider>();
            settings.Setup(s => s.GetInt32Async(It.IsAny<string>())).ReturnsAsync((string key) =>
            {
                Step("settings");
                return key switch
                {
                    SystemConfigDefaults.PostTitleMinLengthKey => MinTitle, SystemConfigDefaults.PostTitleMaxLengthKey => MaxTitle,
                    SystemConfigDefaults.PostBodyMinLengthKey => MinBody, SystemConfigDefaults.PostBodyMaxLengthKey => MaxBody, _ => 100
                };
            });
            Service = new PostService(mapper.Object, realPosts ?? repository.Object, Mock.Of<IBaseRepository<UserPostLike>>(), relations.Object,
                category.Object, tags.Object, Mock.Of<IBaseRepository<PostPoll>>(), Mock.Of<IBaseRepository<PostPollOption>>(), Mock.Of<IBaseRepository<PostPollVote>>(),
                Mock.Of<IBaseRepository<PostQuestion>>(), Mock.Of<IBaseRepository<PostAnswer>>(), tagService.Object, Mock.Of<ICoinRewardService>(),
                Mock.Of<INotificationService>(), Mock.Of<INotificationDedupService>(), Mock.Of<IExperienceService>(), Mock.Of<IBaseRepository<PostEditHistory>>(),
                Mock.Of<IAttachmentService>(), Options.Create(new ForumEditHistoryOptions
                { Post = new ForumPostEditHistoryOptions { MaxEditCount = 3 }, AdminOverride = new ForumEditHistoryAdminOverrideOptions { BypassEditCountLimit = false } }), settings.Object);
            var submission = new Mock<IContentSubmissionService>();
            submission.Setup(s => s.CreateRequestSnapshot(It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<IReadOnlyDictionary<string, object?>>()))
                .Returns(() => { Step("snapshot"); return new ContentSubmissionRequestSnapshot { RequestDigest = Secret, RequestSummary = Secret, ContentFingerprint = Secret }; });
            submission.Setup(s => s.BeginAsync(It.IsAny<ContentSubmissionBeginRequest>())).ReturnsAsync(() =>
            {
                Step("submission");
                return new ContentSubmissionBeginResult { Status = BeginStatus, RecordId = 41, ResultType = ReplayRevision ? ContentSubmissionResultTypes.PostContentRevision : ContentSubmissionResultTypes.Post, ResultId = ReplayRevision ? 42 : PostId, RetryAfterSeconds = 10 };
            });
            submission.Setup(s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>())).Callback<ContentSubmissionCompletionRequest>(r =>
            { Step("completion"); Completions.Add(r); }).Returns(Task.CompletedTask);
            var revision = new Mock<IForumContentRevisionService>();
            revision.Setup(r => r.AppendPostRevisionAsync(PostId, ForumContentRevisionSourceTypes.Edit, null, UserId, Secret)).ReturnsAsync(() =>
            { Step("revision"); return new ForumContentRevisionWriteResult { VoRevisionId = 42, VoContentRevision = Stored.ContentRevision }; });
            revision.Setup(r => r.GetCurrentPostRevisionAsync(PostId)).ReturnsAsync(new ForumContentRevisionWriteResult { VoRevisionId = 42, VoContentRevision = 1 });
            revision.Setup(r => r.GetPostRevisionDetailAsync(42, UserId, true)).ReturnsAsync(new PostContentRevisionDetailVo
            { VoPostId = PostId, VoSummary = new PostContentRevisionSummaryVo { VoRevisionNumber = 2 } });
            var unit = new Mock<IUnitOfWorkManage>();
            unit.Setup(u => u.BeginTran(It.IsAny<MethodInfo>())).Callback(() => Step("begin"));
            unit.Setup(u => u.CommitTran(It.IsAny<MethodInfo>())).Callback(() => Step("commit"));
            unit.Setup(u => u.RollbackTran(It.IsAny<MethodInfo>())).Callback(() => Step("rollback"));
            var aop = new TranAop(realUnit ?? unit.Object);
            Posts = new ProxyGenerator().CreateInterfaceProxyWithTarget<IPostService>(Service, aop);
            // 编辑由写入服务统一事务包围；置顶直接通过 PostService 的事务代理。
            Writes = new ProxyGenerator().CreateInterfaceProxyWithTarget<IForumContentWriteService>(
                new ForumContentWriteService(submission.Object, Service, Mock.Of<ICommentService>(), revision.Object), aop);
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(() => new CurrentUser
            { UserId = UserId, UserName = Secret, TenantId = TenantId, IsAuthenticated = true, Roles = Admin ? [UserRoles.Admin] : [] });
            Controller = new PostController(Posts, Mock.Of<IUserService>(), Mock.Of<IContentModerationService>(), null, null,
                Mock.Of<IUserBrowseHistoryService>(), current.Object, Writes, Mock.Of<IStringLocalizer<Errors>>());
        }
        private void Step(string stage) { Steps.Add(stage); if (stage == FailedStage) throw Failure; }
        public Task<MessageModel> CallAsync(string operation) => operation == "top"
            ? Controller.SetTop(new SetPostTopDto { PostId = RequestPostId, IsTop = Top })
            : Controller.Update(new UpdatePostDto { PostId = PostId, Title = Title, Content = Content, CategoryId = CategoryId, TagNames = Tags, ExpectedContentRevision = ExpectedRevision, ClientSubmissionId = Secret });
        public async Task CallServiceAsync(string operation)
        {
            if (operation == "top") await Posts.SetTopAsync(PostId, Top, UserId, Secret);
            else await Writes.UpdatePostAsync(TenantId, PostId, Title, Content, CategoryId, Tags, Admin, UserId, Secret, Admin, Secret, ExpectedRevision);
        }
        public void AssertTransaction(string outcome)
        {
            Assert.Single(Steps, s => s == "begin");
            Assert.Single(Steps, s => s == outcome);
            Assert.DoesNotContain(outcome == "commit" ? "rollback" : "commit", Steps);
        }
    }

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
        context.Request.Path = "/api/v1/Post/Update";
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
            foreach (var forbidden in new[] { Secret, PostId.ToString(), UserId.ToString(), TenantId.ToString(), "runtime.unclassified", "System.IO.IOException", "System.ArgumentException", "System.InvalidOperationException", "PostContentRevision", "ClientSubmissionId", "RequestDigest", "RequestSummary" })
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
