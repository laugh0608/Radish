using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.Model;
using Radish.Shared;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

public partial class WikiDocumentService
{
    private async Task ValidateWikiAttachmentReferencesAsync(
        long tenantId,
        long? documentId,
        string? markdownContent,
        long? coverAttachmentId,
        long operatorId)
    {
        var attachmentIds = AttachmentReferenceParser.ExtractAttachmentIds(markdownContent);
        if (coverAttachmentId.HasValue && coverAttachmentId.Value > 0)
        {
            attachmentIds.Add(coverAttachmentId.Value);
        }
        if (attachmentIds.Count == 0)
        {
            return;
        }

        var attachments = await _attachmentRepository.QueryAsync(attachment =>
            attachmentIds.Contains(attachment.Id) &&
            attachment.TenantId == tenantId &&
            attachment.IsEnabled &&
            !attachment.IsDeleted);
        if (attachments.Count != attachmentIds.Count)
        {
            throw WikiAttachmentError(
                "附件不存在、已停用或不属于当前租户",
                WikiAttachmentErrorCodes.InvalidReference);
        }

        foreach (var attachment in attachments)
        {
            if (string.Equals(
                    attachment.BusinessType,
                    AttachmentBusinessTypes.Wiki,
                    StringComparison.Ordinal))
            {
                if (attachment.UploaderId != operatorId &&
                    !await HasExistingDocumentReferenceAsync(attachment.Id, tenantId, documentId))
                {
                    throw WikiAttachmentError(
                        "无权引用该 Wiki 附件",
                        WikiAttachmentErrorCodes.ReferenceForbidden);
                }
                continue;
            }

            if (string.Equals(
                    attachment.BusinessType,
                    AttachmentBusinessTypes.Document,
                    StringComparison.Ordinal) &&
                await HasExistingDocumentReferenceAsync(attachment.Id, tenantId, documentId))
            {
                continue;
            }

            throw WikiAttachmentError(
                "附件业务类型不能用于 Wiki",
                WikiAttachmentErrorCodes.TypeMismatch);
        }
    }

    private async Task<bool> HasExistingDocumentReferenceAsync(
        long attachmentId,
        long tenantId,
        long? documentId)
    {
        if (!documentId.HasValue || documentId.Value <= 0)
        {
            return false;
        }

        var references = await _wikiAttachmentReferenceRepository.QueryActiveByAttachmentAsync(
            tenantId,
            attachmentId);
        return references.Any(reference => reference.DocumentId == documentId.Value);
    }

    private async Task SyncDocumentAttachmentReferencesAsync(
        WikiDocument document,
        long operatorId,
        string operatorName,
        DateTime nowUtc)
    {
        await SyncAttachmentReferenceSourceAsync(
            document.TenantId,
            document.Id,
            WikiAttachmentReferenceKind.DocumentContent,
            document.Id,
            AttachmentReferenceParser.ExtractAttachmentIds(document.MarkdownContent),
            operatorId,
            operatorName,
            nowUtc);
        await SyncAttachmentReferenceSourceAsync(
            document.TenantId,
            document.Id,
            WikiAttachmentReferenceKind.DocumentCover,
            document.Id,
            document.CoverAttachmentId is > 0 ? [document.CoverAttachmentId.Value] : [],
            operatorId,
            operatorName,
            nowUtc);
    }

    private async Task SyncDraftAttachmentReferencesAsync(
        WikiDocument document,
        WikiDocumentDraft draft,
        long operatorId,
        string operatorName,
        DateTime nowUtc)
    {
        await SyncAttachmentReferenceSourceAsync(
            document.TenantId,
            document.Id,
            WikiAttachmentReferenceKind.DraftContent,
            draft.Id,
            AttachmentReferenceParser.ExtractAttachmentIds(draft.MarkdownContent),
            operatorId,
            operatorName,
            nowUtc);
        await SyncAttachmentReferenceSourceAsync(
            document.TenantId,
            document.Id,
            WikiAttachmentReferenceKind.DraftCover,
            draft.Id,
            draft.CoverAttachmentId is > 0 ? [draft.CoverAttachmentId.Value] : [],
            operatorId,
            operatorName,
            nowUtc);
    }

    private async Task SyncRevisionAttachmentReferencesAsync(
        WikiDocument document,
        WikiDocumentRevision revision,
        long operatorId,
        string operatorName,
        DateTime nowUtc)
    {
        try
        {
            await _wikiAttachmentReferenceRepository.AppendRevisionAsync(
                new WikiAttachmentReferenceSyncCommand(
                    document.TenantId,
                    document.Id,
                    (int)WikiAttachmentReferenceKind.RevisionContent,
                    revision.Id,
                    AttachmentReferenceParser.ExtractAttachmentIds(revision.MarkdownContent),
                    operatorId,
                    ResolveOperatorName(operatorName),
                    nowUtc));
        }
        catch (WikiAttachmentReferenceConflictException)
        {
            throw WikiAttachmentError(
                "Wiki Revision 附件引用已经存在且内容不一致",
                WikiAttachmentErrorCodes.ReferenceConflict);
        }
    }

    private Task SyncAttachmentReferenceSourceAsync(
        long tenantId,
        long documentId,
        WikiAttachmentReferenceKind referenceKind,
        long referenceSourceId,
        IReadOnlyCollection<long> attachmentIds,
        long operatorId,
        string operatorName,
        DateTime nowUtc)
    {
        return _wikiAttachmentReferenceRepository.SyncSourceAsync(
            new WikiAttachmentReferenceSyncCommand(
                tenantId,
                documentId,
                (int)referenceKind,
                referenceSourceId,
                attachmentIds,
                operatorId,
                ResolveOperatorName(operatorName),
                nowUtc));
    }

    private static BusinessException WikiAttachmentError(
        string message,
        string code)
    {
        return new BusinessException(
            message,
            409,
            code,
            WikiAttachmentErrorCodes.ResolveMessageKey(code));
    }
}
