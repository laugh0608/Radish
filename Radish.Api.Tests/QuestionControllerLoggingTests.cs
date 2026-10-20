using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.Repository;
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
public sealed class QuestionControllerLoggingTests
{
    private const string Secret = "QUESTION_PRIVATE_SENTINEL";
    private const long PostId = 912345678;
    private const long UserId = 623456789;
    private const long OwnerId = 734567891;
    private const long AnswerId = 823456789;
    private const long QuestionId = 923456789;
    private const string AnswerPublicId = "ans_0123456789abcdef0123456789abcdef";
    private const string Content = Secret + " ![image](attachment://401)";
    private static readonly string[] Operations = ["answer", "edit", "delete", "restore", "accept", "revoke", "page", "revisions", "revision"];
    private static readonly string[] Mutations = Operations[..6];
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task EveryConsumer_ShouldPreserveDirectAndSingleAggregateContracts(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var aggregate in new[] { false, true })
        foreach (var failure in new Exception[]
        {
            new UnsafeArgument(), new ForumAnswerContentValidationException(Secret, "content"),
            new BusinessException(Secret, 409, "Private.Code", "Private.Key", "line\nvalue", new HostileValue(), 7),
            new BusinessException(Secret, 503, "Private.Code", "Private.Key", Secret)
        })
        {
            var thrown = aggregate ? new AggregateException(new AggregateException(failure)) : failure;
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IForumQuestionService>(new ThrowingInterceptor(thrown));
            var controller = CreateController(service, UserId);
            var status = failure is BusinessException business ? business.StatusCode : 400;
            var result = await InvokeApiAsync(() => CallControllerAsync(controller, operation), status);
            Assert.NotNull(result);
            Assert.Equal(failure.Message, result.MessageInfo);
            if (failure is BusinessException expected)
            {
                Assert.Equal(expected.ErrorCode, result.Code);
                Assert.Equal(expected.MessageKey, result.MessageKey);
                Assert.Equal(status, result.StatusCode);
                if (status == 409) Assert.Equal(new object[] { "line value", "[unsupported]", 7 }, result.MessageArguments);
            }
            if (failure is UnsafeArgument) capture.AssertSingle("question.request_failed", "argument");
            else if (status >= 500) capture.AssertSingle("http.failed", "other", status);
            else capture.AssertQuiet();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task UnknownAndUnreadableFailures_ShouldHaveOnlyApiOwnership(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var (failure, kind) in new (Exception, string)[]
        {
            (new IOException(Secret), "io"), (new InvalidOperationException(Secret), "invalid-operation"),
            (new TimeoutException(Secret), "timeout"), (new OperationCanceledException(Secret), "cancelled"),
            (new HostileFailure(), "other"), (new UnreadableArgument(), "io"),
            (new AggregateException(new UnreadableArgument()), "aggregate"),
            (new AggregateException(new UnsafeArgument(), new IOException(Secret)), "aggregate"),
            (new AggregateException(new UnsafeArgument(), new BusinessException(Secret, 503)), "aggregate"),
            (new AggregateException(), "aggregate"), (new AggregateException(new IOException(Secret)), "aggregate")
        })
        {
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IForumQuestionService>(new ThrowingInterceptor(failure));
            Assert.Null(await InvokeApiAsync(() => CallControllerAsync(CreateController(service, UserId), operation), 500));
            capture.AssertSingle("http.failed", kind, 500);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task RealMutations_ShouldRollbackAtConsumedFailureStages(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Mutations)
        {
            var stages = new List<string> { "repo.QueryQuestionAsync", "ledger.CreateRequestSnapshot", "ledger.BeginAsync", "ledger.CompleteSuccessAsync" };
            stages.Add(operation switch
            {
                "answer" => "repo.InsertAnswerAsync", "edit" or "restore" => "repo.UpdateAnswerContentAsync",
                "delete" => "repo.SoftDeleteAnswerAsync", _ => "repo.ChangeAcceptanceAsync"
            });
            if (operation is "answer" or "edit" or "restore") stages.Add("repo.BindAnswerAttachmentsAsync");
            if (operation is "answer" or "accept" or "revoke") stages.Add("outbox.AddAsync");
            if (operation is "answer" or "edit" or "restore" or "delete") stages.Add("avatar");
            foreach (var stage in stages)
            {
                using var f = new Fixture(operation) { FailedStage = stage, Failure = new UnsafeArgument() };
                var result = await InvokeApiAsync(f.CallAsync, 400);
                Assert.Equal(f.Failure.Message, result!.MessageInfo);
                Assert.Contains(stage, f.Steps);
                f.AssertRolledBack();
                capture.AssertSingle("question.request_failed", "argument");
                capture.Clear();
            }
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task RealLateFailures_ShouldPreservePropagationAndRollbackOutboxAndLedger(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Mutations)
        foreach (var (failure, status, kind) in new (Exception, int, string?)[]
        {
            (new IOException(Secret), 500, "io"), (new BusinessException(Secret, 503, "Service.Unavailable", "Errors.Unavailable"), 503, "other"),
            (new BusinessException(Secret, 409, "Service.Conflict", "Errors.Conflict"), 409, null),
            (new AggregateException(new UnsafeArgument()), 400, "argument")
        })
        {
            using var f = new Fixture(operation)
            {
                FailedStage = operation is "answer" or "accept" or "revoke" ? "outbox.AddAsync" : "avatar",
                Failure = failure, FailAfterOutboxWrite = true
            };
            await InvokeApiAsync(f.CallAsync, status);
            f.AssertRolledBack();
            if (kind == null) capture.AssertQuiet();
            else capture.AssertSingle(status == 400 ? "question.request_failed" : "http.failed", kind, status == 400 ? null : status);
            capture.Clear();
        }
        using var direct = new Fixture("answer") { FailedStage = "ledger.CompleteSuccessAsync", Failure = new UnsafeArgument() };
        var caught = await Assert.ThrowsAsync<UnsafeArgument>(() => direct.Service.CreateAnswerAsync(0, PostId.ToString(), Content, UserId, Secret, Secret));
        Assert.Same(direct.Failure, caught);
        direct.AssertRolledBack();
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task NormalRejectionsAndReadContracts_ShouldStayQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in new[] { "answer", "edit" })
        foreach (var content in new[] { " ", new string('x', 20001) })
        {
            using var f = new Fixture(operation) { RequestContent = content };
            var exception = await Assert.ThrowsAsync<ForumAnswerContentValidationException>(() => operation == "answer"
                ? f.Service.CreateAnswerAsync(0, PostId.ToString(), content, UserId, Secret, Secret)
                : f.Service.UpdateAnswerAsync(0, AnswerPublicId, content, 2, UserId, Secret, Secret));
            Assert.Equal("content", exception.ParamName);
            Assert.Equal(new ArgumentException(content.Length > 20000 ? "回答内容不能超过 20000 个字符" : "回答内容不能为空", "content").Message, exception.Message);
            await InvokeApiAsync(f.CallAsync, 400);
            f.AssertRolledBack();
        }
        foreach (var scenario in new[] { "missing-question", "missing-answer", "denied", "accepted", "cas", "invalid-key", "attachment", "revision", "incomplete" })
        {
            using var f = new Fixture(scenario is "revision" or "incomplete" ? "restore" : "edit");
            var status = 409;
            switch (scenario)
            {
                case "missing-question": f.Db.Deleteable<PostQuestion>().ExecuteCommand(); status = 404; break;
                case "missing-answer": f.Db.Deleteable<PostAnswer>().ExecuteCommand(); status = 404; break;
                case "denied": f.Db.Updateable<PostAnswer>().Where(a => a.Id == AnswerId).SetColumns(a => a.AuthorId == OwnerId).ExecuteCommand(); status = 403; break;
                case "accepted": f.Db.Updateable<PostAnswer>().Where(a => a.Id == AnswerId).SetColumns(a => a.IsAccepted == true).ExecuteCommand(); break;
                case "cas": f.ExpectedContentRevision = 9; break;
                case "invalid-key": f.SubmissionId = new string('x', 81); status = 400; break;
                case "attachment": f.Db.Updateable<Attachment>().Where(a => a.Id == 401).SetColumns(a => a.IsEnabled == false).ExecuteCommand(); break;
                case "revision": f.Db.Deleteable<PostAnswerContentRevision>().ExecuteCommand(); status = 404; break;
                case "incomplete": f.Db.Updateable<PostAnswerContentRevision>().Where(r => r.Id == 301).SetColumns(r => r.IntegrityStatus == ForumContentRevisionIntegrityStatuses.LegacyIncomplete).ExecuteCommand(); break;
            }
            var result = await InvokeApiAsync(f.CallAsync, status);
            var code = scenario switch
            {
                "missing-question" => ForumQuestionErrorCodes.NotFound,
                "missing-answer" or "revision" => ForumQuestionErrorCodes.AnswerNotFound,
                "denied" => ForumQuestionErrorCodes.AccessDenied,
                "accepted" => ForumQuestionErrorCodes.AcceptedAnswerLocked,
                "attachment" => ForumQuestionErrorCodes.AttachmentUnavailable,
                "incomplete" => ForumQuestionErrorCodes.RevisionIncomplete,
                _ => ForumQuestionErrorCodes.Conflict
            };
            Assert.Equal(code, result!.Code);
            Assert.Equal(ForumQuestionErrorCodes.ResolveMessageKey(code), result.MessageKey);
            Assert.Empty(f.Db.Queryable<ContentSubmissionRecord>().ToList());
            Assert.Empty(f.Db.Queryable<ReliableOutboxMessage>().ToList());
        }
        using var reads = new Fixture("page");
        var page = Assert.IsType<PostAnswerPageVo>((await InvokeApiAsync(reads.CallAsync, 200))!.ResponseData);
        Assert.Equal(1, page.VoTotal);
        Assert.True(Assert.Single(page.VoItems).VoCanEdit);
        var revisions = await reads.Service.GetAnswerRevisionsAsync(0, AnswerPublicId, UserId);
        Assert.True(Assert.Single(revisions.VoItems).VoCanRestore);
        Assert.Equal("restored " + Content, (await reads.Service.GetAnswerRevisionAsync(0, AnswerPublicId, 1, UserId)).VoContent);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SuccessfulMutationsAndReplays_ShouldKeepPersistedFactsAndPayloads(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Mutations)
        {
            using var f = new Fixture(operation);
            var before = DateTime.UtcNow.AddSeconds(-1);
            var result = await InvokeApiAsync(f.CallAsync, 200);
            Assert.True(result!.IsSuccess);
            var ledger = Assert.Single(f.Db.Queryable<ContentSubmissionRecord>().ToList());
            Assert.Equal(ContentSubmissionStatuses.Succeeded, ledger.Status);
            var question = f.Db.Queryable<PostQuestion>().Single();
            var answer = f.Db.Queryable<PostAnswer>().InSingle(AnswerId);
            var revisions = f.Db.Queryable<PostAnswerContentRevision>().ToList();
            var outbox = f.Db.Queryable<ReliableOutboxMessage>().ToList();
            if (operation == "answer")
            {
                var created = Assert.IsType<PostAnswerMutationVo>(result.ResponseData).VoAnswer;
                Assert.Equal(Content, created.VoContent);
                Assert.Equal(2, question.AnswerCount);
                Assert.Equal(created.VoAnswerId, ledger.ResultId);
                Assert.Equal(created.VoPublicId, ledger.ResultPublicId);
                Assert.InRange(created.VoCreateTime, before, DateTime.UtcNow);
                Assert.Equal(DateTimeKind.Utc, created.VoCreateTime.Kind);
            }
            else if (operation is "edit" or "restore")
            {
                Assert.Equal(operation == "edit" ? Content : "restored " + Content, answer.Content);
                Assert.Equal(3, answer.ContentRevision);
                Assert.Equal(1, answer.EditCount);
                Assert.Equal(UserId, answer.ModifyId);
                Assert.Equal(Secret, answer.ModifyBy);
                var added = Assert.Single(revisions, r => r.RevisionNumber == 3);
                Assert.Equal(operation == "edit" ? ForumContentRevisionSourceTypes.Edit : ForumContentRevisionSourceTypes.Restore, added.SourceType);
                Assert.Equal(operation == "restore" ? 301L : (long?)null, added.RestoredFromRevisionId);
                Assert.Equal("restored " + Content, Assert.Single(revisions, r => r.Id == 301).Content);
            }
            else if (operation == "delete")
            {
                Assert.True(answer.IsDeleted);
                Assert.Equal(Secret, answer.DeletedBy);
                Assert.Equal(UserId, answer.ModifyId);
                Assert.NotNull(answer.DeletedAt);
                Assert.Equal(0, question.AnswerCount);
            }
            else
            {
                Assert.Equal(operation == "accept", question.IsSolved);
                Assert.Equal(operation == "accept", answer.IsAccepted);
                Assert.Equal(2, question.AcceptanceRevision);
                Assert.Equal(operation == "accept" ? AnswerId : (long?)null, question.AcceptedAnswerId);
                var acceptance = f.Db.Queryable<PostAnswerAcceptanceEvent>().Single();
                Assert.Equal(OwnerId, acceptance.OperatorId);
                Assert.Equal(operation == "accept" ? PostAnswerAcceptanceEventTypes.Accepted : PostAnswerAcceptanceEventTypes.Revoked, acceptance.EventType);
            }
            if (operation is "answer" or "edit" or "restore")
            {
                var reference = f.Db.Queryable<ForumContentRevisionAttachment>().Single();
                Assert.Equal(401, reference.AttachmentId);
                Assert.Equal(reference.TargetId, f.Db.Queryable<Attachment>().Single().BusinessId);
                Assert.Equal(UserId, reference.CreateId);
            }
            if (operation is "answer" or "accept" or "revoke")
            {
                var item = Assert.Single(outbox);
                var notification = JsonSerializer.Deserialize<NotificationRequestedTaskPayload>(item.PayloadJson)!.Notification;
                Assert.Equal(operation == "answer" ? OwnerId : UserId, Assert.Single(notification.ReceiverUserIds));
                Assert.Equal(PostId, notification.BusinessId);
                Assert.Equal(Secret, notification.Content);
                Assert.Equal(Secret, notification.TriggerName);
                Assert.Equal(notification.Target!.AnswerId!.Value.ToString(), item.AggregateId);
                Assert.Equal("task:" + notification.BusinessKey, item.IdempotencyKey);
                Assert.Equal(notification.OccurredAtUtc, item.OccurredAtUtc);
                Assert.Equal(ReliableTaskTypes.NotificationRequested, item.TaskType);
                Assert.Equal(ReliableOutboxStatuses.Pending, item.Status);
                Assert.InRange(item.OccurredAtUtc, before, DateTime.UtcNow);
            }
            else Assert.Empty(outbox);
            var counts = f.Counts();
            await InvokeApiAsync(f.CallAsync, 200);
            Assert.Equal(counts, f.Counts());
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertQuiet();
        }
    }

    private sealed class UnsafeArgument() : ArgumentException(Secret)
    {
        public override string ToString() => throw new InvalidOperationException("日志不得渲染异常");
    }
    private sealed class UnreadableArgument : ArgumentException
    {
        public override string Message => throw new IOException(Secret);
    }
    private sealed class HostileFailure : Exception
    {
        public override string Message => throw new InvalidOperationException("不可读取异常原文");
        public override string ToString() => throw new InvalidOperationException("不可渲染异常");
    }
    private sealed class HostileValue
    {
        public override string ToString() => throw new InvalidOperationException("不可渲染消息参数");
    }
    private sealed class ThrowingInterceptor(Exception failure) : IInterceptor
    {
        public void Intercept(Castle.DynamicProxy.IInvocation invocation) => throw failure;
    }

    private sealed class Fixture : IDisposable
    {
        public SqlSugarScope Db { get; }
        public UnitOfWorkManage Unit { get; }
        public IForumQuestionService Service { get; }
        public QuestionController Controller { get; }
        public List<string> Steps { get; } = [];
        public string? FailedStage { get; set; }
        public Exception Failure { get; set; } = new UnsafeArgument();
        public bool FailAfterOutboxWrite { get; set; }
        public string RequestContent { get; set; } = Content;
        public string SubmissionId { get; set; } = Secret;
        public int ExpectedContentRevision { get; set; } = 2;
        private readonly string _operation;

        public Fixture(string operation)
        {
            _operation = operation;
            new ServiceCollection().ConfigureApplication();
            Db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", DbType = DbType.Sqlite, ConnectionString = "Data Source=:memory:",
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            Db.CodeFirst.InitTables<Post, PostQuestion, PostAnswer, PostAnswerContentRevision>();
            Db.CodeFirst.InitTables<PostAnswerAcceptanceEvent, Attachment, ForumContentRevisionAttachment, ContentSubmissionRecord, ReliableOutboxMessage>();
            Unit = new UnitOfWorkManage(Db, NullLogger<UnitOfWorkManage>.Instance);
            Db.Insertable(new Post(Secret, Secret)
            {
                Id = PostId, PublicId = "pst_" + Secret, AuthorId = OwnerId, AuthorName = Secret,
                TenantId = 0, IsPublished = true, PublishTime = DateTime.UtcNow
            }).ExecuteCommand();
            Db.Insertable(new PostQuestion
            {
                Id = QuestionId, PostId = PostId, TenantId = 0, AnswerCount = 1, AcceptanceRevision = 1,
                IsSolved = operation == "revoke", AcceptedAnswerId = operation == "revoke" ? AnswerId : null,
                AcceptedAnswerContentRevision = operation == "revoke" ? 2 : null
            }).ExecuteCommand();
            Db.Insertable(new PostAnswer
            {
                Id = AnswerId, PublicId = AnswerPublicId, PostId = PostId, AuthorId = UserId, AuthorName = Secret,
                TenantId = 0, Content = "current " + Secret, ContentRevision = 2, IsAccepted = operation == "revoke"
            }).ExecuteCommand();
            Db.Insertable(new PostAnswerContentRevision
            {
                Id = 301, TenantId = 0, PostId = PostId, AnswerId = AnswerId, Content = "restored " + Content,
                RevisionNumber = 1, SourceType = ForumContentRevisionSourceTypes.Baseline,
                IntegrityStatus = ForumContentRevisionIntegrityStatuses.Complete, EditorId = UserId, EditorName = Secret
            }).ExecuteCommand();
            Db.Insertable(new Attachment { Id = 401, UploaderId = UserId, TenantId = 0, IsEnabled = true }).ExecuteCommand();
            var attachments = new Mock<IAttachmentService>();
            attachments.Setup(a => a.GetLatestAvatarAssetMapAsync(It.IsAny<IReadOnlyCollection<long>>()))
                .ReturnsAsync(() => { Step("avatar"); return new Dictionary<long, AttachmentAssetDto>(); });
            var service = new ForumQuestionService(
                Wrap<IForumQuestionRepository>(new ForumQuestionRepository(Unit), "repo"),
                Wrap<IContentSubmissionService>(new ContentSubmissionService(new BaseRepository<ContentSubmissionRecord>(Unit), Unit), "ledger"),
                Mock.Of<IUserInteractionPolicyService>(),
                Wrap<IReliableOutboxService>(new ReliableOutboxService(new ReliableOutboxRepository(Db)), "outbox"), attachments.Object);
            Service = new ProxyGenerator().CreateInterfaceProxyWithTarget<IForumQuestionService>(service, new TranAop(Unit));
            Controller = CreateController(Service, operation is "accept" or "revoke" ? OwnerId : UserId);
        }
        private T Wrap<T>(T target, string name) where T : class =>
            new ProxyGenerator().CreateInterfaceProxyWithTarget(target, new StageInterceptor(this, name));
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
                if (stage == fixture.FailedStage && stage == "outbox.AddAsync" && fixture.FailAfterOutboxWrite)
                {
                    fixture.Steps.Add(stage);
                    invocation.Proceed();
                    invocation.ReturnValue = FailAfterWriteAsync((Task<long>)invocation.ReturnValue);
                    return;
                }
                fixture.Step(stage);
                invocation.Proceed();
            }
            private async Task<long> FailAfterWriteAsync(Task<long> write)
            {
                await write;
                throw fixture.Failure;
            }
        }
        public Task<MessageModel> CallAsync() => CallControllerAsync(Controller, _operation, RequestContent, SubmissionId, ExpectedContentRevision);
        public int[] Counts() =>
        [
            Db.Queryable<PostAnswer>().Count(), Db.Queryable<PostAnswerContentRevision>().Count(),
            Db.Queryable<PostAnswerAcceptanceEvent>().Count(), Db.Queryable<ForumContentRevisionAttachment>().Count(),
            Db.Queryable<ContentSubmissionRecord>().Count(), Db.Queryable<ReliableOutboxMessage>().Count()
        ];
        public void AssertRolledBack()
        {
            Assert.Equal(new[] { 1, 1, 0, 0, 0, 0 }, Counts());
            var answer = Db.Queryable<PostAnswer>().InSingle(AnswerId);
            Assert.Equal("current " + Secret, answer.Content);
            Assert.Equal(2, answer.ContentRevision);
            Assert.False(answer.IsDeleted);
            Assert.Equal(_operation == "revoke", answer.IsAccepted);
            var question = Db.Queryable<PostQuestion>().Single();
            Assert.Equal(1, question.AnswerCount);
            Assert.Equal(1, question.AcceptanceRevision);
            Assert.Equal(_operation == "revoke", question.IsSolved);
            Assert.Equal(_operation == "revoke" ? AnswerId : (long?)null, question.AcceptedAnswerId);
            Assert.Null(Db.Queryable<Attachment>().Single().BusinessId);
            Assert.Equal(0, Unit.TranCount);
        }
        public void Dispose() => Db.Dispose();
    }

    private static QuestionController CreateController(IForumQuestionService service, long userId)
    {
        var current = new Mock<ICurrentUserAccessor>();
        current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = userId, UserName = Secret, TenantId = 0, IsAuthenticated = true });
        var moderation = new Mock<IContentModerationService>();
        moderation.Setup(m => m.GetPublishPermissionAsync(It.IsAny<long>())).ReturnsAsync(new ContentModerationPermissionVo { VoCanPublish = true });
        return new QuestionController(Mock.Of<IPostService>(), moderation.Object, current.Object, service);
    }

    private static Task<MessageModel> CallControllerAsync(QuestionController controller, string operation, string content = Content, string submissionId = Secret, int expectedRevision = 2) => operation switch
    {
        "answer" => controller.Answer(new CreateAnswerDto { PostIdentifier = PostId.ToString(), Content = content, ClientSubmissionId = submissionId }),
        "edit" => controller.Edit(new UpdatePostAnswerDto { AnswerPublicId = AnswerPublicId, Content = content, ExpectedContentRevision = expectedRevision, ClientSubmissionId = submissionId }),
        "delete" => controller.Delete(new DeletePostAnswerDto { AnswerPublicId = AnswerPublicId, ExpectedContentRevision = expectedRevision, ClientSubmissionId = submissionId }),
        "restore" => controller.Restore(new RestorePostAnswerRevisionDto { AnswerPublicId = AnswerPublicId, RevisionNumber = 1, ExpectedContentRevision = expectedRevision, ClientSubmissionId = submissionId }),
        "accept" => controller.Accept(new ChangePostAnswerAcceptanceDto { PostIdentifier = PostId.ToString(), AnswerPublicId = AnswerPublicId, ExpectedAcceptanceRevision = 1, ClientSubmissionId = submissionId }),
        "revoke" => controller.Revoke(new RevokePostAnswerAcceptanceDto { PostIdentifier = PostId.ToString(), ExpectedAcceptanceRevision = 1, ClientSubmissionId = submissionId }),
        "page" => controller.Page(new GetPostAnswerPageDto { PostIdentifier = PostId.ToString() }),
        "revisions" => controller.Revisions(AnswerPublicId),
        "revision" => controller.Revision(AnswerPublicId, 1),
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
        context.Request.Path = "/api/v1/Question/Answer";
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
            foreach (var forbidden in new[] { Secret, PostId.ToString(), UserId.ToString(), OwnerId.ToString(), AnswerId.ToString(), AnswerPublicId, "runtime.unclassified", "System.IO.IOException", "System.ArgumentException", "System.InvalidOperationException", "PostAnswerContentRevision", "ClientSubmissionId", "RequestDigest", "RequestSummary" })
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
