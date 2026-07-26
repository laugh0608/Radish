using System.Threading.Tasks;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public sealed class WikiAuthoringControllerTest
{
    [Fact]
    public async Task AuthorCreate_ShouldAllowOrdinaryAuthenticatedUser()
    {
        var service = new Mock<IWikiDocumentService>(MockBehavior.Strict);
        service.Setup(item => item.AuthorCreateAsync(
                It.IsAny<CreateWikiAuthorDraftDto>(), 10001, "Author", 7))
            .ReturnsAsync(new WikiAuthorDraftDetailVo { VoDocumentId = 20001, VoDraftId = 30001 });
        var controller = CreateController(service.Object, 10001, "Author", 7);

        var result = await controller.AuthorCreate(new CreateWikiAuthorDraftDto
        {
            Title = "Contribution",
            MarkdownContent = "body"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ResponseData);
        Assert.Equal(20001, result.ResponseData!.VoDocumentId);
    }

    [Fact]
    public async Task AuthorGetById_ShouldNotLeakInaccessibleDraft()
    {
        var service = new Mock<IWikiDocumentService>(MockBehavior.Strict);
        service.Setup(item => item.AuthorGetByIdAsync(20001, 10001, false))
            .ReturnsAsync((WikiAuthorDraftDetailVo?)null);
        var controller = CreateController(service.Object, 10001, "Author", 7);

        var result = await controller.AuthorGetById(20001);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Wiki.DraftNotFound", result.Code);
    }

    [Fact]
    public async Task AdminReviewDraft_ShouldPreserveStableConflictContract()
    {
        var service = new Mock<IWikiDocumentService>(MockBehavior.Strict);
        service.Setup(item => item.AdminReviewDraftAsync(
                30001, It.IsAny<ReviewWikiDraftDto>(), 90001, "Reviewer", 7))
            .ThrowsAsync(new BusinessException(
                "正式文档版本已变化", 409,
                "Wiki.DocumentVersionConflict", "error.wiki.document_version_conflict"));
        var controller = CreateController(service.Object, 90001, "Reviewer", 7);

        var result = await controller.AdminReviewDraft(30001, new ReviewWikiDraftDto
        {
            Action = "Apply",
            ExpectedDraftVersion = 2,
            ExpectedDocumentVersion = 1
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Wiki.DocumentVersionConflict", result.Code);
    }

    private static WikiController CreateController(
        IWikiDocumentService service,
        long userId,
        string userName,
        long tenantId)
    {
        var current = new Mock<ICurrentUserAccessor>();
        current.SetupGet(item => item.Current).Returns(new CurrentUser
        {
            IsAuthenticated = true,
            UserId = userId,
            UserName = userName,
            TenantId = tenantId,
            Roles = []
        });
        return new WikiController(service, Mock.Of<IUserBrowseHistoryService>(), current.Object);
    }
}
