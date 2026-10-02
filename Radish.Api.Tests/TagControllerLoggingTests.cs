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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Filters;
using Radish.Api.Resources;
using Radish.Common.CoreTool;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Extension.AopExtension;
using Radish.Extension.AutoMapperExtension.CustomProfiles;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using Radish.Shared.Constants;
using Serilog;
using Serilog.Events;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class TagControllerLoggingTests
{
    private const string Secret = "TAG_PRIVATE_SENTINEL";
    private const string Slug = "tag-private-slug";
    private const long TagId = 912345678;
    private const long UserId = 623456789;
    private static readonly DateTime CreatedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly string[] Operations = ["create", "update"];
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Consumers_ShouldPreserveResponsesAndDistinguishTypedConflictFromFailures(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var failure in new InvalidOperationException[]
        {
            new TagNameConflictException("标签名称已存在"),
            new InvalidOperationException("标签名称已存在"), new UnsafeOperation()
        })
        {
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<ITagService>(new ThrowingInterceptor(failure));
            var controller = CreateController(service);
            var result = await InvokeApiAsync(() => CallControllerAsync(controller, operation, new CreateTagDto()), 400, environment);
            AssertError(result, 400, failure.Message);
            Assert.Equal(ApiErrorCodes.ValidationFailed, result!.Code);
            Assert.Equal("error.common.validation_failed", result.MessageKey);
            if (failure is TagNameConflictException) capture.AssertQuiet();
            else capture.AssertSingle("tag.request_failed", "invalid-operation");
            capture.Clear();
        }

        foreach (var operation in Operations)
        foreach (var (failure, kind) in new (Exception, string)[]
        {
            (new IOException(Secret), "io"), (new ArgumentException(Secret), "argument"),
            (new TimeoutException(Secret), "timeout"), (new OperationCanceledException(Secret), "cancelled"),
            (new HostileFailure(), "other"), (new UnreadableOperation(), "io"),
            (new AggregateException(new TagNameConflictException("标签名称已存在")), "aggregate"),
            (new AggregateException(new UnsafeOperation(), new IOException(Secret)), "aggregate")
        })
        {
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<ITagService>(new ThrowingInterceptor(failure));
            Assert.Null(await InvokeApiAsync(() => CallControllerAsync(CreateController(service), operation, new CreateTagDto()), 500, environment));
            capture.AssertSingle("http.failed", kind, 500);
            capture.Clear();
        }

        // BusinessException 从未在 TagController 消费，仍由 API 处理其状态与错误契约。
        foreach (var operation in Operations)
        foreach (var status in new[] { 400, 403, 409, 500, 503 })
        {
            var failure = new BusinessException(Secret, status, "Private.Code", "Private.Key", Secret);
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<ITagService>(new ThrowingInterceptor(failure));
            Task<MessageModel> CallAsync() => CallControllerAsync(CreateController(service), operation, new CreateTagDto());
            Assert.Same(failure, await Assert.ThrowsAsync<BusinessException>(CallAsync));
            Assert.Null(await InvokeApiAsync(CallAsync, status, environment));
            if (status >= 500) capture.AssertSingle("http.failed", "other", status);
            else capture.AssertQuiet();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task UnconsumedBusinessNotFound_ShouldRemainOwnedByExistingFrameworkBoundary(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        {
            var failure = new BusinessException(Secret, 404, "Private.Code", "Private.Key");
            var service = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<ITagService>(new ThrowingInterceptor(failure));
            Task<MessageModel> CallAsync() => CallControllerAsync(CreateController(service), operation, new CreateTagDto());
            Assert.Same(failure, await Assert.ThrowsAsync<BusinessException>(CallAsync));
            capture.AssertQuiet();
            Assert.Null(await InvokeApiAsync(CallAsync, 404, environment, failure));
            // 合成的依赖 404 触达既有框架缺口，不冒充标签最终消费或安全输出已收口。
            capture.AssertFrameworkNotFound(failure);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DuplicateNamesAndPrechecks_ShouldStayQuietAndAvoidWrites(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var enabled in new[] { false, true })
        {
            using var f = new Fixture(operation);
            f.InsertTag(TagId + 1, Secret, "occupied", enabled: enabled);
            var before = f.Snapshot();
            var failure = await Assert.ThrowsAsync<TagNameConflictException>(f.CallServiceAsync);
            Assert.IsAssignableFrom<InvalidOperationException>(failure);
            Assert.Equal("标签名称已存在", failure.Message);
            AssertError(await InvokeApiAsync(f.CallAsync, 400, environment), 400, failure.Message);
            Assert.Equal(operation == "create" ? new[] { "name-check" } : new[] { "read", "name-check" }, f.Steps);
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
        foreach (var id in new[] { 0L, -1L })
        {
            using var f = new Fixture("update") { RequestId = id };
            AssertError(await InvokeApiAsync(f.CallAsync, 400, environment), 400, "标签ID无效");
            Assert.Empty(f.Steps);
            var failure = await Assert.ThrowsAsync<ArgumentException>(f.CallServiceAsync);
            Assert.Equal("id", failure.ParamName);
            Assert.Empty(f.Steps);
            capture.AssertQuiet();
        }
        foreach (var deleted in new[] { false, true })
        {
            using var f = new Fixture("update");
            if (deleted) f.Db.Updateable<Tag>().Where(t => t.Id == TagId).SetColumns(t => t.IsDeleted == true).ExecuteCommand();
            else f.Db.Deleteable<Tag>().Where(t => t.Id == TagId).ExecuteCommand();
            var before = f.Snapshot();
            AssertError(await InvokeApiAsync(f.CallAsync, 404, environment), 404, "标签不存在");
            Assert.Equal(new[] { "read" }, f.Steps);
            Assert.Equal(before, f.Snapshot());
            capture.AssertQuiet();
        }
        using var unchanged = new Fixture("update") { ReturnFalseOnUpdate = true };
        var snapshot = unchanged.Snapshot();
        AssertError(await InvokeApiAsync(unchanged.CallAsync, 404, environment), 404, "标签不存在");
        Assert.Equal(snapshot, unchanged.Snapshot());
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DependencyFailuresBeforeWrite_ShouldPropagateOrBeConsumedOnceWithoutChangingRows(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var stage in operation == "create" ? new[] { "name-check", "slug-check", "create" } : new[] { "read", "name-check", "slug-check", "update" })
        foreach (var failure in new Exception[] { new UnsafeOperation(), new IOException(Secret) })
        {
            using var f = new Fixture(operation) { FailedStage = stage, Failure = failure };
            var before = f.Snapshot();
            Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(f.CallServiceAsync));
            Assert.Equal(before, f.Snapshot());
            capture.AssertQuiet();
            var result = await InvokeApiAsync(f.CallAsync, failure is InvalidOperationException ? 400 : 500, environment);
            if (failure is InvalidOperationException)
            {
                AssertError(result, 400, failure.Message);
                capture.AssertSingle("tag.request_failed", "invalid-operation");
            }
            else
            {
                Assert.Null(result);
                capture.AssertSingle("http.failed", "io", 500);
            }
            Assert.Equal(stage, f.Steps.Last());
            Assert.Equal(before, f.Snapshot());
            Assert.Equal(0, f.Unit.TranCount);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task FailureAfterActualWrite_ShouldKeepExistingNontransactionalCommitAndErrorOwner(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var direct in new[] { false, true })
        foreach (var failure in new Exception[] { new UnsafeOperation(), new IOException(Secret) })
        {
            using var f = new Fixture(operation) { FailedStage = operation + "-completed", Failure = failure };
            if (direct)
            {
                Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(f.CallServiceAsync));
                capture.AssertQuiet();
            }
            else
            {
                var result = await InvokeApiAsync(f.CallAsync, failure is InvalidOperationException ? 400 : 500, environment);
                if (failure is InvalidOperationException)
                {
                    AssertError(result, 400, failure.Message);
                    capture.AssertSingle("tag.request_failed", "invalid-operation");
                }
                else
                {
                    Assert.Null(result);
                    capture.AssertSingle("http.failed", "io", 500);
                }
            }
            Assert.Equal(operation + "-completed", f.Steps.Last());
            f.AssertWritten(Secret, Slug);
            Assert.Equal(operation == "create" ? 2 : 1, f.Db.Queryable<Tag>().Count());
            Assert.Equal(0, f.Unit.TranCount);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Success_ShouldPreserveNameSlugCollisionAndAuditRules(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var scenario in new[] { "normal", "collision", "long-slug", "deleted", "fixed-name", "self", "system" })
        {
            if (operation == "create" && scenario == "self") continue;
            using var f = new Fixture(operation);
            var expectedName = Secret;
            var expectedSlug = Slug;
            switch (scenario)
            {
                case "collision":
                    f.InsertTag(TagId + 1, "occupied-one", Slug, enabled: false);
                    f.InsertTag(TagId + 2, "occupied-two", Slug + "-2");
                    f.InsertTag(TagId + 3, "deleted-three", Slug + "-3", deleted: true);
                    expectedSlug += "-3";
                    break;
                case "long-slug":
                    f.Dto.Slug = new string('a', 50);
                    f.InsertTag(TagId + 1, "occupied-long", f.Dto.Slug);
                    expectedSlug = new string('a', 48) + "-2";
                    break;
                case "deleted":
                    f.InsertTag(TagId + 1, Secret, Slug, deleted: true);
                    break;
                case "fixed-name":
                    f.Dto.Name = " 抽奖 ";
                    f.Dto.Slug = null;
                    expectedName = "抽奖";
                    expectedSlug = "lotteries";
                    break;
                case "self":
                    f.Dto.Name = " Original ";
                    f.Dto.Slug = " original ";
                    expectedName = "Original";
                    expectedSlug = "original";
                    break;
                case "system":
                    f.OperatorName = "  ";
                    f.Dto.Description = null;
                    f.Dto.Color = null;
                    break;
            }
            var beforeCount = f.Db.Queryable<Tag>().Count();
            var result = await InvokeApiAsync(f.CallAsync, 200, environment);
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal(operation == "create" ? "创建成功" : "更新成功", result.MessageInfo);
            if (operation == "create") Assert.Equal(f.WrittenId, Assert.IsType<long>(result.ResponseData));
            else Assert.True(Assert.IsType<bool>(result.ResponseData));
            Assert.Null(result.Code);
            Assert.Null(result.MessageKey);
            f.AssertWritten(expectedName, expectedSlug);
            Assert.Equal(beforeCount + (operation == "create" ? 1 : 0), f.Db.Queryable<Tag>().Count());
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertQuiet();
        }
    }

    private sealed class UnsafeOperation() : InvalidOperationException(Secret, new IOException(Secret))
    {
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
    private sealed class ThrowingInterceptor(Exception failure) : IInterceptor
    {
        public void Intercept(Castle.DynamicProxy.IInvocation invocation) => throw failure;
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly IMapper Mapper = new MapperConfiguration(c => c.AddProfile<ForumProfile>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly string _operation;
        private int _existsCalls;
        private readonly DateTime _startedAt = DateTime.UtcNow.AddSeconds(-1);
        public SqlSugarScope Db { get; }
        public UnitOfWorkManage Unit { get; }
        public ITagService Service { get; }
        public TagController Controller { get; }
        public CreateTagDto Dto { get; } = new()
        {
            Name = "  " + Secret + "  ", Slug = " TAG-PRIVATE-SLUG ",
            Description = "  " + Secret + "  ", Color = " #aabbcc ",
            SortOrder = 17, IsEnabled = false, IsFixed = true
        };
        public List<string> Steps { get; } = [];
        public string? FailedStage { get; set; }
        public Exception Failure { get; set; } = new UnsafeOperation();
        public long RequestId { get; set; } = TagId;
        public string OperatorName { get; set; } = Secret;
        public bool ReturnFalseOnUpdate { get; set; }
        public long WrittenId { get; private set; }

        public Fixture(string operation)
        {
            _operation = operation;
            new ServiceCollection().ConfigureApplication();
            Db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", DbType = DbType.Sqlite, ConnectionString = "Data Source=:memory:",
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            Db.CodeFirst.InitTables<Tag>();
            Unit = new UnitOfWorkManage(Db, NullLogger<UnitOfWorkManage>.Instance);
            InsertTag(TagId, "Original", "original");
            var repository = new ProxyGenerator().CreateInterfaceProxyWithTarget<IBaseRepository<Tag>>(
                new BaseRepository<Tag>(Unit), new RepositoryInterceptor(this));
            var service = new TagService(Mapper, repository, new Mock<ITagDiscoveryRepository>(MockBehavior.Strict).Object);
            Service = new ProxyGenerator().CreateInterfaceProxyWithTarget<ITagService>(service, new TranAop(Unit));
            Controller = CreateController(Service, () => new CurrentUser { UserId = UserId, UserName = OperatorName, IsAuthenticated = true });
        }

        public void InsertTag(long id, string name, string slug, bool enabled = true, bool deleted = false) => Db.Insertable(new Tag(name)
        {
            Id = id, Slug = slug, IsEnabled = enabled, IsDeleted = deleted, PostCount = 9,
            CreateId = 42, CreateBy = "original-author", CreateTime = CreatedAt,
            DeletedAt = deleted ? CreatedAt : null, DeletedBy = deleted ? "original-author" : null
        }).ExecuteCommand();

        private void Step(string stage)
        {
            Steps.Add(stage);
            if (stage == FailedStage) throw Failure;
        }
        private sealed class RepositoryInterceptor(Fixture fixture) : IInterceptor
        {
            public void Intercept(Castle.DynamicProxy.IInvocation invocation)
            {
                var stage = invocation.Method.Name switch
                {
                    "QueryByIdAsync" => "read",
                    "QueryExistsAsync" => ++fixture._existsCalls == 1 ? "name-check" : "slug-check",
                    "AddAsync" => "create",
                    "UpdateAsync" => "update",
                    _ => throw new InvalidOperationException("Unexpected repository call")
                };
                fixture.Step(stage);
                if (stage == "update" && fixture.ReturnFalseOnUpdate)
                {
                    invocation.ReturnValue = Task.FromResult(false);
                    return;
                }
                invocation.Proceed();
                if (stage == "create") invocation.ReturnValue = fixture.AfterCreateAsync((Task<long>)invocation.ReturnValue);
                if (stage == "update") invocation.ReturnValue = fixture.AfterUpdateAsync((Task<bool>)invocation.ReturnValue);
            }
        }
        private async Task<long> AfterCreateAsync(Task<long> task)
        {
            WrittenId = await task;
            Step("create-completed");
            return WrittenId;
        }
        private async Task<bool> AfterUpdateAsync(Task<bool> task)
        {
            var result = await task;
            WrittenId = RequestId;
            Step("update-completed");
            return result;
        }
        private void ResetTrace()
        {
            Steps.Clear();
            _existsCalls = 0;
        }
        public Task<MessageModel> CallAsync()
        {
            ResetTrace();
            return CallControllerAsync(Controller, _operation, Dto, RequestId);
        }
        public async Task CallServiceAsync()
        {
            ResetTrace();
            if (_operation == "create") await Service.CreateTagAsync(Dto, UserId, OperatorName);
            else await Service.UpdateTagAsync(RequestId, Dto, UserId, OperatorName);
        }
        public string Snapshot() => JsonSerializer.Serialize(Db.Queryable<Tag>().OrderBy(t => t.Id).ToList());
        public void AssertWritten(string name, string slug)
        {
            var written = Db.Queryable<Tag>().InSingle(WrittenId);
            Assert.NotNull(written);
            Assert.Equal(name, written.Name);
            Assert.Equal(slug, written.Slug);
            Assert.Equal(Dto.Description?.Trim() ?? "", written.Description);
            Assert.Equal(Dto.Color?.Trim() ?? "", written.Color);
            Assert.Equal(Dto.SortOrder, written.SortOrder);
            Assert.Equal(Dto.IsEnabled, written.IsEnabled);
            Assert.Equal(Dto.IsFixed, written.IsFixed);
            Assert.False(written.IsDeleted);
            Assert.Null(written.DeletedAt);
            Assert.Null(written.DeletedBy);
            Assert.Equal(_operation == "create" ? 0 : 9, written.PostCount);
            var operatorName = string.IsNullOrWhiteSpace(OperatorName) ? "System" : OperatorName;
            if (_operation == "create")
            {
                Assert.True(WrittenId > 0);
                Assert.Equal(UserId, written.CreateId);
                Assert.Equal(operatorName, written.CreateBy);
                Assert.InRange(written.CreateTime, _startedAt, DateTime.UtcNow.AddSeconds(1));
                Assert.Null(written.ModifyId);
                Assert.Null(written.ModifyTime);
            }
            else
            {
                Assert.Equal(TagId, WrittenId);
                Assert.Equal(42, written.CreateId);
                Assert.Equal("original-author", written.CreateBy);
                Assert.Equal(CreatedAt, written.CreateTime);
                Assert.Equal(UserId, written.ModifyId);
                Assert.Equal(operatorName, written.ModifyBy);
                Assert.NotNull(written.ModifyTime);
                Assert.InRange(written.ModifyTime.Value, _startedAt, DateTime.UtcNow.AddSeconds(1));
            }
        }
        public void Dispose() => Db.Dispose();
    }

    private static TagController CreateController(ITagService service, Func<CurrentUser>? current = null)
    {
        var accessor = new Mock<ICurrentUserAccessor>();
        accessor.SetupGet(a => a.Current).Returns(current ?? (() => new CurrentUser { UserId = UserId, UserName = Secret }));
        var localizer = new Mock<IStringLocalizer<Errors>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, resourceNotFound: true));
        return new TagController(service, accessor.Object, localizer.Object);
    }

    private static Task<MessageModel> CallControllerAsync(TagController controller, string operation, CreateTagDto dto, long id = TagId) =>
        operation == "create" ? controller.Create(dto) : controller.Update(id, dto);

    private static void AssertError(MessageModel? result, int status, string message)
    {
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(message, result.MessageInfo);
        Assert.Null(result.ResponseData);
        Assert.Null(result.MessageArguments);
    }

    private static async Task<MessageModel?> InvokeApiAsync(
        Func<Task<MessageModel>> action, int status, string environment, BusinessException? expectedNotFoundRethrow = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        MessageModel? result = null;
        app.Run(async context =>
        {
            result = await action();
            await ApplyResultAsync(context, result);
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Tag/Create";
        context.Request.QueryString = new QueryString("?query=" + Secret);
        using var output = new MemoryStream();
        context.Response.Body = output;
        context.RequestServices = app.Services;
        var pipeline = ((IApplicationBuilder)app).Build();
        if (expectedNotFoundRethrow == null) await pipeline(context);
        else
        {
            // 现有中间件未允许异常处理器返回 404；仅锁定该合成故障的传播，不修改框架配置。
            var propagated = await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline(context));
            Assert.Same(expectedNotFoundRethrow, propagated.InnerException);
        }
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
        public void AssertFrameworkNotFound(BusinessException failure)
        {
            var text = _output.ToString();
            Assert.DoesNotContain("tag.request_failed", text);
            Assert.DoesNotContain("http.failed", text);
            if (_mode != null)
            {
                Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                using var json = JsonDocument.Parse(text);
                Assert.Equal("runtime.unclassified", json.RootElement.GetProperty("eventCode").GetString());
                Assert.Equal("Error", json.RootElement.GetProperty("level").GetString());
                Assert.DoesNotContain(Secret, text);
            }
            else
            {
                var logged = Assert.Single(_events);
                Assert.Equal(LogEventLevel.Error, logged.Level);
                Assert.Same(failure, logged.Exception);
                Assert.Contains("ExceptionHandlerMiddleware", logged.Properties["SourceContext"].ToString());
            }
        }
        public void AssertSingle(string code, string kind, int? status = null)
        {
            var text = _output.ToString();
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(code, text);
            Assert.Contains("Error", text);
            Assert.Contains(kind, text);
            foreach (var forbidden in new[] { Secret, Slug, TagId.ToString(), UserId.ToString(), "runtime.unclassified", "标签名称已存在", "System.IO.IOException", "System.InvalidOperationException", "original-author", "#aabbcc" })
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
