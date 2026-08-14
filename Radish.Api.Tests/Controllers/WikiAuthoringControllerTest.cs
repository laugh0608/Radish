using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Filters;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public sealed class WikiAuthoringControllerTest
{
    [Theory]
    [InlineData(nameof(WikiController.AuthorGetRevisionHistory))]
    [InlineData(nameof(WikiController.AuthorGetRevisionDetail))]
    public void AuthorRevisionReads_ShouldRequireClientWithoutConsolePermission(string methodName)
    {
        var method = typeof(WikiController).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        var authorize = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(AuthorizationPolicies.Client, authorize.Policy);
        Assert.Empty(method.GetCustomAttributes<RequireConsolePermissionAttribute>());
    }

    [Theory]
    [InlineData(nameof(WikiController.GetRevisionList))]
    [InlineData(nameof(WikiController.GetRevisionDetail))]
    public void ConsoleRevisionReads_ShouldKeepDocsViewPermission(string methodName)
    {
        var method = typeof(WikiController).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        var authorize = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(AuthorizationPolicies.Client, authorize.Policy);
        var permission = Assert.Single(
            method.GetCustomAttributes<RequireConsolePermissionAttribute>());
        var permissionField = typeof(RequireConsolePermissionAttribute).GetField(
            "_permissions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(permissionField);
        Assert.Equal(
            [ConsolePermissions.DocsView],
            Assert.IsType<string[]>(permissionField.GetValue(permission)));
    }

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
    public async Task AuthorGetList_ShouldParseStableQueryTokens()
    {
        var service = new Mock<IWikiDocumentService>(MockBehavior.Strict);
        service.Setup(item => item.AuthorGetListAsync(
                10001,
                WikiAuthorDocumentScope.Owned,
                WikiAuthorDraftStage.Terminal,
                2,
                15))
            .ReturnsAsync(new PageModel<WikiAuthorDocumentVo>
            {
                Page = 2,
                PageSize = 15
            });
        var controller = CreateController(service.Object, 10001, "Author", 7);

        var result = await controller.AuthorGetList("owned", "terminal", 2, 15);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ResponseData!.Page);
        Assert.Equal(15, result.ResponseData.PageSize);
    }

    [Theory]
    [InlineData("owner", "all")]
    [InlineData("all", "finished")]
    public async Task AuthorGetList_ShouldRejectUnknownQueryTokens(string scope, string draftStage)
    {
        var service = new Mock<IWikiDocumentService>(MockBehavior.Strict);
        var controller = CreateController(service.Object, 10001, "Author", 7);

        var result = await controller.AuthorGetList(scope, draftStage);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Wiki.AuthorListQueryInvalid", result.Code);
        Assert.Equal("error.wiki.author_list_query_invalid", result.MessageKey);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorGetRevisionHistory_ShouldUseRelationAuthorizedAuthorContract()
    {
        var service = new Mock<IWikiDocumentService>(MockBehavior.Strict);
        service.Setup(item => item.AuthorGetRevisionHistoryAsync(20001, 10001, false))
            .ReturnsAsync(new WikiAuthorRevisionHistoryVo
            {
                VoDocumentId = 20001,
                VoAuthorRole = "Owner",
                VoCanStartDraft = true
            });
        var controller = CreateController(service.Object, 10001, "Author", 7);

        var result = await controller.AuthorGetRevisionHistory(20001);

        Assert.True(result.IsSuccess);
        Assert.Equal("Owner", result.ResponseData!.VoAuthorRole);
        Assert.True(result.ResponseData.VoCanStartDraft);
    }

    [Fact]
    public async Task AuthorGetRevisionDetail_ShouldNotLeakInaccessibleRevision()
    {
        var service = new Mock<IWikiDocumentService>(MockBehavior.Strict);
        service.Setup(item => item.AuthorGetRevisionDetailAsync(50001, 10001, false))
            .ReturnsAsync((WikiAuthorRevisionDetailVo?)null);
        var controller = CreateController(service.Object, 10001, "Author", 7);

        var result = await controller.AuthorGetRevisionDetail(50001);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Wiki.RevisionNotFound", result.Code);
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
