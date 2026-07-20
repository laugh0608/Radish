using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class WikiAuthoringServiceTest
{
    [Fact]
    public async Task AuthorCreate_ShouldUseRepositoryGeneratedIdsForDocumentDraftRelation()
    {
        const long documentId = 2079205135170404353;
        const long draftId = 2079205135199764480;
        var fixture = CreateFixture();
        WikiDocument? insertedDocument = null;
        WikiDocumentDraft? insertedDraft = null;
        fixture.Documents.Setup(item => item.QueryCountAsync(
                It.IsAny<Expression<Func<WikiDocument, bool>>?>()))
            .ReturnsAsync(0);
        fixture.Documents.Setup(item => item.QueryExistsAsync(
                It.IsAny<Expression<Func<WikiDocument, bool>>>()))
            .ReturnsAsync(false);
        fixture.Documents.Setup(item => item.AddAsync(It.IsAny<WikiDocument>()))
            .Callback<WikiDocument>(document => insertedDocument = document)
            .ReturnsAsync(documentId);
        fixture.Drafts.Setup(item => item.AddAsync(It.IsAny<WikiDocumentDraft>()))
            .Callback<WikiDocumentDraft>(draft => insertedDraft = draft)
            .ReturnsAsync(draftId);
        fixture.Documents.Setup(item => item.SetActiveDraftAsync(
                documentId, 0, null, draftId, 10001, "Owner", It.IsAny<DateTime>()))
            .ReturnsAsync(1);

        var result = await fixture.Service.AuthorCreateAsync(
            new CreateWikiAuthorDraftDto
            {
                Title = "Draft",
                Slug = "draft",
                MarkdownContent = "body"
            },
            10001,
            "Owner",
            0);

        Assert.NotNull(insertedDocument);
        Assert.NotNull(insertedDraft);
        Assert.Equal(documentId, insertedDocument.Id);
        Assert.Equal(documentId, insertedDraft.DocumentId);
        Assert.Equal(draftId, insertedDraft.Id);
        Assert.Equal(draftId, insertedDocument.ActiveDraftId);
        Assert.Equal(documentId, result.VoDocumentId);
        Assert.Equal(draftId, result.VoDraftId);
    }

    [Fact]
    public async Task AuthorStartDraft_ShouldUseRepositoryGeneratedDraftIdForActiveDraftRelation()
    {
        const long draftId = 2079205135199764480;
        var fixture = CreateFixture(activeDraftId: null);
        fixture.Documents.Setup(item => item.QueryCountAsync(
                It.IsAny<Expression<Func<WikiDocument, bool>>?>()))
            .ReturnsAsync(0);
        fixture.Drafts.Setup(item => item.AddAsync(It.IsAny<WikiDocumentDraft>()))
            .ReturnsAsync(draftId);
        fixture.Documents.Setup(item => item.SetActiveDraftAsync(
                20001, 0, null, draftId, 10001, "Owner", It.IsAny<DateTime>()))
            .ReturnsAsync(1);

        var result = await fixture.Service.AuthorStartDraftAsync(20001, 10001, "Owner", 0);

        Assert.Equal(draftId, result.VoDraftId);
        fixture.Documents.Verify(item => item.SetActiveDraftAsync(
            20001, 0, null, draftId, 10001, "Owner", It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task AuthorGetById_ShouldRejectUserWithoutOwnerOrAcceptedCollaboratorRelation()
    {
        var fixture = CreateFixture();
        fixture.Collaborators.Setup(item => item.QueryFirstAsync(
                It.IsAny<Expression<Func<WikiDocumentCollaborator, bool>>?>()))
            .ReturnsAsync((WikiDocumentCollaborator?)null);

        var result = await fixture.Service.AuthorGetByIdAsync(20001, 10002);

        Assert.Null(result);
    }

    [Fact]
    public async Task AuthorGetById_ShouldAllowAcceptedEditorAndExposeServerCapabilities()
    {
        var fixture = CreateFixture();
        fixture.Collaborators.Setup(item => item.QueryFirstAsync(
                It.IsAny<Expression<Func<WikiDocumentCollaborator, bool>>?>()))
            .ReturnsAsync(new WikiDocumentCollaborator
            {
                DocumentId = 20001,
                UserId = 10002,
                InviteState = (int)WikiDocumentCollaboratorState.Accepted
            });

        var result = await fixture.Service.AuthorGetByIdAsync(20001, 10002);

        Assert.NotNull(result);
        Assert.Equal("Editor", result.VoAuthorRole);
        Assert.True(result.VoCanEdit);
        Assert.False(result.VoCanSubmit);
        Assert.False(result.VoCanManageCollaborators);
    }

    [Fact]
    public async Task AuthorGetById_ShouldAllowPendingInviteeToRespondWithoutEditCapability()
    {
        var fixture = CreateFixture();
        fixture.Collaborators.Setup(item => item.QueryFirstAsync(
                It.IsAny<Expression<Func<WikiDocumentCollaborator, bool>>?>()))
            .ReturnsAsync(new WikiDocumentCollaborator
            {
                DocumentId = 20001,
                UserId = 10002,
                InviteState = (int)WikiDocumentCollaboratorState.Pending
            });

        var result = await fixture.Service.AuthorGetByIdAsync(20001, 10002);

        Assert.NotNull(result);
        Assert.Equal("Invitee", result.VoAuthorRole);
        Assert.False(result.VoCanEdit);
        Assert.False(result.VoCanSubmit);
        Assert.False(result.VoCanManageCollaborators);
    }

    [Fact]
    public async Task AuthorSubmitDraft_ShouldReturnStableConflictWhenCompareAndSetMisses()
    {
        var fixture = CreateFixture(ownerUserId: 10001);
        fixture.Documents.Setup(item => item.TransitionDraftAsync(It.IsAny<WikiDraftTransitionCommand>()))
            .ReturnsAsync(0);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.AuthorSubmitDraftAsync(
            30001,
            new SubmitWikiDraftDto { ExpectedDraftVersion = 1 },
            10001,
            "Owner",
            0));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("Wiki.DraftVersionConflict", exception.ErrorCode);
        fixture.ReviewEvents.Verify(item => item.AddAsync(It.IsAny<WikiDocumentReviewEvent>()), Times.Never);
    }

    [Fact]
    public async Task AuthorSubmitDraft_ShouldTreatRepeatedTargetStateAsIdempotent()
    {
        var fixture = CreateFixture(
            ownerUserId: 10001,
            draftState: WikiDocumentDraftState.Submitted,
            draftVersion: 2);

        var result = await fixture.Service.AuthorSubmitDraftAsync(
            30001,
            new SubmitWikiDraftDto { ExpectedDraftVersion = 1 },
            10001,
            "Owner",
            0);

        Assert.Equal((int)WikiDocumentDraftState.Submitted, result.VoReviewState);
        fixture.Documents.Verify(
            item => item.TransitionDraftAsync(It.IsAny<WikiDraftTransitionCommand>()),
            Times.Never);
    }

    [Fact]
    public async Task AdminReviewDraft_ShouldTreatRepeatedApplyAsIdempotent()
    {
        var fixture = CreateFixture(
            draftState: WikiDocumentDraftState.Applied,
            draftVersion: 2,
            documentVersion: 2);

        var result = await fixture.Service.AdminReviewDraftAsync(
            30001,
            new ReviewWikiDraftDto
            {
                Action = WikiDocumentReviewActions.Apply,
                ExpectedDraftVersion = 1,
                ExpectedDocumentVersion = 1
            },
            90001,
            "Reviewer",
            0);

        Assert.Equal((int)WikiDocumentDraftState.Applied, result.VoReviewState);
        fixture.Documents.Verify(
            item => item.ApplyDraftToDocumentAsync(It.IsAny<WikiDraftApplyCommand>()),
            Times.Never);
    }

    private static Fixture CreateFixture(
        long ownerUserId = 10001,
        WikiDocumentDraftState draftState = WikiDocumentDraftState.Editing,
        int draftVersion = 1,
        int documentVersion = 1,
        long? activeDraftId = 30001)
    {
        var mapper = Mock.Of<IMapper>();
        var documents = new Mock<IWikiDocumentRepository>(MockBehavior.Strict);
        var revisions = new Mock<IBaseRepository<WikiDocumentRevision>>(MockBehavior.Strict);
        var consoleAuthorization = new Mock<IConsoleAuthorizationService>(MockBehavior.Strict);
        var drafts = new Mock<IBaseRepository<WikiDocumentDraft>>(MockBehavior.Strict);
        var collaborators = new Mock<IBaseRepository<WikiDocumentCollaborator>>(MockBehavior.Strict);
        var reviewEvents = new Mock<IBaseRepository<WikiDocumentReviewEvent>>(MockBehavior.Strict);
        var users = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var document = new WikiDocument
        {
            Id = 20001,
            TenantId = 0,
            OwnerUserId = ownerUserId,
            ActiveDraftId = activeDraftId,
            Title = "Document",
            Slug = "document",
            MarkdownContent = "formal",
            Version = documentVersion,
            SourceType = "Custom"
        };
        var draft = new WikiDocumentDraft
        {
            Id = 30001,
            TenantId = 0,
            DocumentId = document.Id,
            BaseDocumentVersion = 1,
            DraftVersion = draftVersion,
            Title = "Draft",
            Slug = "draft",
            MarkdownContent = "body",
            ReviewState = (int)draftState
        };
        documents.Setup(item => item.QueryByIdAsync(document.Id)).ReturnsAsync(document);
        drafts.Setup(item => item.QueryByIdAsync(draft.Id)).ReturnsAsync(draft);
        users.Setup(item => item.QueryByIdAsync(ownerUserId)).ReturnsAsync(new User
        {
            Id = ownerUserId,
            PublicId = "usr_owner",
            UserName = "Owner"
        });
        collaborators.Setup(item => item.QueryAsync(
                It.IsAny<Expression<Func<WikiDocumentCollaborator, bool>>?>()))
            .ReturnsAsync([]);
        reviewEvents.Setup(item => item.QueryAsync(
                It.IsAny<Expression<Func<WikiDocumentReviewEvent, bool>>?>()))
            .ReturnsAsync([]);
        var service = new WikiDocumentService(
            mapper,
            documents.Object,
            revisions.Object,
            consoleAuthorization.Object,
            Options.Create(new DocumentOptions()),
            drafts.Object,
            collaborators.Object,
            reviewEvents.Object,
            users.Object);
        return new Fixture(service, documents, drafts, collaborators, reviewEvents);
    }

    private sealed record Fixture(
        WikiDocumentService Service,
        Mock<IWikiDocumentRepository> Documents,
        Mock<IBaseRepository<WikiDocumentDraft>> Drafts,
        Mock<IBaseRepository<WikiDocumentCollaborator>> Collaborators,
        Mock<IBaseRepository<WikiDocumentReviewEvent>> ReviewEvents);
}
