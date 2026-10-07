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
using Radish.Common.CacheTool;
using Radish.Infrastructure.FileStorage;
using Radish.Infrastructure.ImageProcessing;
using Microsoft.Extensions.Localization;
using Radish.Api.Resources;
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
public sealed class StickerControllerLoggingTests
{
    private const string Secret = "STICKER_PRIVATE_SENTINEL";
    private const long GroupId = 912345678;
    private const long AttachmentId = 823456789;
    private const long UserId = 623456789;
    private static readonly string[] Operations = ["group", "single", "batch", "sort"];
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ExplicitRejections_ShouldPreserveResponsesAndRemainQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var scenario in new[] { "model", "input", "missing", "conflict" })
        {
            using var f = new Fixture(operation);
            if (scenario == "model") f.Controller.ModelState.AddModelError("Code", Secret);
            if (scenario == "input")
            {
                f.Group.Code = "!"; f.Single.Code = "!"; f.Batch.GroupId = 0; f.Sort.GroupId = 0;
            }
            if (scenario == "missing")
            {
                f.Db.Deleteable<StickerGroup>().In(GroupId).ExecuteCommand();
                // 分组创建没有目标缺失校验，改测已有分组标识冲突。
                if (operation == "group")
                {
                    f.Db.Insertable(new StickerGroup { Id = GroupId, Code = "private_group" }).ExecuteCommand();
                    f.Group.Code = "private_group";
                }
            }
            if (scenario == "conflict")
            {
                f.Group.Code = "private_group"; f.Single.Code = "existing_1";
                f.Batch.Stickers[1].Code = f.Batch.Stickers[0].Code;
                f.Sort.Items[1].Id = 999;
            }
            var before = f.Snapshot();
            var status = scenario is "model" or "input" ? 400 : operation == "batch" && scenario == "missing" ? 404 : 409;
            var result = await InvokeApiAsync(f.CallAsync, status, environment);
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Equal(before, f.Snapshot());
            if (scenario == "model") Assert.Empty(f.Steps);
            if (status == 409 && operation is "group" or "single") Assert.Equal("CodeAlreadyExists", result.Code);
            if (status == 409 && operation == "sort") Assert.Equal("StickerSortSnapshotStale", result.Code);
            if (status == 404) Assert.Equal("StickerGroupNotFound", result.Code);
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertQuiet();
        }
        foreach (var attachment in new long?[] { null, 0, 999 })
        {
            using var f = new Fixture("single");
            f.Single.AttachmentId = attachment;
            var status = attachment == 999 ? 409 : 400;
            var result = await InvokeApiAsync(f.CallAsync, status, environment);
            Assert.Contains(attachment == 999 ? "关联附件不存在" : "附件 ID 不能为空", result!.MessageInfo);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ConsumedDependencies_ShouldNotBeClassifiedByMessage(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var failure in new Exception[] { new UnsafeArgument(), new UnsafeOperation(), new InvalidOperationException("分组不存在或已删除") })
        {
            using var f = new Fixture(operation) { FailedStage = operation == "group" ? "group.QueryExistsAsync" : "group.QueryByIdAsync", Failure = failure };
            var before = f.Snapshot();
            var status = failure is ArgumentException ? 400 : operation == "batch" ? 404 : 409;
            var response = await InvokeApiAsync(f.CallAsync, status, environment);
            Assert.Equal(failure.Message, response!.MessageInfo);
            Assert.Equal(before, f.Snapshot());
            capture.AssertEvents(("sticker.request_failed", "Error", failure is ArgumentException ? "argument" : "invalid-operation", null));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task UnconsumedOrUnbuildableResponses_ShouldRemainApiOwned(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var (failure, kind) in new (Exception, string)[]
        {
            (new IOException(Secret), "io"), (new TimeoutException(Secret), "timeout"),
            (new OperationCanceledException(Secret), "cancelled"), (new UnreadableOperation(), "io"),
            (new UnreadableArgument(), "io"), (new HostileFailure(), "other")
        })
        {
            using var f = new Fixture(operation) { FailedStage = operation == "group" ? "group.QueryExistsAsync" : "group.QueryByIdAsync", Failure = failure };
            Assert.Null(await InvokeApiAsync(f.CallAsync, 500, environment));
            capture.AssertEvents(("http.failed", "Error", kind, 500));
            capture.Clear();
        }
        foreach (var operation in new[] { "batch", "sort" })
        {
            using var f = new Fixture(operation) { FailedStage = "group.QueryByIdAsync", Failure = new UnsafeOperation() };
            f.Localizer.Setup(l => l[It.IsAny<string>()]).Throws(new IOException(Secret));
            Assert.Null(await InvokeApiAsync(f.CallAsync, 500, environment));
            capture.AssertEvents(("http.failed", "Error", "io", 500));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SuccessfulWrites_ShouldPreserveDataAndTransactionBoundaries(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        {
            using var f = new Fixture(operation);
            var response = await InvokeApiAsync(f.CallAsync, 200, environment);
            Assert.True(response!.IsSuccess);
            if (operation == "group")
            {
                var row = f.Db.Queryable<StickerGroup>().InSingle(Assert.IsType<long>(response.ResponseData));
                Assert.Equal("new_group", row.Code); Assert.Equal(Secret, row.Name);
                Assert.Equal(Secret, row.Description); Assert.Equal(UserId, row.CreateId);
                Assert.Equal(AttachmentId, row.CoverAttachmentId); Assert.False(row.IsEnabled);
            }
            else if (operation == "single")
            {
                var row = f.Db.Queryable<Sticker>().InSingle(Assert.IsType<long>(response.ResponseData));
                Assert.Equal("private_sticker", row.Code); Assert.Equal(Secret, row.Name);
                Assert.Equal(AttachmentId, row.AttachmentId); Assert.Equal(UserId, row.CreateId);
                Assert.True(row.IsAnimated); Assert.False(row.AllowInline); Assert.Equal(17, row.Sort);
            }
            else if (operation == "batch")
            {
                var result = Assert.IsType<StickerBatchAddResultVo>(response.ResponseData);
                Assert.Equal(2, result.VoCreatedCount); Assert.Equal(2, result.VoStickerIds.Count);
                Assert.Empty(result.VoFailedItems); Assert.Empty(result.VoConflicts);
                var rows = f.Db.Queryable<Sticker>().Where(s => result.VoStickerIds.Contains(s.Id)).OrderBy(s => s.Sort).ToList();
                Assert.Equal(new[] { 0, 1 }, rows.Select(s => s.Sort));
                Assert.All(rows, row => { Assert.Equal(UserId, row.CreateId); Assert.True(row.IsAnimated); });
            }
            else
            {
                Assert.Equal(2, Assert.IsType<StickerBatchUpdateSortResultVo>(response.ResponseData).VoUpdatedCount);
                var rows = f.Db.Queryable<Sticker>().OrderBy(s => s.Id).ToList();
                Assert.Equal(new[] { 8, 4 }, rows.Select(s => s.Sort));
                Assert.All(rows, row => { Assert.Equal(UserId, row.ModifyId); Assert.Equal(Secret, row.ModifyBy); });
            }
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task LateWriteFailures_ShouldRollbackOnlyExistingTransactionalOperations(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var io in new[] { false, true })
        {
            using var f = new Fixture(operation)
            {
                FailedStage = operation == "group" ? "group.AddAsync.completed" : operation == "sort" ? "sticker.UpdateRangeAsync.completed" : "sticker.AddAsync.completed",
                Failure = io ? new IOException(Secret) : new UnsafeOperation(),
                FailOccurrence = operation == "batch" ? 2 : 1
            };
            var before = f.Snapshot();
            var status = io ? 500 : operation == "batch" ? 404 : 409;
            await InvokeApiAsync(f.CallAsync, status, environment);
            if (operation is "batch" or "sort") Assert.Equal(before, f.Snapshot());
            else
            {
                Assert.NotEqual(before, f.Snapshot());
                Assert.Equal(operation == "group" ? 2 : 1, f.Db.Queryable<StickerGroup>().Count());
                Assert.Equal(operation == "single" ? 3 : 2, f.Db.Queryable<Sticker>().Count());
            }
            Assert.Equal(0, f.Unit.TranCount);
            capture.AssertEvents((io ? "http.failed" : "sticker.request_failed", "Error", io ? "io" : "invalid-operation", io ? 500 : null));
            capture.Clear();
        }
        using var partial = new Fixture("sort") { PartialSort = true };
        var snapshot = partial.Snapshot();
        var result = await InvokeApiAsync(partial.CallAsync, 409, environment);
        Assert.Equal("排序写入未完整提交，请刷新后重试", result!.MessageInfo);
        Assert.Equal("StickerSortSnapshotStale", result.Code);
        Assert.Equal(snapshot, partial.Snapshot());
        capture.AssertEvents(("sticker.request_failed", "Error", "invalid-operation", null));
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task BatchItemFailures_ShouldKeepPartialResultsAndPriorAttachmentEffects(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var unreadable in new[] { false, true })
        {
            using var f = new Fixture("batch")
            {
                FailedStage = "attachment.QueryByIdAsync", FailOccurrence = 2,
                Failure = unreadable ? new UnreadableOperation() : new UnsafeOperation()
            };
            f.EnableThumbnails();
            var before = f.Snapshot();
            var result = await InvokeApiAsync(f.CallAsync, 500, environment);
            Assert.Equal(2, f.Db.Queryable<Sticker>().Count());
            Assert.Single(Directory.GetFiles(f.DirectoryPath, "*", SearchOption.AllDirectories));
            if (unreadable)
            {
                Assert.Null(result);
                Assert.Equal(before, f.Snapshot());
                capture.AssertEvents(("http.failed", "Error", "io", 500));
            }
            else
            {
                Assert.Equal("ImageProcessFailed", result!.Code);
                var batch = Assert.IsType<StickerBatchAddResultVo>(result.ResponseData);
                var failed = Assert.Single(batch.VoFailedItems);
                Assert.Equal(1, failed.VoRowIndex); Assert.Equal(Secret, failed.VoMessage);
                Assert.Equal(AttachmentId + 1, failed.VoAttachmentId);
                Assert.Equal(0, batch.VoCreatedCount); Assert.Empty(batch.VoStickerIds);
                Assert.False(string.IsNullOrEmpty(f.Db.Queryable<Attachment>().InSingle(AttachmentId).ThumbnailPath));
                capture.AssertEvents(("sticker.batch_item_failed", "Error", "invalid-operation", null));
            }
            Assert.Equal(0, f.Unit.TranCount);
            capture.Clear();
        }
        // 第一项失败后仍检查第二项；缺失附件保留逐项失败结果及既有 500。
        using var missing = new Fixture("batch");
        missing.Batch.Stickers[0].AttachmentId = 999;
        var response = await InvokeApiAsync(missing.CallAsync, 500, environment);
        var payload = Assert.IsType<StickerBatchAddResultVo>(response!.ResponseData);
        Assert.Equal("关联附件不存在或已删除", Assert.Single(payload.VoFailedItems).VoMessage);
        Assert.Equal(2, missing.Steps.Count(s => s == "attachment.QueryByIdAsync"));
        Assert.Equal(2, missing.Db.Queryable<Sticker>().Count());
        capture.AssertEvents(("sticker.batch_item_failed", "Error", "invalid-operation", null));
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ThumbnailDegradation_ShouldStaySafeAndKeepSaving(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var scenario in new[] { "result", "exists", "download", "path", "image", "write", "dispose", "result-dispose", "missing-file", "null-stream" })
        {
            using var f = new Fixture("single");
            f.EnableThumbnails();
            f.ThumbnailSuccess = scenario is not ("result" or "result-dispose");
            f.ThrowOnDispose = scenario is "dispose" or "result-dispose";
            f.FileExists = scenario != "missing-file"; f.NullStream = scenario == "null-stream";
            f.FailedStage = scenario switch
            {
                "exists" => "storage.exists", "download" => "storage.download", "path" => "storage.path",
                "image" => "image", "write" => "attachment.UpdateAsync", _ => null
            };
            f.Failure = new IOException(Secret);
            var result = await InvokeApiAsync(f.CallAsync, 200, environment);
            Assert.True(result!.IsSuccess); Assert.Equal(3, f.Db.Queryable<Sticker>().Count());
            if (scenario is "missing-file" or "null-stream") capture.AssertQuiet();
            else capture.AssertEvents(("sticker.thumbnail_failed", "Warning", scenario == "result" ? "other" : "io", null));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CacheFailures_ShouldKeepFallbackAndWriteSuccess(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "cache.read", "cache.write", "cache.remove" })
        {
            using var f = new Fixture("single") { FailedStage = stage, Failure = new HostileFailure() };
            if (stage == "cache.remove") Assert.True((await InvokeApiAsync(f.CallAsync, 200, environment))!.IsSuccess);
            else Assert.Single(await f.Service.GetGroupsAsync(0));
            capture.AssertEvents((stage switch
            {
                "cache.read" => "sticker.cache_read_failed", "cache.write" => "sticker.cache_write_failed", _ => "sticker.cache_invalidation_failed"
            }, "Warning", "other", null));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task BatchAndSortPrechecks_ShouldKeepLimitsResultsAndEmptySemantics(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var scenario in new[] { "null", "empty", "limit", "attachment", "code", "name", "existing" })
        {
            using var f = new Fixture("batch");
            if (scenario == "null") f.Batch.Stickers = null!;
            if (scenario == "empty") f.Batch.Stickers.Clear();
            if (scenario == "limit") f.Batch.Stickers = Enumerable.Repeat(f.Batch.Stickers[0], 51).ToList();
            if (scenario == "attachment") f.Batch.Stickers[0].AttachmentId = 0;
            if (scenario == "code") f.Batch.Stickers[0].Code = "!";
            if (scenario == "name") f.Batch.Stickers[0].Name = " ";
            if (scenario == "existing") f.Batch.Stickers[0].Code = "existing_1";
            var before = f.Snapshot();
            var result = await InvokeApiAsync(f.CallAsync, scenario is "null" or "empty" or "limit" ? 400 : 409, environment);
            if (result!.StatusCode == 409)
            {
                var payload = Assert.IsType<StickerBatchAddResultVo>(result.ResponseData);
                Assert.Single(payload.VoConflicts); Assert.Empty(payload.VoStickerIds); Assert.Empty(payload.VoFailedItems);
                Assert.Equal("BatchCodeConflict", result.Code);
            }
            Assert.Equal(before, f.Snapshot());
            Assert.DoesNotContain("attachment.QueryByIdAsync", f.Steps);
            capture.AssertQuiet();
        }
        foreach (var scenario in new[] { "null", "empty", "invalid", "duplicate", "cross-group", "deleted-group" })
        {
            using var f = new Fixture("sort");
            if (scenario == "null") f.Sort.Items = null!;
            if (scenario == "empty") f.Sort.Items.Clear();
            if (scenario == "invalid") f.Sort.Items[0].Id = 0;
            if (scenario == "duplicate") f.Sort.Items[1].Id = 91;
            if (scenario == "cross-group") f.Db.Updateable<Sticker>().SetColumns(s => s.GroupId == 999).Where(s => s.Id == 92).ExecuteCommand();
            if (scenario == "deleted-group") f.Db.Updateable<StickerGroup>().SetColumns(g => g.IsDeleted == true).Where(g => g.Id == GroupId).ExecuteCommand();
            var before = f.Snapshot();
            var status = scenario is "null" or "empty" ? 200 : scenario is "invalid" or "duplicate" ? 400 : 409;
            var result = await InvokeApiAsync(f.CallAsync, status, environment);
            if (status == 200) Assert.Equal(0, Assert.IsType<StickerBatchUpdateSortResultVo>(result!.ResponseData).VoUpdatedCount);
            Assert.Equal(before, f.Snapshot());
            Assert.DoesNotContain("sticker.UpdateRangeAsync", f.Steps);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DirectDependencyStages_ShouldKeepConsumedAndPropagatedOwnership(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var (operation, stage) in new[]
        {
            ("single", "sticker.QueryExistsAsync"), ("single", "attachment.QueryByIdAsync"),
            ("batch", "sticker.QueryAsync"), ("sort", "sticker.QueryByIdsAsync")
        })
        {
            using var f = new Fixture(operation) { FailedStage = stage, Failure = new UnsafeArgument() };
            var before = f.Snapshot();
            var response = await InvokeApiAsync(f.CallAsync, 400, environment);
            Assert.Equal(Secret, response!.MessageInfo);
            Assert.Equal(before, f.Snapshot());
            capture.AssertEvents(("sticker.request_failed", "Error", "argument", null));
            capture.Clear();
        }
        foreach (var operation in Operations)
        foreach (var aggregate in new[] { false, true })
        {
            using var f = new Fixture(operation)
            {
                FailedStage = operation == "group" ? "group.QueryExistsAsync" : "group.QueryByIdAsync",
                Failure = aggregate ? new AggregateException(new IOException(Secret)) : new BusinessException(Secret, 503, "Private.Code")
            };
            var response = await InvokeApiAsync(f.CallAsync, aggregate ? 500 : 503, environment);
            Assert.Null(response);
            // 原 TranAop 只解包单一聚合异常，非事务入口仍传播 AggregateException。
            var kind = aggregate ? operation is "batch" or "sort" ? "io" : "aggregate" : "other";
            capture.AssertEvents(("http.failed", "Error", kind, aggregate ? 500 : 503));
            capture.Clear();
        }
        foreach (var operation in new[] { "batch", "sort" })
        {
            using var f = new Fixture(operation) { FailedStage = "group.QueryByIdAsync", Failure = new UnsafeArgument() };
            f.Localizer.Setup(l => l["error.common.validation_failed"]).Returns(new LocalizedString("error.common.validation_failed", "localized reply"));
            Assert.Equal("localized reply", (await InvokeApiAsync(f.CallAsync, 400, environment))!.MessageInfo);
            capture.AssertEvents(("sticker.request_failed", "Error", "argument", null));
            capture.Clear();
        }
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly IMapper Mapper = new MapperConfiguration(c => c.AddProfile<ForumProfile>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly string _operation;
        private readonly ILoggerFactory _logs;
        private readonly Dictionary<string, int> _calls = [];
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "radish-sticker-logging-" + Guid.NewGuid().ToString("N"));
        public SqlSugarScope Db { get; }
        public UnitOfWorkManage Unit { get; }
        public StickerController Controller { get; }
        public IStickerService Service { get; }
        public Mock<IStringLocalizer<Errors>> Localizer { get; } = new();
        public List<string> Steps { get; } = [];
        public string? FailedStage { get; set; }
        public int FailOccurrence { get; set; } = 1;
        public Exception Failure { get; set; } = new UnsafeOperation();
        public bool PartialSort { get; set; }
        public bool ThumbnailSuccess { get; set; } = true;
        public bool ThrowOnDispose { get; set; }
        public bool FileExists { get; set; } = true;
        public bool NullStream { get; set; }
        public CreateStickerGroupDto Group { get; } = new()
        {
            Code = " NEW_GROUP ", Name = "  " + Secret + "  ", Description = "  " + Secret + "  ",
            CoverAttachmentId = AttachmentId, Sort = 17, IsEnabled = false
        };
        public CreateStickerDto Single { get; } = new()
        {
            GroupId = GroupId, Code = " PRIVATE_STICKER ", Name = "  " + Secret + "  ",
            AttachmentId = AttachmentId, AllowInline = false, Sort = 17
        };
        public BatchAddStickersDto Batch { get; } = new()
        {
            GroupId = GroupId,
            Stickers = [new() { Code = " private_sticker ", Name = Secret, AttachmentId = AttachmentId },
                new() { Code = "private_sticker_2", Name = Secret, AttachmentId = AttachmentId + 1 }]
        };
        public BatchUpdateStickerSortDto Sort { get; } = new()
        {
            GroupId = GroupId, Items = [new() { Id = 91, Sort = 8 }, new() { Id = 92, Sort = 4 }]
        };

        public Fixture(string operation)
        {
            _operation = operation;
            new ServiceCollection().ConfigureApplication();
            Db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", DbType = DbType.Sqlite, ConnectionString = "Data Source=:memory:",
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            Db.CodeFirst.InitTables<StickerGroup, Sticker, Attachment>();
            Unit = new UnitOfWorkManage(Db, NullLogger<UnitOfWorkManage>.Instance);
            Db.Insertable(new StickerGroup { Id = GroupId, Code = "private_group", Name = Secret }).ExecuteCommand();
            Db.Insertable(new List<Sticker>
            {
                new() { Id = 91, GroupId = GroupId, Code = "existing_1", Name = Secret, Sort = 0, AttachmentId = AttachmentId },
                new() { Id = 92, GroupId = GroupId, Code = "existing_2", Name = Secret, Sort = 1, AttachmentId = AttachmentId + 1 }
            }).ExecuteCommand();
            Db.Insertable(new List<Attachment>
            {
                new() { Id = AttachmentId, StoragePath = Secret + "/first.gif", ThumbnailPath = "existing.jpg", Extension = ".gif" },
                new() { Id = AttachmentId + 1, StoragePath = Secret + "/second.gif", ThumbnailPath = "existing.jpg", MimeType = "image/gif" }
            }).ExecuteCommand();
            var proxy = new ProxyGenerator();
            var groupRepository = proxy.CreateInterfaceProxyWithTarget<IBaseRepository<StickerGroup>>(new BaseRepository<StickerGroup>(Unit), new RepositoryInterceptor(this, "group"));
            var stickerRepository = proxy.CreateInterfaceProxyWithTarget<IBaseRepository<Sticker>>(new BaseRepository<Sticker>(Unit), new RepositoryInterceptor(this, "sticker"));
            var attachmentRepository = proxy.CreateInterfaceProxyWithTarget<IBaseRepository<Attachment>>(new BaseRepository<Attachment>(Unit), new RepositoryInterceptor(this, "attachment"));
            var cache = new Mock<ICaching>(MockBehavior.Strict);
            cache.Setup(c => c.GetAsync<List<StickerGroupVo>>(It.IsAny<string>())).Returns(() =>
            {
                Step("cache.read"); return Task.FromResult<List<StickerGroupVo>>(null!);
            });
            cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<List<StickerGroupVo>>(), It.IsAny<TimeSpan>())).Returns(() =>
            {
                Step("cache.write"); return Task.CompletedTask;
            });
            cache.Setup(c => c.RemoveAsync(It.IsAny<string>())).Returns(() =>
            {
                Step("cache.remove"); return Task.CompletedTask;
            });
            var storage = new Mock<IFileStorage>(MockBehavior.Strict);
            storage.Setup(s => s.ExistsAsync(It.IsAny<string>())).Returns(() =>
            {
                Step("storage.exists"); return Task.FromResult(FileExists);
            });
            storage.Setup(s => s.DownloadAsync(It.IsAny<string>())).Returns(() =>
            {
                Step("storage.download"); return Task.FromResult<Stream?>(NullStream ? null : new ThumbnailStream(ThrowOnDispose));
            });
            storage.Setup(s => s.GetFullPath(It.IsAny<string>())).Returns((string path) =>
            {
                Step("storage.path"); return Path.Combine(DirectoryPath, path);
            });
            var processor = new Mock<IImageProcessor>(MockBehavior.Strict);
            processor.Setup(p => p.GenerateThumbnailAsync(It.IsAny<Stream>(), It.IsAny<string>(), 96, 96, 85))
                .Returns((Stream stream, string path, int width, int height, int quality) =>
                {
                    Step("image");
                    if (!ThumbnailSuccess) return Task.FromResult(ImageProcessResult.Fail(Secret));
                    // 仅模拟文件副作用，不能作为真实图片处理验收。
                    File.WriteAllText(path, "thumbnail fixture");
                    return Task.FromResult(ImageProcessResult.Ok(path, 17, 96, 96));
                });
            _logs = LoggerFactory.Create(b => b.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddSerilog(Log.Logger, dispose: false));
            Service = proxy.CreateInterfaceProxyWithTarget<IStickerService>(new StickerService(Mapper, groupRepository,
                stickerRepository, attachmentRepository, cache.Object, storage.Object, processor.Object,
                _logs.CreateLogger<StickerService>(), new Mock<IAttachmentUrlResolver>().Object), new TranAop(Unit));
            var accessor = new Mock<ICurrentUserAccessor>();
            accessor.SetupGet(a => a.Current).Returns(new CurrentUser { UserId = UserId, UserName = Secret, IsAuthenticated = true });
            Localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, true));
            Controller = new StickerController(Service, accessor.Object, Localizer.Object);
        }
        private void Step(string stage)
        {
            Steps.Add(stage);
            _calls.TryGetValue(stage, out var count);
            _calls[stage] = ++count;
            if (stage == FailedStage && count == FailOccurrence) throw Failure;
        }
        private sealed class RepositoryInterceptor(Fixture fixture, string owner) : IInterceptor
        {
            public void Intercept(Castle.DynamicProxy.IInvocation invocation)
            {
                var stage = owner + "." + invocation.Method.Name;
                fixture.Step(stage);
                if (stage == "sticker.UpdateRangeAsync" && fixture.PartialSort)
                    invocation.SetArgumentValue(0, ((List<Sticker>)invocation.Arguments[0]).Take(1).ToList());
                invocation.Proceed();
                if (invocation.ReturnValue is Task<long> id) invocation.ReturnValue = fixture.AfterAsync(id, stage);
                else if (invocation.ReturnValue is Task<int> affected) invocation.ReturnValue = fixture.AfterAsync(affected, stage);
                else if (invocation.ReturnValue is Task<bool> result) invocation.ReturnValue = fixture.AfterAsync(result, stage);
            }
        }
        private async Task<T> AfterAsync<T>(Task<T> pending, string stage)
        {
            var result = await pending;
            Step(stage + ".completed");
            return result;
        }
        public void EnableThumbnails() => Db.Updateable<Attachment>().SetColumns(a => a.ThumbnailPath == "").Where(a => a.Id > 0).ExecuteCommand();
        public Task<MessageModel> CallAsync() => _operation switch
        {
            "group" => Controller.CreateGroup(Group), "single" => Controller.AddSticker(Single),
            "batch" => Controller.BatchAddStickers(Batch), "sort" => Controller.BatchUpdateSort(Sort),
            _ => throw new InvalidOperationException()
        };
        public string Snapshot() => JsonSerializer.Serialize(new
        {
            Groups = Db.Queryable<StickerGroup>().OrderBy(g => g.Id).ToList(),
            Stickers = Db.Queryable<Sticker>().OrderBy(s => s.Id).ToList(),
            Attachments = Db.Queryable<Attachment>().OrderBy(a => a.Id).ToList()
        });
        public void Dispose()
        {
            Db.Dispose(); _logs.Dispose();
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
        }
    }
    private sealed class ThumbnailStream(bool throwOnDispose) : MemoryStream
    {
        public override ValueTask DisposeAsync()
        {
            base.Dispose();
            return throwOnDispose ? ValueTask.FromException(new IOException(Secret)) : ValueTask.CompletedTask;
        }
    }

    private sealed class UnsafeOperation : InvalidOperationException
    {
        public UnsafeOperation() : base(Secret, new IOException(Secret)) => Data[Secret] = Secret;
        public override string ToString() => throw new Exception("Must not read exception text");
    }
    private sealed class UnsafeArgument : ArgumentException
    {
        public UnsafeArgument() : base(Secret) => Data[Secret] = Secret;
        public override string ToString() => throw new Exception("Must not read exception text");
    }
    private sealed class UnreadableOperation : InvalidOperationException
    {
        public override string Message => throw new IOException(Secret);
    }
    private sealed class UnreadableArgument : ArgumentException
    {
        public override string Message => throw new IOException(Secret);
    }
    private sealed class HostileFailure : Exception
    {
        public override string Message => throw new Exception("Must not read exception message");
        public override string ToString() => throw new Exception("Must not read exception text");
    }

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
        context.Request.Path = "/api/v1/Sticker/BatchAddStickers";
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
            foreach (var forbidden in new[] { Secret, "private_group", "private_sticker", GroupId.ToString(), UserId.ToString(), AttachmentId.ToString(),
                "Private.Code", "Private.Key", "runtime.unclassified", "UNIQUE constraint", "System.IO.IOException", "idx_sticker", "CodeAlreadyExists", "cacheKey", "StoragePath" })
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
