#nullable enable

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
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

    [Fact(DisplayName = "更新访问策略只应修改可见性与允许列表")]
    public async Task UpdateAccessPolicyAsync_ShouldOnlyUpdateAccessPolicy()
    {
        var document = new WikiDocument
        {
            Id = 12,
            Title = "治理文档",
            Slug = "governance-doc",
            MarkdownContent = "# old",
            SourceType = "Custom",
            Status = (int)WikiDocumentStatusEnum.Published,
            Visibility = (int)WikiDocumentVisibilityEnum.Authenticated,
            Version = 5,
            IsDeleted = false
        };

        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(r => r.QueryByIdAsync(12)).ReturnsAsync(document);
        repository.Setup(r => r.UpdateAsync(It.IsAny<WikiDocument>())).ReturnsAsync(true);

        var service = CreateService(repository);
        var dto = new UpdateWikiDocumentAccessPolicyDto
        {
            Visibility = (int)WikiDocumentVisibilityEnum.Restricted,
            AllowedRoles = ["Admin", "editor"],
            AllowedPermissions = ["console.docs.view"]
        };

        var result = await service.UpdateAccessPolicyAsync(12, dto, 9, "Governance");

        result.ShouldBeTrue();
        document.Title.ShouldBe("治理文档");
        document.MarkdownContent.ShouldBe("# old");
        document.Version.ShouldBe(5);
        document.Visibility.ShouldBe((int)WikiDocumentVisibilityEnum.Restricted);
        document.AllowedRoles.ShouldBe("|admin|editor|");
        document.AllowedPermissions.ShouldBe("|console.docs.view|");
        document.ModifyId.ShouldBe(9);
        document.ModifyBy.ShouldBe("Governance");
    }

    [Fact(DisplayName = "更新固定文档访问策略应被拒绝")]
    public async Task UpdateAccessPolicyAsync_ShouldThrow_WhenDocumentIsBuiltIn()
    {
        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(r => r.QueryByIdAsync(13)).ReturnsAsync(new WikiDocument
        {
            Id = 13,
            Title = "固定文档",
            Slug = "built-in-doc",
            SourceType = "BuiltIn",
            Status = (int)WikiDocumentStatusEnum.Published,
            Visibility = (int)WikiDocumentVisibilityEnum.Public,
            IsDeleted = false
        });

        var service = CreateService(repository);
        var dto = new UpdateWikiDocumentAccessPolicyDto
        {
            Visibility = (int)WikiDocumentVisibilityEnum.Authenticated
        };

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => service.UpdateAccessPolicyAsync(13, dto, 1, "Tester"));
        exception.Message.ShouldBe("固定文档为只读内容，请修改 Docs 目录中的源文件");
    }

    private static WikiDocumentService CreateService(Mock<IWikiDocumentRepository> wikiDocumentRepository)
    {
        var mapper = new Mock<IMapper>();
        var revisionRepository = new Mock<IBaseRepository<WikiDocumentRevision>>();
        var consoleAuthorizationService = new Mock<IConsoleAuthorizationService>();
        consoleAuthorizationService
            .Setup(service => service.GetPermissionKeysByRolesAsync(It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync([]);
        return new WikiDocumentService(
            mapper.Object,
            wikiDocumentRepository.Object,
            revisionRepository.Object,
            consoleAuthorizationService.Object,
            Options.Create(new DocumentOptions()));
    }
}
