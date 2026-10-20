using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Radish.Extension.AopExtension;
using Radish.Extension.AutoMapperExtension.CustomProfiles;
using Radish.Extension.Log;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Service.Base;
using Radish.Shared.Constants;
using Serilog;
using Serilog.Events;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class CategoryControllerLoggingTests
{
    private const string Secret = "CATEGORY_PRIVATE_SENTINEL";
    private const string Slug = "private--category-slug";
    private const long CategoryId = 912345678;
    private const long ParentId = 812345678;
    private const long UserId = 623456789;
    private const long IconId = 723456789;
    private const long CoverId = 723456788;
    private static readonly DateTime CreatedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly string[] Operations = ["create", "update"];
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ConsumedFailures_ShouldPreserveResponsesAndNotClassifyByMessage(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var failure in new InvalidOperationException[] { new UnsafeOperation(), new InvalidOperationException("父分类不存在") })
        {
            using var f = new Fixture(operation) { FailedStage = operation, Failure = failure };
            var before = f.Snapshot();
            AssertError(await InvokeApiAsync(f.CallAsync, 400, environment), 400, failure.Message);
            Assert.Equal(before, f.Snapshot());
            capture.AssertSingle("category.request_failed", "invalid-operation");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task UnconsumedAndUnreadableFailures_ShouldRemainApiOwned(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var (failure, kind) in new (Exception, string)[]
        {
            (new IOException(Secret), "io"), (new ArgumentException(Secret), "argument"),
            (new TimeoutException(Secret), "timeout"), (new OperationCanceledException(Secret), "cancelled"),
            (new HostileFailure(), "other"), (new UnreadableOperation(), "io"),
            (new AggregateException(new UnsafeOperation()), "aggregate"),
            (new AggregateException(new UnsafeOperation(), new IOException(Secret)), "aggregate")
        })
        {
            using var f = new Fixture(operation) { FailedStage = operation, Failure = failure };
            var before = f.Snapshot();
            // xUnit 的 Throws 辅助会读取 Message；这里手动捕获以验证不可读取消息的原异常传播。
            Exception? propagated = null;
            try { await f.CallAsync(); }
            catch (Exception exception) { propagated = exception; }
            Assert.NotNull(propagated);
            if (failure is UnreadableOperation) Assert.IsType<IOException>(propagated);
            else Assert.Same(failure, propagated);
            capture.AssertQuiet();
            var response = await InvokeApiAsync(f.CallAsync, 500, environment);
            Assert.Equal(ApiErrorCodes.UnexpectedError, response.Code);
            Assert.DoesNotContain(Secret, response.MessageInfo);
            Assert.Equal(before, f.Snapshot());
            capture.AssertSingle("http.failed", kind, 500);
            capture.Clear();
        }

        foreach (var operation in Operations)
        foreach (var status in new[] { 400, 403, 409, 500, 503 })
        {
            var failure = new BusinessException(Secret, status, "Private.Code", "Private.Key", Secret);
            using var f = new Fixture(operation) { FailedStage = operation, Failure = failure };
            Assert.Same(failure, await Assert.ThrowsAsync<BusinessException>(f.CallAsync));
            capture.AssertQuiet();
            var response = await InvokeApiAsync(f.CallAsync, status, environment);
            Assert.Equal(failure.Message, response.MessageInfo);
            Assert.Equal(failure.ErrorCode, response.Code);
            Assert.Equal(failure.MessageKey, response.MessageKey);
            Assert.Equal(Secret, Assert.IsType<JsonElement>(Assert.Single(response.MessageArguments!)).GetString());
            if (status >= 500) capture.AssertSingle("http.failed", "other", status);
            else capture.AssertQuiet();
            capture.Clear();
        }
        // 依赖 404 的内存中间件分支已有 TagControllerLoggingTests 独立覆盖，本批只核对原样传播。
        foreach (var operation in Operations)
        {
            var failure = new BusinessException(Secret, 404);
            using var f = new Fixture(operation) { FailedStage = operation, Failure = failure };
            Assert.Same(failure, await Assert.ThrowsAsync<BusinessException>(f.CallAsync));
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task MissingParentAndPrechecks_ShouldStayQuietAndPreserveRows(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var scenario in new[] { "missing", "deleted", "zero", "negative" })
        {
            using var f = new Fixture(operation);
            if (scenario == "missing") f.Db.Deleteable<Category>().Where(c => c.Id == ParentId).ExecuteCommand();
            if (scenario == "deleted") f.Db.Updateable<Category>().Where(c => c.Id == ParentId).SetColumns(c => c.IsDeleted == true).ExecuteCommand();
            if (scenario == "zero") f.Dto.ParentId = 0;
            if (scenario == "negative") f.Dto.ParentId = -1;
            var before = f.Snapshot();
            AssertError(await InvokeApiAsync(f.CallAsync, 400, environment), 400, "父分类不存在");
            Assert.Equal(operation == "create" ? new[] { "parent-read" } : new[] { "target-read", "target-map", "parent-read" }, f.Steps);
            Assert.Equal(before, f.Snapshot());
            capture.AssertQuiet();
        }
        foreach (var operation in Operations)
        {
            using var f = new Fixture(operation);
            var before = f.Snapshot();
            f.Controller.ModelState.AddModelError("Name", Secret);
            AssertError(await InvokeApiAsync(f.CallAsync, 400, environment), 400, "请求参数验证失败");
            Assert.Empty(f.Steps);
            Assert.Equal(before, f.Snapshot());
            capture.AssertQuiet();
        }
        foreach (var scenario in new[] { "zero", "negative", "self", "missing", "deleted" })
        {
            using var f = new Fixture("update");
            var status = 400;
            var message = "分类 ID 无效";
            if (scenario == "zero") f.RequestId = 0;
            if (scenario == "negative") f.RequestId = -1;
            if (scenario == "self") { f.Dto.ParentId = CategoryId; message = "分类不能设置自己为父级"; }
            if (scenario == "missing") f.Db.Deleteable<Category>().Where(c => c.Id == CategoryId).ExecuteCommand();
            if (scenario == "deleted") f.Db.Updateable<Category>().Where(c => c.Id == CategoryId).SetColumns(c => c.IsDeleted == true).ExecuteCommand();
            if (scenario is "missing" or "deleted") { status = 404; message = "分类不存在"; }
            var before = f.Snapshot();
            AssertError(await InvokeApiAsync(f.CallAsync, status, environment), status, message);
            Assert.Equal(status == 404 ? new[] { "target-read" } : Array.Empty<string>(), f.Steps);
            Assert.Equal(before, f.Snapshot());
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ReadMapAndWriteFailures_ShouldKeepOriginalCatchBoundary(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var stage in operation == "create" ? new[] { "parent-read", "parent-map", "create" } : new[] { "target-read", "target-map", "parent-read", "parent-map", "update" })
        foreach (var failure in new Exception[] { new UnsafeOperation(), new IOException(Secret) })
        {
            using var f = new Fixture(operation) { FailedStage = stage, Failure = failure };
            var before = f.Snapshot();
            var consumed = failure is InvalidOperationException && !stage.StartsWith("target-", StringComparison.Ordinal);
            var result = await InvokeApiAsync(f.CallAsync, consumed ? 400 : 500, environment);
            if (consumed) AssertError(result, 400, failure.Message);
            else Assert.Equal(ApiErrorCodes.UnexpectedError, result.Code);
            capture.AssertSingle(consumed ? "category.request_failed" : "http.failed",
                failure is InvalidOperationException ? "invalid-operation" : "io", consumed ? null : 500);
            Assert.Equal(stage, f.Steps.Last());
            Assert.Equal(before, f.Snapshot());
            Assert.Equal(0, f.Unit.TranCount);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task FailureAfterActualWrite_ShouldKeepCommittedDataAndSingleErrorOwner(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var failure in new Exception[] { new UnsafeOperation(), new IOException(Secret) })
        {
            using var f = new Fixture(operation) { FailedStage = operation + "-completed", Failure = failure };
            var consumed = failure is InvalidOperationException;
            var result = await InvokeApiAsync(f.CallAsync, consumed ? 400 : 500, environment);
            if (consumed) AssertError(result, 400, failure.Message);
            else Assert.Equal(ApiErrorCodes.UnexpectedError, result.Code);
            capture.AssertSingle(consumed ? "category.request_failed" : "http.failed", consumed ? "invalid-operation" : "io", consumed ? null : 500);
            Assert.Equal(operation + "-completed", f.Steps.Last());
            f.AssertWritten(Secret, Slug, 3);
            Assert.Equal(operation == "create" ? 3 : 2, f.Db.Queryable<Category>().Count());
            Assert.Equal(0, f.Unit.TranCount);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Success_ShouldPreserveHierarchySlugFieldsAndAudit(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var scenario in new[] { "normal", "root", "disabled-parent", "negative-level", "fallback-slug", "null-fields" })
        {
            using var f = new Fixture(operation);
            var expectedName = Secret;
            var expectedSlug = Slug;
            var expectedLevel = 3;
            if (scenario == "root") { f.Dto.ParentId = null; expectedLevel = 0; }
            if (scenario == "disabled-parent") f.Db.Updateable<Category>().Where(c => c.Id == ParentId).SetColumns(c => c.IsEnabled == false).ExecuteCommand();
            if (scenario == "negative-level")
            {
                f.Db.Updateable<Category>().Where(c => c.Id == ParentId).SetColumns(c => c.Level == -5).ExecuteCommand();
                expectedLevel = 0;
            }
            if (scenario == "fallback-slug")
            {
                f.Dto.Name = " New CATEGORY "; f.Dto.Slug = "  ";
                expectedName = "New CATEGORY"; expectedSlug = "new-category";
            }
            if (scenario == "null-fields")
            {
                f.Dto.Slug = null; f.Dto.Description = null; f.Dto.IconAttachmentId = null; f.Dto.CoverAttachmentId = null;
                expectedSlug = Secret.ToLowerInvariant();
            }
            var result = await InvokeApiAsync(f.CallAsync, 200, environment);
            Assert.True(result.IsSuccess);
            Assert.Equal(operation == "create" ? "创建成功" : "更新成功", result.MessageInfo);
            if (operation == "create") Assert.Equal(f.WrittenId, Assert.IsType<long>(result.ResponseData));
            else Assert.True(Assert.IsType<bool>(result.ResponseData));
            Assert.Null(result.Code);
            Assert.Null(result.MessageKey);
            f.AssertWritten(expectedName, expectedSlug, expectedLevel);
            Assert.Equal(operation == "create" ? 3 : 2, f.Db.Queryable<Category>().Count());
            if (scenario == "root") Assert.DoesNotContain("parent-read", f.Steps);
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ZeroAffectedRowsAfterTargetCheck_ShouldKeepOriginalSuccess(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var softDelete in new[] { false, true })
        {
            using var f = new Fixture("update") { RemoveBeforeUpdate = true, SoftDeleteBeforeUpdate = softDelete };
            var before = f.Db.Queryable<Category>().InSingle(CategoryId);
            var response = await InvokeApiAsync(f.CallAsync, 200, environment);
            Assert.True(response.IsSuccess);
            Assert.True(Assert.IsType<bool>(response.ResponseData));
            Assert.Equal("更新成功", response.MessageInfo);
            Assert.Equal(0, f.AffectedRows);
            if (softDelete)
            {
                before.IsDeleted = true;
                Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(f.Db.Queryable<Category>().InSingle(CategoryId)));
            }
            else Assert.Null(f.Db.Queryable<Category>().InSingle(CategoryId));
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertQuiet();
        }
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly IMapper Mapper = new MapperConfiguration(c => c.AddProfile<ForumProfile>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly string _operation;
        private readonly DateTime _startedAt = DateTime.UtcNow.AddSeconds(-1);
        private int _queryCalls;
        public SqlSugarScope Db { get; }
        public UnitOfWorkManage Unit { get; }
        public CategoryController Controller { get; }
        public CreateCategoryDto Dto { get; } = new()
        {
            Name = "  " + Secret + "  ", Slug = " PRIVATE  CATEGORY-SLUG ", Description = "  " + Secret + "  ",
            ParentId = ParentId, IconAttachmentId = IconId, CoverAttachmentId = CoverId, OrderSort = 17, IsEnabled = false
        };
        public List<string> Steps { get; } = [];
        public string? FailedStage { get; set; }
        public Exception Failure { get; set; } = new UnsafeOperation();
        public long RequestId { get; set; } = CategoryId;
        public long WrittenId { get; private set; }
        public int? AffectedRows { get; private set; }
        public bool RemoveBeforeUpdate { get; set; }
        public bool SoftDeleteBeforeUpdate { get; set; }

        public Fixture(string operation)
        {
            _operation = operation;
            new ServiceCollection().ConfigureApplication();
            Db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", DbType = DbType.Sqlite, ConnectionString = "Data Source=:memory:",
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            Db.CodeFirst.InitTables<Category>();
            Unit = new UnitOfWorkManage(Db, NullLogger<UnitOfWorkManage>.Instance);
            Db.Insertable(new List<Category>
            {
                new("Original") { Id = CategoryId, Slug = "original", PostCount = 9, CreateBy = "original-author", CreateId = 42, CreateTime = CreatedAt },
                new("Parent") { Id = ParentId, Slug = "parent", Level = 2, CreateBy = "original-author", CreateId = 42, CreateTime = CreatedAt }
            }).ExecuteCommand();
            var proxy = new ProxyGenerator();
            var repository = proxy.CreateInterfaceProxyWithTarget<IBaseRepository<Category>>(new BaseRepository<Category>(Unit), new RepositoryInterceptor(this));
            var mapper = proxy.CreateInterfaceProxyWithTarget(Mapper, new MapperInterceptor(this));
            var service = proxy.CreateInterfaceProxyWithTarget<IBaseService<Category, CategoryVo>>(
                new BaseService<Category, CategoryVo>(mapper, repository), new TranAop(Unit));
            var accessor = new Mock<ICurrentUserAccessor>();
            accessor.SetupGet(a => a.Current).Returns(new CurrentUser { UserId = UserId, UserName = Secret, IsAuthenticated = true });
            Controller = new CategoryController(service, accessor.Object, new Mock<IAttachmentUrlResolver>(MockBehavior.Strict).Object);
        }

        private void Step(string stage)
        {
            Steps.Add(stage);
            if (stage == FailedStage) throw Failure;
        }
        private sealed class MapperInterceptor(Fixture fixture) : IInterceptor
        {
            public void Intercept(Castle.DynamicProxy.IInvocation invocation)
            {
                if (invocation.Method.Name == "Map" && invocation.Arguments[0] is Category category)
                    fixture.Step(category.Id == CategoryId ? "target-map" : "parent-map");
                invocation.Proceed();
            }
        }
        private sealed class RepositoryInterceptor(Fixture fixture) : IInterceptor
        {
            public void Intercept(Castle.DynamicProxy.IInvocation invocation)
            {
                var stage = invocation.Method.Name switch
                {
                    "QueryFirstAsync" => ++fixture._queryCalls == 1 && fixture._operation == "update" ? "target-read" : "parent-read",
                    "AddAsync" => "create",
                    "UpdateColumnsAsync" => "update",
                    _ => throw new InvalidOperationException("Unexpected repository call")
                };
                fixture.Step(stage);
                if (stage == "update" && fixture.RemoveBeforeUpdate)
                {
                    if (fixture.SoftDeleteBeforeUpdate)
                        fixture.Db.Updateable<Category>().Where(c => c.Id == CategoryId).SetColumns(c => c.IsDeleted == true).ExecuteCommand();
                    else fixture.Db.Deleteable<Category>().Where(c => c.Id == CategoryId).ExecuteCommand();
                }
                invocation.Proceed();
                if (stage == "create") invocation.ReturnValue = fixture.AfterCreateAsync((Task<long>)invocation.ReturnValue);
                if (stage == "update") invocation.ReturnValue = fixture.AfterUpdateAsync((Task<int>)invocation.ReturnValue);
            }
        }
        private async Task<long> AfterCreateAsync(Task<long> task)
        {
            WrittenId = await task;
            Step("create-completed");
            return WrittenId;
        }
        private async Task<int> AfterUpdateAsync(Task<int> task)
        {
            var affected = await task;
            AffectedRows = affected;
            WrittenId = RequestId;
            Step("update-completed");
            return affected;
        }
        public Task<MessageModel> CallAsync()
        {
            Steps.Clear();
            _queryCalls = 0;
            return _operation == "create" ? Controller.Create(Dto) : Controller.Update(RequestId, Dto);
        }
        public string Snapshot() => JsonSerializer.Serialize(Db.Queryable<Category>().OrderBy(c => c.Id).ToList());
        public void AssertWritten(string name, string slug, int level)
        {
            var written = Db.Queryable<Category>().InSingle(WrittenId);
            Assert.NotNull(written);
            Assert.Equal(name, written.Name);
            Assert.Equal(slug, written.Slug);
            Assert.Equal(Dto.Description ?? "", written.Description);
            Assert.Equal(Dto.IconAttachmentId, written.IconAttachmentId);
            Assert.Equal(Dto.CoverAttachmentId, written.CoverAttachmentId);
            Assert.Equal(Dto.ParentId, written.ParentId);
            Assert.Equal(level, written.Level);
            Assert.Equal(Dto.OrderSort, written.OrderSort);
            Assert.Equal(Dto.IsEnabled, written.IsEnabled);
            Assert.False(written.IsDeleted);
            Assert.Equal(_operation == "create" ? 0 : 9, written.PostCount);
            Assert.Equal(UserId, written.ModifyId);
            Assert.Equal(Secret, written.ModifyBy);
            Assert.NotNull(written.ModifyTime);
            if (_operation == "create")
            {
                Assert.True(WrittenId > 0);
                Assert.Equal(UserId, written.CreateId);
                Assert.Equal(Secret, written.CreateBy);
                Assert.InRange(written.CreateTime, _startedAt, DateTime.UtcNow.AddSeconds(1));
                Assert.InRange(written.ModifyTime.Value, _startedAt, DateTime.UtcNow.AddSeconds(1));
            }
            else
            {
                Assert.Equal(CategoryId, WrittenId);
                Assert.Equal(1, AffectedRows);
                Assert.Equal(42, written.CreateId);
                Assert.Equal("original-author", written.CreateBy);
                Assert.Equal(CreatedAt, written.CreateTime);
                // 更新沿用表达式 DateTime.Now，未把日志治理扩展为时间规范化改造。
                Assert.True(written.ModifyTime > CreatedAt);
            }
        }
        public void Dispose() => Db.Dispose();
    }

    private static void AssertError(MessageModel result, int status, string message)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(message, result.MessageInfo);
        Assert.Equal(status == 404 ? ApiErrorCodes.NotFound : ApiErrorCodes.ValidationFailed, result.Code);
        Assert.Equal(status == 404 ? "error.common.not_found" : "error.common.validation_failed", result.MessageKey);
        Assert.Null(result.ResponseData);
        Assert.Null(result.MessageArguments);
    }

    private static async Task<MessageModel> InvokeApiAsync(Func<Task<MessageModel>> action, int status, string environment)
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
        context.Request.Path = "/api/v1/Category/Create";
        context.Request.QueryString = new QueryString("?query=" + Secret);
        using var output = new MemoryStream();
        context.Response.Body = output;
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
        result ??= JsonSerializer.Deserialize<MessageModel>(output.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        Assert.Equal(status, result.StatusCode);
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

    private sealed class UnsafeOperation : InvalidOperationException
    {
        public UnsafeOperation() : base(Secret, new IOException(Secret)) => Data["payload"] = Secret;
        public override string ToString() => throw new InvalidOperationException("禁止输出异常原文");
    }
    private sealed class UnreadableOperation : InvalidOperationException
    {
        public override string Message => throw new IOException(Secret);
        public override string ToString() => throw new InvalidOperationException("禁止输出异常原文");
    }
    private sealed class HostileFailure : Exception
    {
        public override string Message => throw new InvalidOperationException("禁止读取异常原文");
        public override string ToString() => throw new InvalidOperationException("禁止输出异常原文");
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
        public void AssertSingle(string code, string kind, int? status = null)
        {
            var text = _output.ToString();
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(code, text);
            Assert.Contains("Error", text);
            Assert.Contains(kind, text);
            foreach (var forbidden in new[] { Secret, Slug, CategoryId.ToString(), ParentId.ToString(), UserId.ToString(), IconId.ToString(), CoverId.ToString(), "runtime.unclassified", "父分类不存在", "System.IO.IOException", "System.InvalidOperationException", "original-author", "#aabbcc" })
                Assert.DoesNotContain(forbidden, text);
            if (_mode != null)
            {
                using var json = JsonDocument.Parse(text);
                var root = json.RootElement;
                Assert.Equal(_mode, root.GetProperty("mode").GetString());
                Assert.Equal(code, root.GetProperty("eventCode").GetString());
                Assert.Equal("Error", root.GetProperty("level").GetString());
                Assert.False(root.GetProperty("diagnostic").GetBoolean());
                var properties = root.GetProperty("properties");
                Assert.Equal(status.HasValue ? 2 : 1, properties.EnumerateObject().Count());
                Assert.Equal(kind, properties.GetProperty("failureKind").GetString());
                if (status.HasValue) Assert.Equal(status.Value, properties.GetProperty("statusCode").GetInt32());
            }
            else
            {
                var logged = Assert.Single(_events);
                Assert.Equal(LogEventLevel.Error, logged.Level);
                Assert.Null(logged.Exception);
                Assert.Equal(code, Assert.IsType<ScalarValue>(logged.Properties["EventCode"]).Value);
                Assert.Equal(kind, Assert.IsType<ScalarValue>(logged.Properties["failureKind"]).Value);
                if (!status.HasValue) Assert.Equal(new[] { "EventCode", "failureKind" }, logged.Properties.Keys.OrderBy(k => k));
            }
        }
        public void Clear()
        {
            _output.GetStringBuilder().Clear();
            _events.Clear();
        }
        public void Dispose() { Log.Logger = _previous; _logger.Dispose(); _output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output, List<LogEvent> events) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(LogEvent value)
        {
            events.Add(value);
            _formatter.Format(value, output);
        }
    }
}
