#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(WikiController))]
public class WikiControllerTest
{
    [Fact]
    public async Task PublicGetList_Should_Use_Dedicated_Public_Service_Contract()
    {
        var page = new PageModel<WikiDocumentVo>
        {
            Page = 2,
            PageSize = 10,
            DataCount = 1,
            PageCount = 1,
            Data = []
        };
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(service => service.GetPublicListAsync(2, 10, "guide", 42))
            .ReturnsAsync(page);

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var result = await controller.PublicGetList(2, 10, "guide", 42);

        Assert.True(result.IsSuccess);
        Assert.Same(page, result.ResponseData);
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task PublicGetTree_Should_Use_Dedicated_Public_Service_Contract()
    {
        List<WikiDocumentTreeNodeVo> tree =
        [
            new() { VoId = 1, VoTitle = "公开文档", VoSlug = "public-doc", VoChildren = [] }
        ];
        var serviceMock = CreateServiceMock();
        serviceMock.Setup(service => service.GetPublicTreeAsync()).ReturnsAsync(tree);

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var result = await controller.PublicGetTree();

        Assert.True(result.IsSuccess);
        Assert.Same(tree, result.ResponseData);
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task PublicGetBySlug_Should_Return_Failed_When_Public_Document_NotFound()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(service => service.GetPublicBySlugAsync("restricted-doc"))
            .ReturnsAsync((WikiDocumentDetailVo?)null);

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var result = await controller.PublicGetBySlug("restricted-doc");

        Assert.False(result.IsSuccess);
        Assert.Equal("公开文档不存在", result.MessageInfo);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Wiki.DocumentNotFound", result.Code);
        Assert.Equal("error.wiki.document_not_found", result.MessageKey);
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task GetById_Should_Return_Failed_When_Document_NotFound()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetDetailAsync(1001, false, false, true, It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync((WikiDocumentDetailVo?)null);

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetById(1001);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("文档不存在或无权访问", result.MessageInfo);
        Assert.Equal("Wiki.DocumentNotAccessible", result.Code);
        Assert.Equal("error.wiki.document_not_accessible", result.MessageKey);
    }

    [Fact]
    public async Task Delete_Should_Return_Success_When_Service_Returns_True()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.DeleteDocumentAsync(42, 10001, "Tester"))
            .ReturnsAsync(true);

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var result = await controller.Delete(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.True(result.ResponseData);
    }

    [Fact]
    public async Task Restore_Should_Return_Success_When_Service_Returns_True()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.RestoreDocumentAsync(42, 10001, "Tester"))
            .ReturnsAsync(true);

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var result = await controller.Restore(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.True(result.ResponseData);
    }

    [Fact]
    public async Task Publish_Should_Return_Failed_When_Document_Missing()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.PublishAsync(404, 10001, "Tester"))
            .ReturnsAsync(false);

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var result = await controller.Publish(404);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.False(result.ResponseData);
        Assert.Equal("Wiki.DocumentNotFound", result.Code);
        Assert.Equal("error.wiki.document_not_found", result.MessageKey);
    }

    [Fact]
    public async Task ImportMarkdown_Should_Return_Id_When_File_Valid()
    {
        var fileBytes = Encoding.UTF8.GetBytes("# 导入测试\n\nhello wiki");
        await using var stream = new MemoryStream(fileBytes);
        IFormFile formFile = new FormFile(stream, 0, fileBytes.Length, "file", "guide.md")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/markdown",
        };

        var request = new WikiMarkdownImportDto
        {
            File = formFile,
            Summary = "导入摘要",
            PublishAfterImport = false,
        };

        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.ImportMarkdownAsync(
                It.Is<WikiMarkdownImportDto>(dto => dto.File.FileName == "guide.md" && dto.Summary == "导入摘要"),
                10001,
                "Tester",
                0))
            .ReturnsAsync(2048);

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var result = await controller.ImportMarkdown(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(2048, result.ResponseData);
    }

    [Fact]
    public async Task ExportMarkdown_Should_Return_File_When_Document_Exists()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.ExportMarkdownAsync(42, true))
            .ReturnsAsync(("wiki-guide.md", "# 标题\n\n内容"));

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var actionResult = await controller.ExportMarkdown(42);

        var fileResult = Assert.IsType<FileContentResult>(actionResult);
        Assert.Equal("text/markdown; charset=utf-8", fileResult.ContentType);
        Assert.Equal("wiki-guide.md", fileResult.FileDownloadName);
        Assert.Equal("# 标题\n\n内容", Encoding.UTF8.GetString(fileResult.FileContents));
    }

    [Fact]
    public async Task GetRevisionList_Should_Return_Data_When_Found()
    {
        var revisionList = new List<WikiDocumentRevisionItemVo>
        {
            new()
            {
                VoId = 11,
                VoDocumentId = 42,
                VoVersion = 3,
                VoTitle = "Wiki 文档",
                VoCreateBy = "Tester",
                VoSourceType = "Manual",
                VoIsCurrent = true,
            }
        };

        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetRevisionListAsync(42))
            .ReturnsAsync(revisionList);

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var result = await controller.GetRevisionList(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.ResponseData);
        Assert.Single(result.ResponseData);
        Assert.Equal(3, result.ResponseData[0].VoVersion);
    }

    [Fact]
    public async Task GetRevisionDetail_Should_Return_Failed_When_Revision_NotFound()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetRevisionDetailAsync(404))
            .ReturnsAsync((WikiDocumentRevisionDetailVo?)null);

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var result = await controller.GetRevisionDetail(404);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("版本不存在", result.MessageInfo);
        Assert.Equal("Wiki.RevisionNotFound", result.Code);
        Assert.Equal("error.wiki.revision_not_found", result.MessageKey);
    }

    [Fact]
    public async Task Rollback_Should_Return_Success_When_Service_Returns_True()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.RollbackAsync(88, 10001, "Tester"))
            .ReturnsAsync(true);

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var result = await controller.Rollback(88);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.True(result.ResponseData);
    }

    private static WikiController CreateController(IWikiDocumentService wikiDocumentService, bool isAdmin = false, bool isAuthenticated = true)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(x => x.Current).Returns(new CurrentUser
        {
            IsAuthenticated = isAuthenticated,
            UserId = isAuthenticated ? 10001 : 0,
            UserName = isAuthenticated ? "Tester" : string.Empty,
            TenantId = 0,
            Roles = isAdmin ? [UserRoles.Admin] : []
        });
        var browseHistoryServiceMock = new Mock<IUserBrowseHistoryService>();
        browseHistoryServiceMock
            .Setup(service => service.RecordAsync(It.IsAny<RecordBrowseHistoryDto>()))
            .Returns(Task.CompletedTask);

        return new WikiController(wikiDocumentService, browseHistoryServiceMock.Object, currentUserAccessorMock.Object);
    }

    private static Mock<IWikiDocumentService> CreateServiceMock()
    {
        return new Mock<IWikiDocumentService>(MockBehavior.Strict);
    }
}
