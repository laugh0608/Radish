#nullable enable

using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>
/// 文档管理行为回归测试。
/// </summary>
public class WikiDocumentManagementServiceTests
{
    [Fact(DisplayName = "删除文档前应阻止仍有子文档的节点")]
    public async Task DeleteDocumentAsync_ShouldThrow_WhenChildrenExist()
    {
        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(r => r.QueryByIdAsync(10)).ReturnsAsync(new WikiDocument
        {
            Id = 10,
            Title = "父文档",
            Slug = "parent",
            SourceType = "Custom",
            Status = 0,
            IsDeleted = false
        });
        repository.Setup(r => r.QueryExistsAsync(It.IsAny<Expression<Func<WikiDocument, bool>>>())).ReturnsAsync(true);

        var service = CreateService(repository);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => service.DeleteDocumentAsync(10, 1, "Tester"));
        exception.Message.ShouldBe("请先处理子文档后再删除当前文档");
    }

    [Fact(DisplayName = "恢复固定文档应被拒绝")]
    public async Task RestoreDocumentAsync_ShouldThrow_WhenDocumentIsBuiltIn()
    {
        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(r => r.QueryByIdIncludingDeletedAsync(11)).ReturnsAsync(new WikiDocument
        {
            Id = 11,
            Title = "固定文档",
            Slug = "built-in-doc",
            SourceType = "BuiltIn",
            Status = 1,
            IsDeleted = true
        });

        var service = CreateService(repository);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => service.RestoreDocumentAsync(11, 1, "Tester"));
        exception.Message.ShouldBe("固定文档为只读内容，请修改 Docs 目录中的源文件");
    }

    [Fact(DisplayName = "更新文档时父级不能设置为自身子孙节点")]
    public async Task UpdateDocumentAsync_ShouldThrow_WhenParentIsDescendant()
    {
        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(r => r.QueryByIdAsync(1)).ReturnsAsync(new WikiDocument
        {
            Id = 1,
            Title = "根文档",
            Slug = "root",
            MarkdownContent = "old",
            SourceType = "Custom",
            ParentId = null,
            Sort = 0,
            Status = 0,
            Version = 1,
            IsDeleted = false
        });
        repository.Setup(r => r.QueryByIdAsync(3)).ReturnsAsync(new WikiDocument
        {
            Id = 3,
            Title = "孙节点",
            Slug = "child",
            MarkdownContent = "child",
            SourceType = "Custom",
            ParentId = 2,
            Sort = 0,
            Status = 0,
            Version = 1,
            IsDeleted = false
        });
        repository.Setup(r => r.QueryByIdAsync(2)).ReturnsAsync(new WikiDocument
        {
            Id = 2,
            Title = "子节点",
            Slug = "middle",
            MarkdownContent = "middle",
            SourceType = "Custom",
            ParentId = 1,
            Sort = 0,
            Status = 0,
            Version = 1,
            IsDeleted = false
        });
        repository.Setup(r => r.QueryExistsAsync(It.IsAny<Expression<Func<WikiDocument, bool>>>())).ReturnsAsync(false);

        var service = CreateService(repository);
        var dto = new UpdateWikiDocumentDto
        {
            Title = "根文档",
            MarkdownContent = "new",
            ParentId = 3,
            Sort = 10
        };

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => service.UpdateDocumentAsync(1, dto, 1, "Tester"));
        exception.Message.ShouldBe("父级文档不能设置为当前文档的子孙节点");
    }

    private static WikiDocumentService CreateService(Mock<IWikiDocumentRepository> wikiDocumentRepository)
    {
        var mapper = new Mock<IMapper>();
        var revisionRepository = new Mock<IBaseRepository<WikiDocumentRevision>>();
        return new WikiDocumentService(
            mapper.Object,
            wikiDocumentRepository.Object,
            revisionRepository.Object,
            Options.Create(new DocumentOptions()));
    }
}
