#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>
/// 固定文档同步回归测试
/// </summary>
public class WikiDocumentBuiltInSyncTests
{
    [Fact(DisplayName = "固定文档同步应生成目录节点并重写内链与资源路径")]
    public async Task SyncBuiltInDocumentsAsync_ShouldGenerateDirectoryNodes_AndRewriteLinks()
    {
        var docsRoot = CreateTempDocsRoot();
        try
        {
            WriteFile(Path.Combine(docsRoot, "guide", "getting-started.md"), """
                # 快速开始

                先看 [配置说明](/guide/configuration)，再看 [架构总览](../architecture/overview.md)。

                ![Logo](../images/logo.png)
                """);
            WriteFile(Path.Combine(docsRoot, "guide", "configuration.md"), """
                # 配置说明

                这里是配置说明。
                """);
            WriteFile(Path.Combine(docsRoot, "architecture", "overview.md"), """
                # 架构总览

                这里是架构概览。
                """);
            WriteBinaryFile(Path.Combine(docsRoot, "images", "logo.png"), new byte[] { 1, 2, 3, 4 });

            var testContext = new WikiDocumentSyncTestContext();
            var service = testContext.CreateService(docsRoot);

            var summary = await service.SyncBuiltInDocumentsAsync(TestContext.Current.CancellationToken);

            summary.IsSkipped.ShouldBeFalse();
            summary.MarkdownFileCount.ShouldBe(3);
            summary.DescriptorCount.ShouldBe(5);
            summary.GeneratedNodeCount.ShouldBe(2);
            summary.SyncedCount.ShouldBe(5);
            summary.CreatedCount.ShouldBe(5);
            summary.UpdatedCount.ShouldBe(0);
            summary.RestoredCount.ShouldBe(0);
            summary.ParentAdjustedCount.ShouldBe(3);
            summary.SoftDeletedCount.ShouldBe(0);
            summary.SkippedCount.ShouldBe(0);

            testContext.Documents.Count.ShouldBe(5);
            testContext.Revisions.Count.ShouldBe(5);

            var guide = testContext.Documents.Single(d => d.Slug == "guide");
            var architecture = testContext.Documents.Single(d => d.Slug == "architecture");
            var gettingStarted = testContext.Documents.Single(d => d.Slug == "guide-getting-started");
            var configuration = testContext.Documents.Single(d => d.Slug == "guide-configuration");
            var overview = testContext.Documents.Single(d => d.Slug == "architecture-overview");

            guide.Title.ShouldBe("开发指南");
            guide.SourceType.ShouldBe("BuiltIn");
            guide.SourcePath.ShouldBe("Docs/guide/");
            guide.MarkdownContent.ShouldContain("/__documents__/guide-configuration");
            guide.MarkdownContent.ShouldContain("/__documents__/guide-getting-started");

            architecture.Title.ShouldBe("架构设计");
            architecture.SourcePath.ShouldBe("Docs/architecture/");

            gettingStarted.ParentId.ShouldBe(guide.Id);
            configuration.ParentId.ShouldBe(guide.Id);
            overview.ParentId.ShouldBe(architecture.Id);

            new[] { configuration.Sort, gettingStarted.Sort }.Order().ShouldBe([10, 20]);
            gettingStarted.SourcePath.ShouldBe("Docs/guide/getting-started.md");
            gettingStarted.MarkdownContent.ShouldContain("/__documents__/guide-configuration");
            gettingStarted.MarkdownContent.ShouldContain("/__documents__/architecture-overview");
            gettingStarted.MarkdownContent.ShouldContain("/docs-assets/images/logo.png");

            testContext.Revisions.ShouldAllBe(r =>
                r.SourceType == "BuiltIn" &&
                r.Version == 1 &&
                testContext.Documents.Any(d => d.Id == r.DocumentId));
        }
        finally
        {
            DeleteDirectory(docsRoot);
        }
    }

    [Fact(DisplayName = "关闭固定文档展示时不应执行同步或暴露内置文档")]
    public async Task SyncBuiltInDocumentsAsync_ShouldSkip_WhenBuiltInDocsDisabled()
    {
        var docsRoot = CreateTempDocsRoot();
        try
        {
            WriteFile(Path.Combine(docsRoot, "guide", "getting-started.md"), """
                # 快速开始

                这篇文档不应被同步。
                """);

            var builtInDocument = new WikiDocument
            {
                Id = 1,
                Title = "已有固定文档",
                Slug = "guide-getting-started",
                MarkdownContent = "旧内容",
                Status = 1,
                SourceType = "BuiltIn",
                SourcePath = "Docs/guide/getting-started.md",
                Version = 1,
                IsDeleted = false,
                TenantId = 0,
                CreateBy = "System",
                CreateId = 0,
                CreateTime = DateTime.Now.AddDays(-1)
            };

            var testContext = new WikiDocumentSyncTestContext([builtInDocument]);
            var service = testContext.CreateService(docsRoot, showBuiltInDocs: false);

            var summary = await service.SyncBuiltInDocumentsAsync(TestContext.Current.CancellationToken);

            summary.IsSkipped.ShouldBeTrue();
            summary.SkipReason.ShouldBe("Document.ShowBuiltInDocs=false");
            summary.SyncedCount.ShouldBe(0);
            summary.CreatedCount.ShouldBe(0);
            summary.UpdatedCount.ShouldBe(0);
            summary.RestoredCount.ShouldBe(0);
            summary.ParentAdjustedCount.ShouldBe(0);
            summary.SoftDeletedCount.ShouldBe(0);

            testContext.Documents.ShouldHaveSingleItem();
            testContext.Documents[0].Title.ShouldBe("已有固定文档");
            testContext.Documents[0].MarkdownContent.ShouldBe("旧内容");
            testContext.Revisions.ShouldBeEmpty();

            var detail = await service.GetDetailAsync(builtInDocument.Id, includeUnpublished: true);
            var detailBySlug = await service.GetBySlugAsync(builtInDocument.Slug, includeUnpublished: true);

            detail.ShouldBeNull();
            detailBySlug.ShouldBeNull();
        }
        finally
        {
            DeleteDirectory(docsRoot);
        }
    }

    [Fact(DisplayName = "固定文档同步应恢复已有固定文档并软删除已移除文档")]
    public async Task SyncBuiltInDocumentsAsync_ShouldRestoreExistingDocument_AndSoftDeleteRemovedOnes()
    {
        var docsRoot = CreateTempDocsRoot();
        try
        {
            WriteFile(Path.Combine(docsRoot, "guide", "getting-started.md"), """
                # 快速开始

                已恢复内容。
                """);

            var existingDocument = new WikiDocument
            {
                Id = 10,
                Title = "旧标题",
                Slug = "guide-getting-started",
                Summary = "旧摘要",
                MarkdownContent = "旧内容",
                ParentId = 999,
                Sort = 999,
                Status = 0,
                SourceType = "BuiltIn",
                SourcePath = "Docs/old.md",
                Version = 3,
                IsDeleted = true,
                DeletedBy = "Tester",
                DeletedAt = DateTime.Now.AddDays(-1),
                TenantId = 0,
                CreateBy = "System",
                CreateId = 0,
                CreateTime = DateTime.Now.AddDays(-2)
            };

            var obsoleteDocument = new WikiDocument
            {
                Id = 11,
                Title = "已废弃文档",
                Slug = "obsolete-doc",
                MarkdownContent = "应被软删除",
                Status = 1,
                SourceType = "BuiltIn",
                SourcePath = "Docs/obsolete.md",
                Version = 1,
                IsDeleted = false,
                TenantId = 0,
                CreateBy = "System",
                CreateId = 0,
                CreateTime = DateTime.Now.AddDays(-2)
            };

            var existingRevision = new WikiDocumentRevision
            {
                Id = 100,
                DocumentId = existingDocument.Id,
                Version = 1,
                Title = "旧标题",
                MarkdownContent = "旧内容",
                ChangeSummary = "旧同步",
                SourceType = "BuiltIn",
                TenantId = 0,
                CreateBy = "System",
                CreateId = 0,
                CreateTime = DateTime.Now.AddDays(-2)
            };

            var testContext = new WikiDocumentSyncTestContext(
                [existingDocument, obsoleteDocument],
                [existingRevision]);
            var service = testContext.CreateService(docsRoot);

            var summary = await service.SyncBuiltInDocumentsAsync(TestContext.Current.CancellationToken);

            summary.IsSkipped.ShouldBeFalse();
            summary.MarkdownFileCount.ShouldBe(1);
            summary.DescriptorCount.ShouldBe(2);
            summary.GeneratedNodeCount.ShouldBe(1);
            summary.SyncedCount.ShouldBe(2);
            summary.CreatedCount.ShouldBe(1);
            summary.UpdatedCount.ShouldBe(1);
            summary.RestoredCount.ShouldBe(1);
            summary.ParentAdjustedCount.ShouldBe(1);
            summary.SoftDeletedCount.ShouldBe(1);
            summary.SkippedCount.ShouldBe(0);

            var guide = testContext.Documents.Single(d => d.Slug == "guide");
            var restored = testContext.Documents.Single(d => d.Id == existingDocument.Id);
            var obsolete = testContext.Documents.Single(d => d.Id == obsoleteDocument.Id);
            var restoredRevision = testContext.Revisions.Single(r => r.DocumentId == restored.Id && r.Version == 1);

            restored.IsDeleted.ShouldBeFalse();
            restored.Title.ShouldBe("快速开始");
            restored.SourcePath.ShouldBe("Docs/guide/getting-started.md");
            restored.ParentId.ShouldBe(guide.Id);
            restored.Version.ShouldBe(1);
            restored.MarkdownContent.ShouldContain("已恢复内容");

            restoredRevision.Title.ShouldBe("快速开始");
            restoredRevision.MarkdownContent.ShouldContain("已恢复内容");
            restoredRevision.ChangeSummary.ShouldBe("固定文档同步");

            obsolete.IsDeleted.ShouldBeTrue();
            obsolete.DeletedBy.ShouldBe("System");
        }
        finally
        {
            DeleteDirectory(docsRoot);
        }
    }

    private static string CreateTempDocsRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "radish-built-in-docs-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void WriteFile(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content.Replace("\r\n", "\n"));
    }

    private static void WriteBinaryFile(string path, byte[] content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, content);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class WikiDocumentSyncTestContext
    {
        private readonly long _initialDocumentId;
        private readonly long _initialRevisionId;

        public WikiDocumentSyncTestContext(
            List<WikiDocument>? documents = null,
            List<WikiDocumentRevision>? revisions = null)
        {
            Documents = documents ?? [];
            Revisions = revisions ?? [];
            _initialDocumentId = Documents.Count == 0 ? 0 : Documents.Max(d => d.Id);
            _initialRevisionId = Revisions.Count == 0 ? 0 : Revisions.Max(r => r.Id);

            DocumentRepository = CreateDocumentRepositoryMock();
            RevisionRepository = CreateRevisionRepositoryMock();
        }

        public List<WikiDocument> Documents { get; }

        public List<WikiDocumentRevision> Revisions { get; }

        public Mock<IWikiDocumentRepository> DocumentRepository { get; }

        public Mock<IBaseRepository<WikiDocumentRevision>> RevisionRepository { get; }

        public WikiDocumentService CreateService(string docsRoot, bool showBuiltInDocs = true)
        {
            var mapper = CreateMapperMock();
            return new WikiDocumentService(
                mapper.Object,
                DocumentRepository.Object,
                RevisionRepository.Object,
                Options.Create(new DocumentOptions
                {
                    ShowBuiltInDocs = showBuiltInDocs,
                    BuiltInDocsPath = docsRoot,
                    StaticAssetsRequestPath = "/docs-assets"
                }));
        }

        private Mock<IMapper> CreateMapperMock()
        {
            var mapper = new Mock<IMapper>();
            mapper
                .Setup(m => m.Map<List<WikiDocumentVo>>(It.IsAny<List<WikiDocument>>()))
                .Returns<List<WikiDocument>>(documents => documents.Select(document => new WikiDocumentVo
                {
                    VoId = document.Id,
                    VoTitle = document.Title,
                    VoSlug = document.Slug,
                    VoSummary = document.Summary,
                    VoParentId = document.ParentId,
                    VoSort = document.Sort,
                    VoStatus = document.Status,
                    VoVersion = document.Version,
                    VoSourceType = document.SourceType,
                    VoSourcePath = document.SourcePath,
                    VoCreateTime = document.CreateTime,
                    VoModifyTime = document.ModifyTime,
                    VoPublishedAt = document.PublishedAt
                }).ToList());
            mapper
                .Setup(m => m.Map<WikiDocumentDetailVo>(It.IsAny<WikiDocument>()))
                .Returns<WikiDocument>(document => new WikiDocumentDetailVo
                {
                    VoId = document.Id,
                    VoTitle = document.Title,
                    VoSlug = document.Slug,
                    VoSummary = document.Summary,
                    VoMarkdownContent = document.MarkdownContent,
                    VoParentId = document.ParentId,
                    VoSort = document.Sort,
                    VoStatus = document.Status,
                    VoVersion = document.Version,
                    VoSourceType = document.SourceType,
                    VoSourcePath = document.SourcePath,
                    VoCreateTime = document.CreateTime,
                    VoModifyTime = document.ModifyTime,
                    VoPublishedAt = document.PublishedAt
                });
            return mapper;
        }

        private Mock<IWikiDocumentRepository> CreateDocumentRepositoryMock()
        {
            var nextId = _initialDocumentId;
            var repository = new Mock<IWikiDocumentRepository>();

            repository
                .Setup(r => r.RestoreAsync(It.IsAny<Expression<Func<WikiDocument, bool>>>() ))
                .Returns<Expression<Func<WikiDocument, bool>>>(expression =>
                {
                    var predicate = expression.Compile();
                    var count = 0;
                    foreach (var document in Documents.Where(predicate))
                    {
                        if (!document.IsDeleted)
                        {
                            continue;
                        }

                        document.IsDeleted = false;
                        document.DeletedAt = null;
                        document.DeletedBy = null;
                        count++;
                    }

                    return Task.FromResult(count);
                });

            repository
                .Setup(r => r.QueryByIdIncludingDeletedAsync(It.IsAny<long>()))
                .Returns<long>(id => Task.FromResult(Documents.FirstOrDefault(d => d.Id == id)));

            repository
                .Setup(r => r.QueryByIdAsync(It.IsAny<long>()))
                .Returns<long>(id => Task.FromResult(Documents.FirstOrDefault(d => d.Id == id && !d.IsDeleted)));

            repository
                .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<WikiDocument, bool>>?>()))
                .Returns<Expression<Func<WikiDocument, bool>>?>(expression =>
                {
                    var result = expression == null
                        ? Documents.FirstOrDefault()
                        : Documents.FirstOrDefault(expression.Compile());
                    return Task.FromResult(result);
                });

            repository
                .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<WikiDocument, bool>>?>()))
                .Returns<Expression<Func<WikiDocument, bool>>?>(expression =>
                {
                    var result = expression == null
                        ? Documents.ToList()
                        : Documents.Where(expression.Compile()).ToList();
                    return Task.FromResult(result);
                });

            repository
                .Setup(r => r.QueryWithOrderAsync(
                    It.IsAny<Expression<Func<WikiDocument, bool>>?>(),
                    It.IsAny<Expression<Func<WikiDocument, object>>>(),
                    It.IsAny<SqlSugar.OrderByType>(),
                    It.IsAny<int>()))
                .Returns<Expression<Func<WikiDocument, bool>>?, Expression<Func<WikiDocument, object>>, SqlSugar.OrderByType, int>((whereExpression, _, _, take) =>
                {
                    var query = whereExpression == null ? Documents.AsEnumerable() : Documents.Where(whereExpression.Compile());
                    var result = query.ToList();
                    if (take > 0)
                    {
                        result = result.Take(take).ToList();
                    }

                    return Task.FromResult(result);
                });

            repository
                .Setup(r => r.QueryPageAsync(
                    It.IsAny<Expression<Func<WikiDocument, bool>>?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<Expression<Func<WikiDocument, object>>?>(),
                    It.IsAny<SqlSugar.OrderByType>()))
                .Returns<Expression<Func<WikiDocument, bool>>?, int, int, Expression<Func<WikiDocument, object>>?, SqlSugar.OrderByType>((whereExpression, pageIndex, pageSize, _, _) =>
                {
                    var query = whereExpression == null ? Documents.AsEnumerable() : Documents.Where(whereExpression.Compile());
                    var filtered = query.ToList();
                    var paged = filtered.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
                    return Task.FromResult((paged, filtered.Count));
                });

            repository
                .Setup(r => r.QueryPageAsync(
                    It.IsAny<Expression<Func<WikiDocument, bool>>?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<Expression<Func<WikiDocument, object>>?>(),
                    It.IsAny<SqlSugar.OrderByType>(),
                    It.IsAny<Expression<Func<WikiDocument, object>>?>(),
                    It.IsAny<SqlSugar.OrderByType>()))
                .Returns<Expression<Func<WikiDocument, bool>>?, int, int, Expression<Func<WikiDocument, object>>?, SqlSugar.OrderByType, Expression<Func<WikiDocument, object>>?, SqlSugar.OrderByType>((whereExpression, pageIndex, pageSize, _, _, _, _) =>
                {
                    var query = whereExpression == null ? Documents.AsEnumerable() : Documents.Where(whereExpression.Compile());
                    var filtered = query.ToList();
                    var paged = filtered.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
                    return Task.FromResult((paged, filtered.Count));
                });

            repository
                .Setup(r => r.AddAsync(It.IsAny<WikiDocument>()))
                .Returns<WikiDocument>(document =>
                {
                    document.Id = ++nextId;
                    Documents.Add(document);
                    return Task.FromResult(document.Id);
                });

            repository
                .Setup(r => r.UpdateAsync(It.IsAny<WikiDocument>()))
                .ReturnsAsync(true);

            repository
                .Setup(r => r.SoftDeleteByIdAsync(It.IsAny<long>(), It.IsAny<string?>()))
                .Returns<long, string?>((id, deletedBy) =>
                {
                    var document = Documents.FirstOrDefault(d => d.Id == id);
                    if (document == null)
                    {
                        return Task.FromResult(false);
                    }

                    document.IsDeleted = true;
                    document.DeletedBy = deletedBy;
                    document.DeletedAt = DateTime.Now;
                    return Task.FromResult(true);
                });

            return repository;
        }

        private Mock<IBaseRepository<WikiDocumentRevision>> CreateRevisionRepositoryMock()
        {
            var nextId = _initialRevisionId;
            var repository = new Mock<IBaseRepository<WikiDocumentRevision>>();

            repository
                .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<WikiDocumentRevision, bool>>?>()))
                .Returns<Expression<Func<WikiDocumentRevision, bool>>?>(expression =>
                {
                    var result = expression == null
                        ? Revisions.FirstOrDefault()
                        : Revisions.FirstOrDefault(expression.Compile());
                    return Task.FromResult(result);
                });

            repository
                .Setup(r => r.AddAsync(It.IsAny<WikiDocumentRevision>()))
                .Returns<WikiDocumentRevision>(revision =>
                {
                    revision.Id = ++nextId;
                    Revisions.Add(revision);
                    return Task.FromResult(revision.Id);
                });

            repository
                .Setup(r => r.UpdateAsync(It.IsAny<WikiDocumentRevision>()))
                .ReturnsAsync(true);

            return repository;
        }
    }
}
