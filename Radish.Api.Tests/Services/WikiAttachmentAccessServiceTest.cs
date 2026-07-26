using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.Common.PermissionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class WikiAttachmentAccessServiceTest
{
    [Fact]
    public async Task CanReadAsync_ShouldAllowAnonymousOnlyForPublishedPublicCurrentReference()
    {
        var fixture = CreateFixture(
            Reference(WikiAttachmentReferenceKind.DocumentContent),
            Document(
                WikiDocumentStatusEnum.Published,
                WikiDocumentVisibilityEnum.Public));

        Assert.True(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            9,
            null,
            []));

        fixture.Document.Status = (int)WikiDocumentStatusEnum.Draft;
        Assert.False(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            9,
            null,
            []));
    }

    [Fact]
    public async Task CanReadAsync_ShouldNotAllowAdministratorToBypassUnboundWikiAttachment()
    {
        var fixture = CreateFixture(null, null);
        var attachment = WikiAttachment();

        Assert.True(await fixture.Service.CanReadAsync(attachment, 9, 1001, []));
        Assert.False(await fixture.Service.CanReadAsync(
            attachment,
            9,
            2002,
            ["Admin"]));
    }

    [Fact]
    public async Task CanReadAsync_ShouldUseCurrentCollaboratorAndReviewerPermissionsForDraft()
    {
        var draftReference = Reference(WikiAttachmentReferenceKind.DraftContent);
        draftReference.ReferenceSourceId = 301;
        var fixture = CreateFixture(
            draftReference,
            Document(WikiDocumentStatusEnum.Draft, WikiDocumentVisibilityEnum.Authenticated),
            new WikiDocumentDraft
            {
                Id = 301,
                TenantId = 9,
                DocumentId = 201,
                Title = "Draft",
                Slug = "draft",
                MarkdownContent = "body"
            });

        fixture.Collaborators.Add(new WikiDocumentCollaborator
        {
            Id = 401,
            TenantId = 9,
            DocumentId = 201,
            UserId = 2002,
            InviteState = (int)WikiDocumentCollaboratorState.Accepted
        });
        Assert.True(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            9,
            2002,
            []));

        fixture.Collaborators[0].InviteState = (int)WikiDocumentCollaboratorState.Revoked;
        Assert.False(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            9,
            2002,
            []));

        fixture.PermissionKeys.Add(ConsolePermissions.DocsReview);
        Assert.True(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            9,
            3003,
            ["Reviewer"]));
    }

    [Fact]
    public async Task IsWikiControlledAsync_ShouldOnlyTreatReferencedLegacyDocumentAsWiki()
    {
        var fixture = CreateFixture(
            Reference(WikiAttachmentReferenceKind.DocumentContent),
            Document(
                WikiDocumentStatusEnum.Published,
                WikiDocumentVisibilityEnum.Public));
        var legacyDocument = WikiAttachment();
        legacyDocument.BusinessType = AttachmentBusinessTypes.Document;

        Assert.True(await fixture.Service.IsWikiControlledAsync(legacyDocument));
        Assert.True(await fixture.Service.CanReadAsync(legacyDocument, 9, null, []));

        fixture.References.Clear();
        Assert.False(await fixture.Service.IsWikiControlledAsync(legacyDocument));
    }

    [Fact]
    public async Task CanReadAsync_ShouldUseRestrictedRoleAndPermissionWithoutAdminBypass()
    {
        var document = Document(
            WikiDocumentStatusEnum.Published,
            WikiDocumentVisibilityEnum.Restricted);
        document.AllowedRoles = "|reader|";
        document.AllowedPermissions = $"|{ConsolePermissions.DocsView}|";
        var fixture = CreateFixture(
            Reference(WikiAttachmentReferenceKind.DocumentContent),
            document);

        Assert.True(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            9,
            2002,
            ["Reader"]));
        Assert.False(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            9,
            2002,
            ["Admin"]));

        fixture.PermissionKeys.Add(ConsolePermissions.DocsView);
        Assert.True(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            9,
            2002,
            ["Reviewer"]));
    }

    [Fact]
    public async Task CanReadAsync_ShouldKeepRevisionPrivateAndRejectInvalidTargetState()
    {
        var revisionReference = Reference(WikiAttachmentReferenceKind.RevisionContent);
        revisionReference.ReferenceSourceId = 601;
        var fixture = CreateFixture(
            revisionReference,
            Document(WikiDocumentStatusEnum.Published, WikiDocumentVisibilityEnum.Public),
            revision: new WikiDocumentRevision
            {
                Id = 601,
                TenantId = 9,
                DocumentId = 201,
                Version = 1,
                Title = "Revision",
                MarkdownContent = "![asset](attachment://101)"
            });

        Assert.False(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            9,
            null,
            []));
        Assert.True(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            9,
            1001,
            []));

        fixture.Document.IsDeleted = true;
        Assert.False(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            9,
            1001,
            []));

        var disabledAttachment = WikiAttachment();
        disabledAttachment.IsEnabled = false;
        Assert.False(await fixture.Service.CanReadAsync(
            disabledAttachment,
            9,
            1001,
            []));
        Assert.False(await fixture.Service.CanReadAsync(
            WikiAttachment(),
            10,
            1001,
            []));
    }

    [Fact]
    public async Task CanManageAsync_ShouldRequireOwnerOrWikiPermissionForBoundAttachment()
    {
        var fixture = CreateFixture(
            Reference(WikiAttachmentReferenceKind.DocumentContent),
            Document(WikiDocumentStatusEnum.Published, WikiDocumentVisibilityEnum.Public));

        Assert.True(await fixture.Service.CanManageAsync(
            WikiAttachment(),
            9,
            1001,
            []));
        Assert.False(await fixture.Service.CanManageAsync(
            WikiAttachment(),
            9,
            2002,
            ["Admin"]));

        fixture.PermissionKeys.Add(ConsolePermissions.DocsPermissions);
        Assert.True(await fixture.Service.CanManageAsync(
            WikiAttachment(),
            9,
            2002,
            ["Publisher"]));
    }

    [Fact]
    public async Task GetReadableAttachmentIdsAsync_ShouldEvaluateMultipleReferencesInOnePolicySnapshot()
    {
        var fixture = CreateFixture(
            Reference(WikiAttachmentReferenceKind.DocumentContent),
            Document(WikiDocumentStatusEnum.Published, WikiDocumentVisibilityEnum.Public));
        fixture.References.Add(new WikiAttachmentReference
        {
            Id = 502,
            TenantId = 9,
            DocumentId = 201,
            AttachmentId = 102,
            ReferenceKind = (int)WikiAttachmentReferenceKind.DocumentCover,
            ReferenceSourceId = 201
        });
        var secondAttachment = WikiAttachment();
        secondAttachment.Id = 102;

        var readableIds = await fixture.Service.GetReadableAttachmentIdsAsync(
            [WikiAttachment(), secondAttachment],
            9,
            null,
            []);

        Assert.Equal([101L, 102L], readableIds.Order());
    }

    private static Fixture CreateFixture(
        WikiAttachmentReference? reference,
        WikiDocument? document,
        WikiDocumentDraft? draft = null,
        WikiDocumentRevision? revision = null)
    {
        var references = reference == null
            ? new List<WikiAttachmentReference>()
            : [reference];
        var documents = document == null ? new List<WikiDocument>() : [document];
        var collaborators = new List<WikiDocumentCollaborator>();
        var permissionKeys = new List<string>();

        var referenceRepository = new Mock<IWikiAttachmentReferenceRepository>();
        referenceRepository
            .Setup(repository => repository.QueryActiveByAttachmentAsync(9, 101))
            .ReturnsAsync(() => references.ToList());
        referenceRepository
            .Setup(repository => repository.QueryActiveByAttachmentsAsync(
                9,
                It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync(() => references.ToList());
        var documentRepository = new Mock<IBaseRepository<WikiDocument>>();
        documentRepository
            .Setup(repository => repository.QueryAsync(
                It.IsAny<Expression<Func<WikiDocument, bool>>?>()))
            .ReturnsAsync((Expression<Func<WikiDocument, bool>>? expression) =>
                expression == null ? documents.ToList() : documents.Where(expression.Compile()).ToList());
        var draftRepository = new Mock<IBaseRepository<WikiDocumentDraft>>();
        draftRepository
            .Setup(repository => repository.QueryAsync(
                It.IsAny<Expression<Func<WikiDocumentDraft, bool>>?>()))
            .ReturnsAsync((Expression<Func<WikiDocumentDraft, bool>>? expression) =>
            {
                var drafts = draft == null ? new List<WikiDocumentDraft>() : [draft];
                return expression == null
                    ? drafts
                    : drafts.Where(expression.Compile()).ToList();
            });
        var revisionRepository = new Mock<IBaseRepository<WikiDocumentRevision>>();
        revisionRepository
            .Setup(repository => repository.QueryAsync(
                It.IsAny<Expression<Func<WikiDocumentRevision, bool>>?>()))
            .ReturnsAsync((Expression<Func<WikiDocumentRevision, bool>>? expression) =>
            {
                var revisions = revision == null ? new List<WikiDocumentRevision>() : [revision];
                return expression == null
                    ? revisions
                    : revisions.Where(expression.Compile()).ToList();
            });
        var collaboratorRepository = new Mock<IBaseRepository<WikiDocumentCollaborator>>();
        collaboratorRepository
            .Setup(repository => repository.QueryAsync(
                It.IsAny<Expression<Func<WikiDocumentCollaborator, bool>>?>()))
            .ReturnsAsync((Expression<Func<WikiDocumentCollaborator, bool>>? expression) =>
                expression == null
                    ? collaborators.ToList()
                    : collaborators.Where(expression.Compile()).ToList());
        var authorizationService = new Mock<IConsoleAuthorizationService>();
        authorizationService
            .Setup(service => service.GetPermissionKeysByRolesAsync(
                It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(() => permissionKeys.ToList());

        return new Fixture(
            new WikiAttachmentAccessService(
                referenceRepository.Object,
                documentRepository.Object,
                draftRepository.Object,
                revisionRepository.Object,
                collaboratorRepository.Object,
                authorizationService.Object),
            references,
            document ?? new WikiDocument(),
            collaborators,
            permissionKeys);
    }

    private static Attachment WikiAttachment() =>
        new()
        {
            Id = 101,
            TenantId = 9,
            UploaderId = 1001,
            BusinessType = AttachmentBusinessTypes.Wiki,
            IsPublic = false,
            IsEnabled = true
        };

    private static WikiAttachmentReference Reference(WikiAttachmentReferenceKind kind) =>
        new()
        {
            Id = 501,
            TenantId = 9,
            DocumentId = 201,
            AttachmentId = 101,
            ReferenceKind = (int)kind,
            ReferenceSourceId = 201
        };

    private static WikiDocument Document(
        WikiDocumentStatusEnum status,
        WikiDocumentVisibilityEnum visibility) =>
        new()
        {
            Id = 201,
            TenantId = 9,
            OwnerUserId = 1001,
            Title = "Guide",
            Slug = "guide",
            MarkdownContent = "![asset](attachment://101)",
            Status = (int)status,
            Visibility = (int)visibility,
            SourceType = "Custom"
        };

    private sealed record Fixture(
        WikiAttachmentAccessService Service,
        List<WikiAttachmentReference> References,
        WikiDocument Document,
        List<WikiDocumentCollaborator> Collaborators,
        List<string> PermissionKeys);
}
