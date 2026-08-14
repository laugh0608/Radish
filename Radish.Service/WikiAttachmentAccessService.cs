using Radish.Common.PermissionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

public sealed class WikiAttachmentAccessService : IWikiAttachmentAccessService
{
    private readonly IWikiAttachmentReferenceRepository _referenceRepository;
    private readonly IBaseRepository<WikiDocument> _documentRepository;
    private readonly IBaseRepository<WikiDocumentDraft> _draftRepository;
    private readonly IBaseRepository<WikiDocumentRevision> _revisionRepository;
    private readonly IBaseRepository<WikiDocumentCollaborator> _collaboratorRepository;
    private readonly IConsoleAuthorizationService _consoleAuthorizationService;

    public WikiAttachmentAccessService(
        IWikiAttachmentReferenceRepository referenceRepository,
        IBaseRepository<WikiDocument> documentRepository,
        IBaseRepository<WikiDocumentDraft> draftRepository,
        IBaseRepository<WikiDocumentRevision> revisionRepository,
        IBaseRepository<WikiDocumentCollaborator> collaboratorRepository,
        IConsoleAuthorizationService consoleAuthorizationService)
    {
        _referenceRepository = referenceRepository;
        _documentRepository = documentRepository;
        _draftRepository = draftRepository;
        _revisionRepository = revisionRepository;
        _collaboratorRepository = collaboratorRepository;
        _consoleAuthorizationService = consoleAuthorizationService;
    }

    public async Task<bool> IsWikiControlledAsync(Attachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        if (string.Equals(
                attachment.BusinessType,
                AttachmentBusinessTypes.Wiki,
                StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(
                attachment.BusinessType,
                AttachmentBusinessTypes.Document,
                StringComparison.Ordinal))
        {
            return false;
        }

        var references = await _referenceRepository.QueryActiveByAttachmentAsync(
            attachment.TenantId,
            attachment.Id);
        return references.Count > 0;
    }

    public async Task<bool> CanReadAsync(
        Attachment attachment,
        long tenantId,
        long? userId,
        IReadOnlyCollection<string>? roleNames)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var readableIds = await GetReadableAttachmentIdsAsync(
            [attachment],
            tenantId,
            userId,
            roleNames);
        return readableIds.Contains(attachment.Id);
    }

    public async Task<IReadOnlySet<long>> GetReadableAttachmentIdsAsync(
        IReadOnlyCollection<Attachment> attachments,
        long tenantId,
        long? userId,
        IReadOnlyCollection<string>? roleNames)
    {
        var normalizedTenantId = tenantId > 0 ? tenantId : 0;
        var candidateMap = attachments
            .Where(attachment =>
                attachment.Id > 0 &&
                attachment.TenantId == normalizedTenantId &&
                !attachment.IsDeleted &&
                attachment.IsEnabled)
            .GroupBy(attachment => attachment.Id)
            .ToDictionary(group => group.Key, group => group.First());
        if (candidateMap.Count == 0)
        {
            return new HashSet<long>();
        }

        var references = await _referenceRepository.QueryActiveByAttachmentsAsync(
            normalizedTenantId,
            candidateMap.Keys.ToList());
        var referencesByAttachment = references
            .Where(reference => candidateMap.ContainsKey(reference.AttachmentId))
            .GroupBy(reference => reference.AttachmentId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var readableIds = candidateMap.Values
            .Where(attachment =>
                !referencesByAttachment.ContainsKey(attachment.Id) &&
                string.Equals(
                    attachment.BusinessType,
                    AttachmentBusinessTypes.Wiki,
                    StringComparison.Ordinal) &&
                userId.HasValue &&
                attachment.UploaderId == userId.Value)
            .Select(attachment => attachment.Id)
            .ToHashSet();
        if (references.Count == 0)
        {
            return readableIds;
        }

        var documentIds = references.Select(reference => reference.DocumentId).Distinct().ToList();
        var documents = await _documentRepository.QueryAsync(document =>
            document.TenantId == normalizedTenantId &&
            documentIds.Contains(document.Id) &&
            !document.IsDeleted);
        var documentMap = documents.ToDictionary(document => document.Id);
        var collaboratorAccess = await GetCollaboratorDocumentAccessAsync(
            normalizedTenantId,
            documentIds,
            userId);
        var permissionKeys = await GetPermissionKeysAsync(roleNames);
        var draftIds = references
            .Where(reference => reference.ReferenceKind is
                (int)WikiAttachmentReferenceKind.DraftContent or
                (int)WikiAttachmentReferenceKind.DraftCover)
            .Select(reference => reference.ReferenceSourceId)
            .Distinct()
            .ToList();
        var draftDocumentMap = draftIds.Count == 0
            ? new Dictionary<long, long>()
            : (await _draftRepository.QueryAsync(draft =>
                    draft.TenantId == normalizedTenantId &&
                    draftIds.Contains(draft.Id) &&
                    !draft.IsDeleted))
                .Where(draft =>
                    documentMap.ContainsKey(draft.DocumentId))
                .ToDictionary(draft => draft.Id, draft => draft.DocumentId);
        var revisionIds = references
            .Where(reference =>
                reference.ReferenceKind == (int)WikiAttachmentReferenceKind.RevisionContent)
            .Select(reference => reference.ReferenceSourceId)
            .Distinct()
            .ToList();
        var revisionDocumentMap = revisionIds.Count == 0
            ? new Dictionary<long, long>()
            : (await _revisionRepository.QueryAsync(revision =>
                    revision.TenantId == normalizedTenantId &&
                    revisionIds.Contains(revision.Id)))
                .Where(revision =>
                    documentMap.ContainsKey(revision.DocumentId))
                .ToDictionary(revision => revision.Id, revision => revision.DocumentId);

        foreach (var (attachmentId, attachmentReferences) in referencesByAttachment)
        {
            foreach (var reference in attachmentReferences)
            {
                if (!documentMap.TryGetValue(reference.DocumentId, out var document))
                {
                    continue;
                }

                var isOwner = userId.HasValue && document.OwnerUserId == userId.Value;
                var isAcceptedCollaborator = collaboratorAccess.AcceptedDocumentIds.Contains(document.Id);
                var isPendingOrAcceptedCollaborator =
                    collaboratorAccess.PendingOrAcceptedDocumentIds.Contains(document.Id);
                if (reference.ReferenceKind is
                    (int)WikiAttachmentReferenceKind.DocumentContent or
                    (int)WikiAttachmentReferenceKind.DocumentCover)
                {
                    if (isOwner || isAcceptedCollaborator || permissionKeys.Contains(ConsolePermissions.DocsView) ||
                        CanReadPublishedDocument(document, userId, roleNames, permissionKeys))
                    {
                        readableIds.Add(attachmentId);
                        break;
                    }
                    continue;
                }

                if (reference.ReferenceKind is
                    (int)WikiAttachmentReferenceKind.DraftContent or
                    (int)WikiAttachmentReferenceKind.DraftCover)
                {
                    if (draftDocumentMap.GetValueOrDefault(reference.ReferenceSourceId) == document.Id &&
                        (isOwner || isPendingOrAcceptedCollaborator ||
                         permissionKeys.Contains(ConsolePermissions.DocsReview)))
                    {
                        readableIds.Add(attachmentId);
                        break;
                    }
                    continue;
                }

                if (reference.ReferenceKind == (int)WikiAttachmentReferenceKind.RevisionContent &&
                    revisionDocumentMap.GetValueOrDefault(reference.ReferenceSourceId) == document.Id &&
                    (isOwner || isPendingOrAcceptedCollaborator ||
                     permissionKeys.Contains(ConsolePermissions.DocsView)))
                {
                    readableIds.Add(attachmentId);
                    break;
                }
            }
        }

        return readableIds;
    }

    public async Task<bool> CanManageAsync(
        Attachment attachment,
        long tenantId,
        long userId,
        IReadOnlyCollection<string>? roleNames)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var normalizedTenantId = tenantId > 0 ? tenantId : 0;
        if (userId <= 0 || attachment.TenantId != normalizedTenantId ||
            attachment.IsDeleted || !attachment.IsEnabled)
        {
            return false;
        }

        var references = await _referenceRepository.QueryActiveByAttachmentAsync(
            normalizedTenantId,
            attachment.Id);
        if (references.Count == 0)
        {
            return string.Equals(
                       attachment.BusinessType,
                       AttachmentBusinessTypes.Wiki,
                       StringComparison.Ordinal) &&
                   attachment.UploaderId == userId;
        }

        var documentIds = references.Select(reference => reference.DocumentId).Distinct().ToList();
        var documents = await _documentRepository.QueryAsync(document =>
            document.TenantId == normalizedTenantId &&
            documentIds.Contains(document.Id) &&
            !document.IsDeleted);
        if (documents.Any(document => document.OwnerUserId == userId))
        {
            return true;
        }

        var permissionKeys = await GetPermissionKeysAsync(roleNames);
        return permissionKeys.Contains(ConsolePermissions.DocsPermissions);
    }

    private async Task<WikiCollaboratorDocumentAccess> GetCollaboratorDocumentAccessAsync(
        long tenantId,
        IReadOnlyCollection<long> documentIds,
        long? userId)
    {
        if (!userId.HasValue || userId.Value <= 0 || documentIds.Count == 0)
        {
            return WikiCollaboratorDocumentAccess.Empty;
        }

        var collaborators = await _collaboratorRepository.QueryAsync(collaborator =>
            collaborator.TenantId == tenantId &&
            collaborator.UserId == userId.Value &&
            documentIds.Contains(collaborator.DocumentId) &&
            (collaborator.InviteState == (int)WikiDocumentCollaboratorState.Pending ||
             collaborator.InviteState == (int)WikiDocumentCollaboratorState.Accepted) &&
            !collaborator.IsDeleted);
        return new WikiCollaboratorDocumentAccess(
            collaborators
                .Where(collaborator =>
                    collaborator.InviteState == (int)WikiDocumentCollaboratorState.Accepted)
                .Select(collaborator => collaborator.DocumentId)
                .ToHashSet(),
            collaborators.Select(collaborator => collaborator.DocumentId).ToHashSet());
    }

    private async Task<HashSet<string>> GetPermissionKeysAsync(
        IReadOnlyCollection<string>? roleNames)
    {
        var normalizedRoles = (roleNames ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedRoles.Length == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var permissions = await _consoleAuthorizationService.GetPermissionKeysByRolesAsync(normalizedRoles);
        return permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool CanReadPublishedDocument(
        WikiDocument document,
        long? userId,
        IReadOnlyCollection<string>? roleNames,
        IReadOnlySet<string> permissionKeys)
    {
        if (document.Status != (int)WikiDocumentStatusEnum.Published)
        {
            return false;
        }

        if (document.Visibility == (int)WikiDocumentVisibilityEnum.Public)
        {
            return true;
        }

        if (document.Visibility is (int)WikiDocumentVisibilityEnum.Authenticated or <= 0)
        {
            return userId.HasValue && userId.Value > 0;
        }

        if (document.Visibility != (int)WikiDocumentVisibilityEnum.Restricted ||
            !userId.HasValue || userId.Value <= 0)
        {
            return false;
        }

        var allowedRoles = SplitAccessList(document.AllowedRoles);
        if ((roleNames ?? []).Any(role => allowedRoles.Contains(role)))
        {
            return true;
        }

        var allowedPermissions = SplitAccessList(document.AllowedPermissions);
        return permissionKeys.Any(allowedPermissions.Contains);
    }

    private static HashSet<string> SplitAccessList(string? rawValue)
    {
        return (rawValue ?? string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record WikiCollaboratorDocumentAccess(
        HashSet<long> AcceptedDocumentIds,
        HashSet<long> PendingOrAcceptedDocumentIds)
    {
        public static WikiCollaboratorDocumentAccess Empty { get; } = new([], []);
    }
}
