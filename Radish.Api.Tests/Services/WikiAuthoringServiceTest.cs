using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class WikiAuthoringServiceTest
{
    [Fact]
    public void AdminReviewDraft_ShouldKeepApplyPipelineInsideTransactionBoundary()
    {
        var method = typeof(WikiDocumentService).GetMethod(
            nameof(WikiDocumentService.AdminReviewDraftAsync),
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<UseTranAttribute>());
    }

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
        Assert.Equal("usr_owner", result.VoOwnerUserPublicId);
        Assert.Empty(result.VoCollaborators);
        Assert.Empty(result.VoReviewEvents);
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
        Assert.Equal("usr_owner", result.VoOwnerUserPublicId);
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

    [Theory]
    [InlineData(20002, 0)]
    [InlineData(20001, 9)]
    public async Task AuthorGetById_ShouldFailClosedForMismatchedActiveDraftBinding(
        long draftDocumentId,
        long draftTenantId)
    {
        var fixture = CreateFixture();
        fixture.Draft.DocumentId = draftDocumentId;
        fixture.Draft.TenantId = draftTenantId;

        var result = await fixture.Service.AuthorGetByIdAsync(
            fixture.Document.Id,
            10001);

        Assert.Null(result);
        fixture.Documents.Verify(
            item => item.QueryLatestTerminalDraftAsync(It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthorGetById_ShouldReturnLatestTerminalEvidenceAndPayloadPurgeState()
    {
        var fixture = CreateFixture(
            draftState: WikiDocumentDraftState.Withdrawn,
            draftVersion: 4,
            activeDraftId: null);
        var purgedAt = DateTime.UtcNow;
        fixture.Draft.PayloadPurgedAt = purgedAt;
        fixture.Draft.ModifyTime = purgedAt;
        fixture.Documents.Setup(item => item.QueryLatestTerminalDraftAsync(fixture.Document.Id))
            .ReturnsAsync(fixture.Draft);

        var result = await fixture.Service.AuthorGetByIdAsync(fixture.Document.Id, 10001);

        Assert.NotNull(result);
        Assert.Equal(fixture.Draft.Id, result.VoDraftId);
        Assert.False(result.VoIsActiveDraft);
        Assert.True(result.VoCanStartDraft);
        Assert.False(result.VoHasDraftPayload);
        Assert.Equal(purgedAt, result.VoPayloadPurgedAt);
    }

    [Fact]
    public async Task AuthorGetList_ShouldExposeLatestTerminalDraftAndExplicitStartCapability()
    {
        var fixture = CreateFixture(
            draftState: WikiDocumentDraftState.Applied,
            draftVersion: 2,
            activeDraftId: null);
        fixture.Draft.ModifyTime = DateTime.UtcNow;
        fixture.Documents.Setup(item => item.QueryPageAsync(
                It.IsAny<Expression<Func<WikiDocument, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<WikiDocument, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync(([fixture.Document], 1));
        fixture.Documents.Setup(item => item.QueryLatestTerminalDraftEvidenceAsync(
                It.Is<IReadOnlyCollection<long>>(documentIds =>
                    documentIds.SequenceEqual(new long[] { fixture.Document.Id }))))
            .ReturnsAsync([
                new WikiTerminalDraftEvidence
                {
                    DraftId = fixture.Draft.Id,
                    DocumentId = fixture.Draft.DocumentId,
                    Title = fixture.Draft.Title,
                    Slug = fixture.Draft.Slug,
                    Summary = fixture.Draft.Summary,
                    DraftVersion = fixture.Draft.DraftVersion,
                    ReviewState = fixture.Draft.ReviewState,
                    PayloadPurgedAt = fixture.Draft.PayloadPurgedAt,
                    ModifyTime = fixture.Draft.ModifyTime
                }
            ]);

        var result = await fixture.Service.AuthorGetListAsync(10001, 1, 20);

        var item = Assert.Single(result.Data);
        Assert.Equal(fixture.Draft.Id, item.VoDraftId);
        Assert.Null(item.VoActiveDraftId);
        Assert.Equal(fixture.Draft.Id, item.VoLatestDraftId);
        Assert.Equal(fixture.Document.Slug, item.VoDocumentSlug);
        Assert.NotEqual(fixture.Draft.Slug, item.VoDocumentSlug);
        Assert.False(item.VoIsActiveDraft);
        Assert.True(item.VoCanStartDraft);
    }

    [Fact]
    public async Task AuthorGetList_ShouldFailClosedForMismatchedActiveDraftBinding()
    {
        var fixture = CreateFixture();
        fixture.Draft.DocumentId = 20002;
        fixture.Documents.Setup(item => item.QueryPageAsync(
                It.IsAny<Expression<Func<WikiDocument, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<WikiDocument, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync(([fixture.Document], 1));
        fixture.Drafts.Setup(item => item.QueryAsync(
                It.IsAny<Expression<Func<WikiDocumentDraft, bool>>?>()))
            .ReturnsAsync([fixture.Draft]);

        var result = await fixture.Service.AuthorGetListAsync(10001, 1, 20);

        var item = Assert.Single(result.Data);
        Assert.Null(item.VoDraftId);
        Assert.Equal(fixture.Document.ActiveDraftId, item.VoActiveDraftId);
        Assert.Equal(fixture.Document.Title, item.VoTitle);
        Assert.False(item.VoIsActiveDraft);
        Assert.False(item.VoCanEdit);
        Assert.False(item.VoCanSubmit);
        Assert.False(item.VoCanStartDraft);
        fixture.Documents.Verify(
            item => item.QueryLatestTerminalDraftEvidenceAsync(
                It.IsAny<IReadOnlyCollection<long>>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthorRevisionHistory_ShouldAllowOwnerAndExposeStartCapability()
    {
        var fixture = CreateFixture(activeDraftId: null);
        fixture.Revisions.Setup(item => item.QueryWithOrderAsync(
                It.IsAny<Expression<Func<WikiDocumentRevision, bool>>?>(),
                It.IsAny<Expression<Func<WikiDocumentRevision, object>>>(),
                OrderByType.Desc,
                0))
            .ReturnsAsync([
                new WikiDocumentRevision
                {
                    Id = 50001,
                    DocumentId = fixture.Document.Id,
                    Version = fixture.Document.Version,
                    Title = fixture.Document.Title
                }
            ]);

        var result = await fixture.Service.AuthorGetRevisionHistoryAsync(
            fixture.Document.Id,
            10001);

        Assert.NotNull(result);
        Assert.Equal("Owner", result.VoAuthorRole);
        Assert.True(result.VoCanStartDraft);
        Assert.Single(result.VoRevisions);
    }

    [Theory]
    [InlineData(WikiDocumentCollaboratorState.Pending, "Invitee")]
    [InlineData(WikiDocumentCollaboratorState.Accepted, "Editor")]
    public async Task AuthorRevisionHistory_ShouldAllowCurrentCollaboratorRelations(
        WikiDocumentCollaboratorState inviteState,
        string expectedRole)
    {
        var fixture = CreateFixture(ownerUserId: 10001);
        fixture.Collaborators.Setup(item => item.QueryFirstAsync(
                It.IsAny<Expression<Func<WikiDocumentCollaborator, bool>>?>()))
            .ReturnsAsync(new WikiDocumentCollaborator
            {
                DocumentId = fixture.Document.Id,
                UserId = 10002,
                InviteState = (int)inviteState
            });
        fixture.Revisions.Setup(item => item.QueryWithOrderAsync(
                It.IsAny<Expression<Func<WikiDocumentRevision, bool>>?>(),
                It.IsAny<Expression<Func<WikiDocumentRevision, object>>>(),
                OrderByType.Desc,
                0))
            .ReturnsAsync([]);

        var result = await fixture.Service.AuthorGetRevisionHistoryAsync(
            fixture.Document.Id,
            10002);

        Assert.NotNull(result);
        Assert.Equal(expectedRole, result.VoAuthorRole);
        Assert.False(result.VoCanStartDraft);
    }

    [Theory]
    [InlineData(WikiDocumentCollaboratorState.Declined)]
    [InlineData(WikiDocumentCollaboratorState.Revoked)]
    public async Task AuthorRevisionHistory_ShouldRejectInactiveCollaboratorRelations(
        WikiDocumentCollaboratorState inviteState)
    {
        var fixture = CreateFixture(ownerUserId: 10001);
        fixture.Collaborators.Setup(item => item.QueryFirstAsync(
                It.IsAny<Expression<Func<WikiDocumentCollaborator, bool>>?>()))
            .ReturnsAsync(new WikiDocumentCollaborator
            {
                DocumentId = fixture.Document.Id,
                UserId = 10002,
                InviteState = (int)inviteState
            });

        var result = await fixture.Service.AuthorGetRevisionHistoryAsync(
            fixture.Document.Id,
            10002);

        Assert.Null(result);
        fixture.Revisions.Verify(item => item.QueryWithOrderAsync(
            It.IsAny<Expression<Func<WikiDocumentRevision, bool>>?>(),
            It.IsAny<Expression<Func<WikiDocumentRevision, object>>>(),
            It.IsAny<OrderByType>(),
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AuthorRevisionHistory_ShouldAllowSystemOrAdminWithoutAuthorRelation()
    {
        var fixture = CreateFixture(ownerUserId: 10001);
        fixture.Revisions.Setup(item => item.QueryWithOrderAsync(
                It.IsAny<Expression<Func<WikiDocumentRevision, bool>>?>(),
                It.IsAny<Expression<Func<WikiDocumentRevision, object>>>(),
                OrderByType.Desc,
                0))
            .ReturnsAsync([]);

        var result = await fixture.Service.AuthorGetRevisionHistoryAsync(
            fixture.Document.Id,
            90001,
            isSystemOrAdmin: true);

        Assert.NotNull(result);
        Assert.Equal("Administrator", result.VoAuthorRole);
    }

    [Fact]
    public async Task AuthorRevisionHistory_ShouldRejectDeletedDocument()
    {
        var fixture = CreateFixture();
        fixture.Document.IsDeleted = true;

        var result = await fixture.Service.AuthorGetRevisionHistoryAsync(
            fixture.Document.Id,
            10001);

        Assert.Null(result);
        fixture.Revisions.Verify(item => item.QueryWithOrderAsync(
            It.IsAny<Expression<Func<WikiDocumentRevision, bool>>?>(),
            It.IsAny<Expression<Func<WikiDocumentRevision, object>>>(),
            It.IsAny<OrderByType>(),
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AuthorRevisionDetail_ShouldAllowPendingInviteeButRejectUnrelatedUser()
    {
        var fixture = CreateFixture(ownerUserId: 10001);
        var revision = new WikiDocumentRevision
        {
            Id = 50001,
            DocumentId = fixture.Document.Id,
            Version = 1,
            Title = "Revision",
            MarkdownContent = "body"
        };
        fixture.Revisions.Setup(item => item.QueryByIdAsync(revision.Id)).ReturnsAsync(revision);
        fixture.Collaborators.SetupSequence(item => item.QueryFirstAsync(
                It.IsAny<Expression<Func<WikiDocumentCollaborator, bool>>?>()))
            .ReturnsAsync(new WikiDocumentCollaborator
            {
                DocumentId = fixture.Document.Id,
                UserId = 10002,
                InviteState = (int)WikiDocumentCollaboratorState.Pending
            })
            .ReturnsAsync((WikiDocumentCollaborator?)null);

        var inviteeResult = await fixture.Service.AuthorGetRevisionDetailAsync(
            revision.Id,
            10002);
        var unrelatedResult = await fixture.Service.AuthorGetRevisionDetailAsync(
            revision.Id,
            10003);

        Assert.NotNull(inviteeResult);
        Assert.Null(unrelatedResult);
        Assert.Null(typeof(WikiAuthorRevisionDetailVo).GetProperty("VoCreateId"));
    }

    [Fact]
    public async Task AuthorRevisionDetail_ShouldAuthorizeAgainstRevisionDocumentBinding()
    {
        var fixture = CreateFixture(ownerUserId: 10001);
        var otherDocument = new WikiDocument
        {
            Id = 20002,
            TenantId = 0,
            OwnerUserId = 10002,
            Title = "Other",
            Slug = "other",
            SourceType = "Custom"
        };
        var revision = new WikiDocumentRevision
        {
            Id = 50002,
            DocumentId = otherDocument.Id,
            Version = 1,
            Title = "Other revision",
            MarkdownContent = "body"
        };
        fixture.Revisions.Setup(item => item.QueryByIdAsync(revision.Id)).ReturnsAsync(revision);
        fixture.Documents.Setup(item => item.QueryByIdAsync(otherDocument.Id)).ReturnsAsync(otherDocument);
        fixture.Collaborators.Setup(item => item.QueryFirstAsync(
                It.IsAny<Expression<Func<WikiDocumentCollaborator, bool>>?>()))
            .ReturnsAsync((WikiDocumentCollaborator?)null);

        var result = await fixture.Service.AuthorGetRevisionDetailAsync(
            revision.Id,
            10001);

        Assert.Null(result);
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
        Assert.Equal("usr_owner", result.VoOwnerUserPublicId);
        fixture.Documents.Verify(
            item => item.TransitionDraftAsync(It.IsAny<WikiDraftTransitionCommand>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthorWithdrawDraft_ShouldReturnTerminalEvidenceOnRepeatedRequest()
    {
        var fixture = CreateFixture(
            draftState: WikiDocumentDraftState.Withdrawn,
            draftVersion: 2,
            activeDraftId: null);

        var result = await fixture.Service.AuthorWithdrawDraftAsync(
            fixture.Draft.Id,
            1,
            10001,
            "Owner",
            0);

        Assert.Equal((int)WikiDocumentDraftState.Withdrawn, result.VoReviewState);
        Assert.Equal(fixture.Document.Slug, result.VoDocumentSlug);
        Assert.NotEqual(fixture.Draft.Slug, result.VoDocumentSlug);
        Assert.Equal("usr_owner", result.VoOwnerUserPublicId);
        Assert.True(result.VoCanStartDraft);
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

    [Fact]
    public async Task AdminReviewDraft_ShouldRejectClientVersionThatDiffersFromDraftBaseVersion()
    {
        var fixture = CreateFixture(
            draftState: WikiDocumentDraftState.Submitted,
            documentVersion: 2);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.AdminReviewDraftAsync(
            fixture.Draft.Id,
            new ReviewWikiDraftDto
            {
                Action = WikiDocumentReviewActions.Apply,
                ExpectedDraftVersion = 1,
                ExpectedDocumentVersion = 2
            },
            90001,
            "Reviewer",
            0));

        Assert.Equal("Wiki.DocumentVersionConflict", exception.ErrorCode);
        fixture.Documents.Verify(
            item => item.TransitionDraftAsync(It.IsAny<WikiDraftTransitionCommand>()),
            Times.Never);
        fixture.Documents.Verify(
            item => item.ApplyDraftToDocumentAsync(It.IsAny<WikiDraftApplyCommand>()),
            Times.Never);
    }

    [Fact]
    public async Task AdminReviewDraft_ShouldApplyUsingServerDraftBaseVersion()
    {
        var fixture = CreateFixture(draftState: WikiDocumentDraftState.Submitted);
        fixture.Documents.Setup(item => item.QueryExistsAsync(
                It.IsAny<Expression<Func<WikiDocument, bool>>>()))
            .ReturnsAsync(false);
        fixture.Documents.Setup(item => item.TransitionDraftAsync(
                It.IsAny<WikiDraftTransitionCommand>()))
            .Callback<WikiDraftTransitionCommand>(command =>
            {
                fixture.Draft.ReviewState = command.TargetState;
                fixture.Draft.DraftVersion = command.ExpectedDraftVersion + 1;
            })
            .ReturnsAsync(1);
        fixture.Documents.Setup(item => item.ApplyDraftToDocumentAsync(
                It.IsAny<WikiDraftApplyCommand>()))
            .ReturnsAsync(1);
        fixture.Documents.Setup(item => item.SetActiveDraftAsync(
                fixture.Document.Id,
                0,
                fixture.Draft.Id,
                null,
                90001,
                "Reviewer",
                It.IsAny<DateTime>()))
            .ReturnsAsync(1);
        fixture.Revisions.Setup(item => item.AddAsync(It.IsAny<WikiDocumentRevision>()))
            .ReturnsAsync(50001);
        fixture.ReviewEvents.Setup(item => item.AddAsync(It.IsAny<WikiDocumentReviewEvent>()))
            .ReturnsAsync(60001);

        var result = await fixture.Service.AdminReviewDraftAsync(
            fixture.Draft.Id,
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
        Assert.Equal(2, fixture.Document.Version);
        fixture.Documents.Verify(item => item.ApplyDraftToDocumentAsync(
            It.Is<WikiDraftApplyCommand>(command =>
                ReferenceEquals(command.Draft, fixture.Draft) &&
                command.Draft.BaseDocumentVersion == 1)), Times.Once);
    }

    [Fact]
    public async Task AuthorSaveDraft_ShouldSynchronizeContentAndCoverReferencesAfterCompareAndSet()
    {
        var fixture = CreateFixture();
        fixture.Documents
            .Setup(item => item.QueryExistsAsync(
                It.IsAny<Expression<Func<WikiDocument, bool>>>()))
            .ReturnsAsync(false);
        fixture.Documents
            .Setup(item => item.SaveDraftAsync(It.IsAny<WikiDraftSaveCommand>()))
            .ReturnsAsync(1);

        var result = await fixture.Service.AuthorSaveDraftAsync(
            30001,
            new SaveWikiAuthorDraftDto
            {
                Title = "Draft",
                Slug = "draft",
                MarkdownContent = "![asset](attachment://8001)",
                ExpectedDraftVersion = 1
            },
            10001,
            "Owner",
            0);

        Assert.Equal(2, result.VoDraftVersion);
        Assert.Equal("usr_owner", result.VoOwnerUserPublicId);
        fixture.AttachmentReferences.Verify(repository => repository.SyncSourceAsync(
            It.Is<WikiAttachmentReferenceSyncCommand>(command =>
                command.ReferenceKind == (int)WikiAttachmentReferenceKind.DraftContent &&
                command.ReferenceSourceId == 30001 &&
                command.AttachmentIds.SequenceEqual(new long[] { 8001 }))), Times.Once);
        fixture.AttachmentReferences.Verify(repository => repository.SyncSourceAsync(
            It.Is<WikiAttachmentReferenceSyncCommand>(command =>
                command.ReferenceKind == (int)WikiAttachmentReferenceKind.DraftCover &&
                command.AttachmentIds.Count == 0)), Times.Once);
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
        var attachments = new Mock<IBaseRepository<Attachment>>();
        var attachmentReferences = new Mock<IWikiAttachmentReferenceRepository>();
        var attachment = new Attachment
        {
            Id = 8001,
            TenantId = 0,
            UploaderId = ownerUserId,
            BusinessType = AttachmentBusinessTypes.Wiki,
            IsEnabled = true
        };
        attachments
            .Setup(item => item.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>?>()))
            .ReturnsAsync((Expression<Func<Attachment, bool>>? expression) =>
                expression == null || expression.Compile()(attachment)
                    ? [attachment]
                    : []);
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
            attachments.Object,
            attachmentReferences.Object,
            consoleAuthorization.Object,
            Options.Create(new DocumentOptions()),
            drafts.Object,
            collaborators.Object,
            reviewEvents.Object,
            users.Object);
        return new Fixture(
            service,
            documents,
            revisions,
            drafts,
            collaborators,
            reviewEvents,
            attachmentReferences,
            document,
            draft);
    }

    private sealed record Fixture(
        WikiDocumentService Service,
        Mock<IWikiDocumentRepository> Documents,
        Mock<IBaseRepository<WikiDocumentRevision>> Revisions,
        Mock<IBaseRepository<WikiDocumentDraft>> Drafts,
        Mock<IBaseRepository<WikiDocumentCollaborator>> Collaborators,
        Mock<IBaseRepository<WikiDocumentReviewEvent>> ReviewEvents,
        Mock<IWikiAttachmentReferenceRepository> AttachmentReferences,
        WikiDocument Document,
        WikiDocumentDraft Draft);
}
