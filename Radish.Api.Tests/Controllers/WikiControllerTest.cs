#nullable enable

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
    public async Task GetById_Should_Return_Failed_When_Document_NotFound()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetDetailAsync(1001, false))
            .ReturnsAsync((WikiDocumentDetailVo?)null);

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetById(1001);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("文档不存在或无权访问", result.MessageInfo);
    }

    [Fact]
    public async Task Create_Should_Return_Id_When_Request_Valid()
    {
        var request = new CreateWikiDocumentDto
        {
            Title = "Wiki 入门",
            MarkdownContent = "# Hello Wiki",
            Summary = "测试摘要",
            Sort = 10,
        };

        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.CreateDocumentAsync(request, 10001, "Tester", 0))
            .ReturnsAsync(9527);

        var controller = CreateController(serviceMock.Object, isAdmin: true);
        var result = await controller.Create(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(9527, result.ResponseData);
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
        Assert.Equal(400, result.StatusCode);
        Assert.False(result.ResponseData);
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

    private static WikiController CreateController(IWikiDocumentService wikiDocumentService, bool isAdmin = false)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(x => x.Current).Returns(new CurrentUser
        {
            IsAuthenticated = true,
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0,
            Roles = isAdmin ? [UserRoles.Admin] : []
        });

        return new WikiController(wikiDocumentService, currentUserAccessorMock.Object);
    }

    private static Mock<IWikiDocumentService> CreateServiceMock()
    {
        return new Mock<IWikiDocumentService>(MockBehavior.Strict);
    }
}
