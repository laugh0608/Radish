using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Hubs;
using Radish.Api.Services;
using Radish.Common.CoreTool;
using Radish.Common.HttpContextTool;
using Radish.Common.LogTool;
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
public sealed class ContentSubmissionLoggingTests
{
    private const string Secret = "SUBMISSION_PRIVATE_SENTINEL";
    private const long RecordId = 812345679;
    private const long UserId = 623456789;
    private const long TargetId = 923456781;
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ConflictResolution_ShouldLogOnlyAfterReadingOrResettingExistingResult(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var scenario in new[] { "pending", "succeeded", "failed", "expired", "different-digest", "invalid-state", "fingerprint-pending", "fingerprint-succeeded" })
        {
            var f = new Fixture();
            f.Existing.Status = scenario.Contains("succeeded", StringComparison.Ordinal) ? ContentSubmissionStatuses.Succeeded
                : scenario == "failed" ? ContentSubmissionStatuses.Failed : scenario == "invalid-state" ? Secret : ContentSubmissionStatuses.Pending;
            if (scenario == "expired") f.Existing.ExpiresAt = DateTime.Now.AddMinutes(-1);
            f.DifferentDigest = scenario == "different-digest";
            var request = Request(scenario.StartsWith("fingerprint", StringComparison.Ordinal) ? null : Secret);
            var before = DateTime.Now;
            var result = await f.Service.BeginAsync(request);
            var expected = scenario switch
            {
                "failed" or "expired" => ContentSubmissionBeginStatus.Started,
                "succeeded" => ContentSubmissionBeginStatus.Succeeded,
                "fingerprint-succeeded" => ContentSubmissionBeginStatus.DuplicateContent,
                "different-digest" or "invalid-state" => ContentSubmissionBeginStatus.Conflict,
                _ => ContentSubmissionBeginStatus.Processing
            };
            Assert.Equal(expected, result.Status);
            Assert.Equal(new[] { "lookup", "savepoint", "insert", "rollback-savepoint", "lookup" }, f.Steps.Take(5));
            if (expected == ContentSubmissionBeginStatus.Started)
            {
                Assert.Equal("reset", f.Steps[^1]);
                Assert.Equal(ContentSubmissionStatuses.Pending, f.Existing.Status);
                Assert.Equal(request.RequestDigest, f.Existing.RequestDigest);
                Assert.Equal(request.RequestSummary, f.Existing.RequestSummary);
                Assert.Equal(request.ContentFingerprint, f.Existing.ContentFingerprint);
                Assert.Null(f.Existing.ResultId);
                Assert.Null(f.Existing.ResultType);
                Assert.Null(f.Existing.ResultPublicId);
                Assert.Null(f.Existing.ErrorCode);
                Assert.Null(f.Existing.ErrorMessage);
                Assert.Null(f.Existing.CompleteTime);
                Assert.Equal("System", f.Existing.ModifyBy);
                Assert.InRange(f.Existing.ExpiresAt, before.AddHours(24), DateTime.Now.AddHours(24));
            }
            if (expected is ContentSubmissionBeginStatus.Succeeded or ContentSubmissionBeginStatus.DuplicateContent)
            {
                Assert.Equal(TargetId, result.ResultId);
                Assert.Equal(Secret, result.ResultPublicId);
                Assert.Equal(ContentSubmissionResultTypes.Comment, result.ResultType);
            }
            f.Repository.Verify(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>()), Times.Once);
            capture.AssertEvents(("content_submission.conflict_resolved", "Warning", "database"));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task FailedRecovery_ShouldNotClaimResolutionOrReplaceOriginalFailure(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "not-found", "read", "reset", "reset-not-persisted", "non-unique" })
        {
            var f = new Fixture { FailureStage = stage };
            if (stage.StartsWith("reset", StringComparison.Ordinal)) f.Existing.Status = ContentSubmissionStatuses.Failed;
            if (stage == "non-unique") f.InsertFailure = new IOException(Secret);
            var observed = await Record.ExceptionAsync(() => f.Service.BeginAsync(Request()));
            if (stage is "not-found" or "non-unique") Assert.Same(f.InsertFailure, observed);
            else if (stage == "reset-not-persisted") Assert.IsType<ContentSubmissionConsistencyException>(observed);
            else Assert.Same(f.RecoveryFailure, observed);
            capture.AssertQuiet();

            var api = new Fixture { FailureStage = stage };
            if (stage.StartsWith("reset", StringComparison.Ordinal)) api.Existing.Status = ContentSubmissionStatuses.Failed;
            if (stage == "non-unique") api.InsertFailure = new IOException(Secret);
            await InvokeApiAsync(() => api.Service.BeginAsync(Request()), 500);
            capture.AssertEvents(("http.failed", "Error", stage == "not-found" ? "database" : stage == "reset-not-persisted" ? "other" : "io"));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task NormalBeginReplayAndCompletion_ShouldStayQuietAndKeepRecordAudit(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture { ConflictOnInsert = false };
        var before = DateTime.Now;
        var created = await f.Service.BeginAsync(Request());
        Assert.Equal(ContentSubmissionBeginStatus.Started, created.Status);
        var record = Assert.Single(f.Records);
        Assert.Equal(RecordId, record.Id);
        Assert.Equal(0, record.TenantId);
        Assert.Equal(UserId, record.UserId);
        Assert.Equal(Secret, record.ClientSubmissionId);
        Assert.Equal(Secret, record.RequestSummary);
        Assert.Equal($"User_{UserId}", record.CreateBy);
        Assert.Equal(UserId, record.CreateId);
        Assert.Equal(record.CreateTime.AddHours(24), record.ExpiresAt);
        Assert.InRange(record.CreateTime, before, DateTime.Now);
        await f.Service.CompleteSuccessAsync(new ContentSubmissionCompletionRequest
        {
            RecordId = RecordId, ResultType = ContentSubmissionResultTypes.Comment, ResultId = TargetId, ResultPublicId = " " + Secret + " "
        });
        Assert.Equal(ContentSubmissionStatuses.Succeeded, record.Status);
        Assert.Equal(Secret, record.ResultPublicId);
        Assert.Equal("System", record.ModifyBy);
        Assert.Equal(0, record.ModifyId);
        Assert.InRange(record.CompleteTime!.Value, before, DateTime.Now);
        Assert.Equal(ContentSubmissionBeginStatus.Succeeded, (await f.Service.BeginAsync(Request())).Status);
        Assert.Equal(ContentSubmissionBeginStatus.InvalidKey, (await f.Service.BeginAsync(Request("invalid/key"))).Status);
        f.Repository.Verify(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CommentConsumer_ShouldKeepResponseTransactionAndDistinctLaterFailures(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var scenario in new[] { "pending", "succeeded", "started", "later-failure", "argument-read", "io-read" })
        {
            var f = new Fixture();
            f.Existing.Status = scenario == "succeeded" ? ContentSubmissionStatuses.Succeeded
                : scenario is "started" or "later-failure" ? ContentSubmissionStatuses.Failed : ContentSubmissionStatuses.Pending;
            if (scenario.EndsWith("read", StringComparison.Ordinal))
            {
                f.FailureStage = "read";
                f.RecoveryFailure = scenario == "argument-read" ? new ArgumentException(Secret) : new IOException(Secret);
            }
            var comments = new Mock<ICommentService>();
            comments.Setup(c => c.AddCommentAsync(It.IsAny<Comment>())).ReturnsAsync((TargetId, CommentHighlightRecheckResultVo.NoChange(TargetId, null, 1)));
            if (scenario == "later-failure") comments.Setup(c => c.AddCommentAsync(It.IsAny<Comment>())).ThrowsAsync(new IOException(Secret));
            var writes = new ProxyGenerator().CreateInterfaceProxyWithTarget<IForumContentWriteService>(
                new ForumContentWriteService(f.Service, Mock.Of<IPostService>(), comments.Object), new TranAop(f.Unit.Object));
            var permission = new Mock<IContentModerationService>();
            permission.Setup(p => p.GetPublishPermissionAsync(UserId)).ReturnsAsync(new ContentModerationPermissionVo { VoCanPublish = true });
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = UserId, UserName = Secret, TenantId = 0 });
            var push = new CommentRealtimePushService(Mock.Of<IHubContext<CommentHub>>(), NullLogger<CommentRealtimePushService>.Instance);
            var controller = new CommentController(comments.Object, Mock.Of<IPostService>(), Mock.Of<IUserService>(), permission.Object, current.Object, push, writes);
            MessageModel? result = null;
            var apiStatus = scenario == "pending" ? 409 : scenario is "later-failure" or "io-read" ? 500 : 200;
            await InvokeApiAsync(async () => result = await controller.Create(new CreateCommentDto { PostId = TargetId, Content = Secret, ClientSubmissionId = Secret }), apiStatus);
            if (scenario == "argument-read")
            {
                Assert.Equal(400, result!.StatusCode);
                Assert.Equal(Secret, result.MessageInfo);
                capture.AssertEvents(("comment.create_failed", "Error", "argument"));
            }
            else if (scenario == "io-read") capture.AssertEvents(("http.failed", "Error", "io"));
            else if (scenario == "later-failure") capture.AssertEvents(("content_submission.conflict_resolved", "Warning", "database"), ("http.failed", "Error", "io"));
            else capture.AssertEvents(("content_submission.conflict_resolved", "Warning", "database"));
            Assert.Contains(scenario is "succeeded" or "started" ? "commit" : "rollback", f.Steps);
            comments.Verify(c => c.AddCommentAsync(It.IsAny<Comment>()), scenario is "started" or "later-failure" ? Times.Once : Times.Never);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SqliteConflict_ShouldRollbackOnlySavepointAndKeepOuterTransactionUsable(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        new ServiceCollection().ConfigureApplication();
        foreach (var commit in new[] { true, false })
        {
            using var db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", DbType = DbType.Sqlite, ConnectionString = "Data Source=:memory:",
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            db.CodeFirst.InitTables<ContentSubmissionRecord>();
            await db.Ado.ExecuteCommandAsync("CREATE TABLE submission_probe (id INTEGER PRIMARY KEY)");
            await db.Insertable(Existing()).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            var unit = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
            var realRepository = new BaseRepository<ContentSubmissionRecord>(unit);
            var repository = new Mock<IBaseRepository<ContentSubmissionRecord>>(MockBehavior.Strict);
            var queries = 0;
            repository.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ContentSubmissionRecord, bool>>?>()))
                .Returns((Expression<Func<ContentSubmissionRecord, bool>>? predicate) => ++queries == 1
                    ? Task.FromResult<ContentSubmissionRecord?>(null) : realRepository.QueryFirstAsync(predicate));
            repository.Setup(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>()))
                .Returns((ContentSubmissionRecord record) => realRepository.AddAsync(record));
            var commands = new List<string>();
            db.Aop.OnLogExecuting = (sql, _) => commands.Add(sql);
            var service = new ContentSubmissionService(repository.Object, unit);
            unit.BeginTran();
            await db.Ado.ExecuteCommandAsync("INSERT INTO submission_probe (id) VALUES (1)");
            var result = await service.BeginAsync(Request());
            Assert.Equal(ContentSubmissionBeginStatus.Processing, result.Status);
            Assert.Equal(RecordId, result.RecordId);
            Assert.Equal(1, unit.TranCount);
            Assert.Equal(1, db.Queryable<ContentSubmissionRecord>().Count());
            Assert.Equal(1, Convert.ToInt32(await db.Ado.GetScalarAsync("SELECT COUNT(*) FROM submission_probe")));
            await db.Ado.ExecuteCommandAsync("INSERT INTO submission_probe (id) VALUES (2)");
            var savepoint = commands.FindIndex(sql => sql.StartsWith("SAVEPOINT ", StringComparison.Ordinal));
            var rollback = commands.FindIndex(sql => sql.StartsWith("ROLLBACK TO SAVEPOINT ", StringComparison.Ordinal));
            var release = commands.FindIndex(sql => sql.StartsWith("RELEASE SAVEPOINT ", StringComparison.Ordinal));
            Assert.True(savepoint >= 0 && rollback > savepoint && release > rollback);
            if (commit) unit.CommitTran();
            else unit.RollbackTran();
            Assert.Equal(0, unit.TranCount);
            Assert.Equal(commit ? 2 : 0, Convert.ToInt32(await db.Ado.GetScalarAsync("SELECT COUNT(*) FROM submission_probe")));
            Assert.Equal(1, db.Queryable<ContentSubmissionRecord>().Count());
            capture.AssertEvents(("content_submission.conflict_resolved", "Warning", "database"));
            capture.Clear();
        }
    }

    private static ContentSubmissionBeginRequest Request(string? key = Secret) => new()
    {
        TenantId = -1, UserId = UserId, OperationType = ContentSubmissionOperationTypes.ForumCommentCreate,
        ClientSubmissionId = key, TargetType = " Post ", TargetId = TargetId, RequestDigest = Secret,
        RequestSummary = Secret, ContentFingerprint = Secret, DuplicateWindowSeconds = 60
    };

    private static ContentSubmissionRecord Existing() => new()
    {
        Id = RecordId, TenantId = 0, UserId = UserId, OperationType = ContentSubmissionOperationTypes.ForumCommentCreate,
        ClientSubmissionId = Secret, TargetType = "Post", TargetId = TargetId, RequestDigest = Secret,
        RequestSummary = Secret, ContentFingerprint = Secret, Status = ContentSubmissionStatuses.Pending,
        ResultType = ContentSubmissionResultTypes.Comment, ResultId = TargetId, ResultPublicId = Secret,
        CreateTime = DateTime.Now, ExpiresAt = DateTime.Now.AddHours(24), ErrorCode = Secret, ErrorMessage = Secret
    };

    private sealed class UniqueFailure() : DbException("23505 duplicate key " + Secret)
    {
        public override string ToString() => throw new InvalidOperationException("日志不得渲染原始异常");
    }

    private sealed class Fixture
    {
        public Mock<IBaseRepository<ContentSubmissionRecord>> Repository { get; } = new(MockBehavior.Strict);
        public Mock<IUnitOfWorkManage> Unit { get; } = new(MockBehavior.Strict);
        public ContentSubmissionService Service { get; }
        public List<ContentSubmissionRecord> Records { get; } = [];
        public ContentSubmissionRecord Existing { get; } = ContentSubmissionLoggingTests.Existing();
        public List<string> Steps { get; } = [];
        public bool ConflictOnInsert { get; set; } = true;
        public bool DifferentDigest { get; set; }
        public string? FailureStage { get; set; }
        public Exception InsertFailure { get; set; } = new UniqueFailure();
        public Exception RecoveryFailure { get; set; } = new IOException(Secret);
        private bool _afterRollback;
        public Fixture()
        {
            Repository.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ContentSubmissionRecord, bool>>?>()))
                .ReturnsAsync((Expression<Func<ContentSubmissionRecord, bool>>? predicate) =>
                {
                    Steps.Add("lookup");
                    if (_afterRollback && FailureStage == "read") throw RecoveryFailure;
                    return Records.FirstOrDefault(record => predicate == null || predicate.Compile()(record));
                });
            Repository.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<ContentSubmissionRecord, bool>>?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<Expression<Func<ContentSubmissionRecord, object>>?>(), It.IsAny<OrderByType>()))
                .ReturnsAsync((new List<ContentSubmissionRecord>(), 0));
            Repository.Setup(r => r.QueryByIdAsync(RecordId)).ReturnsAsync(() => Records.SingleOrDefault(record => record.Id == RecordId));
            Repository.Setup(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>())).ReturnsAsync((ContentSubmissionRecord pending) =>
            {
                Steps.Add("insert");
                if (ConflictOnInsert)
                {
                    Existing.RequestDigest = DifferentDigest ? Secret + "-different" : pending.RequestDigest;
                    Existing.ContentFingerprint = pending.ContentFingerprint;
                    throw InsertFailure;
                }
                pending.Id = RecordId;
                Records.Add(pending);
                return RecordId;
            });
            Repository.Setup(r => r.UpdateAsync(It.IsAny<ContentSubmissionRecord>())).ReturnsAsync(() =>
            {
                Steps.Add("reset");
                if (FailureStage == "reset") throw RecoveryFailure;
                return FailureStage != "reset-not-persisted";
            });
            Unit.Setup(u => u.ExecuteInSavepointAsync(It.IsAny<Func<Task<long>>>())).Returns(async (Func<Task<long>> operation) =>
            {
                Steps.Add("savepoint");
                try { return await operation(); }
                catch
                {
                    Steps.Add("rollback-savepoint");
                    _afterRollback = true;
                    if (FailureStage != "not-found") Records.Add(Existing);
                    throw;
                }
            });
            Unit.SetupGet(u => u.TranCount).Returns(0);
            Unit.Setup(u => u.BeginTran(It.IsAny<MethodInfo>())).Callback(() => Steps.Add("begin"));
            Unit.Setup(u => u.CommitTran(It.IsAny<MethodInfo>())).Callback(() => Steps.Add("commit"));
            Unit.Setup(u => u.RollbackTran(It.IsAny<MethodInfo>())).Callback(() => Steps.Add("rollback"));
            Service = new ContentSubmissionService(Repository.Object, Unit.Object);
        }
    }

    private static async Task InvokeApiAsync(Func<Task> action, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        app.Run(async _ => await action());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Comment/Create";
        context.Request.QueryString = new QueryString("?query=" + Secret);
        using var output = new MemoryStream();
        context.Response.Body = output;
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
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
        public void AssertEvents(params (string Code, string Level, string Kind)[] expected)
        {
            var text = _output.ToString();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(expected.Length, lines.Length);
            for (var index = 0; index < expected.Length; index++)
            {
                var value = expected[index];
                Assert.Contains(value.Code, lines[index]);
                Assert.Contains(value.Level, lines[index]);
                Assert.Contains(value.Kind, lines[index]);
                if (_mode != null)
                {
                    using var json = JsonDocument.Parse(lines[index]);
                    var root = json.RootElement;
                    Assert.Equal(_mode, root.GetProperty("mode").GetString());
                    Assert.Equal(value.Code, root.GetProperty("eventCode").GetString());
                    Assert.Equal(value.Level, root.GetProperty("level").GetString());
                    var properties = root.GetProperty("properties");
                    Assert.Equal(value.Kind, properties.GetProperty("failureKind").GetString());
                    Assert.Equal(value.Code == "http.failed" ? 2 : 1, properties.EnumerateObject().Count());
                }
            }
            foreach (var forbidden in new[] { Secret, RecordId.ToString(), UserId.ToString(), TargetId.ToString(), "runtime.unclassified", "23505", "duplicate key", "UNIQUE constraint", "RequestDigest", "RequestSummary", "ClientSubmissionId", "ContentSubmissionRecord", "radish_sp_" })
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
