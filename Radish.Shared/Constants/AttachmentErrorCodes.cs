namespace Radish.Shared.Constants;

/// <summary>
/// 附件上传业务域的稳定错误码。
/// </summary>
public static class AttachmentErrorCodes
{
    public const string FileEmpty = "Attachment.FileEmpty";
    public const string AttachmentNotFound = "Attachment.NotFound";
    public const string ImageTypeUnsupported = "Attachment.ImageTypeUnsupported";
    public const string DocumentTypeUnsupported = "Attachment.DocumentTypeUnsupported";
    public const string BusinessTypeUnsupported = "Attachment.BusinessTypeUnsupported";
    public const string UploadForbidden = "Attachment.UploadForbidden";
    public const string RateLimited = "Attachment.RateLimited";
    public const string ConcurrentUploadLimitReached = "Attachment.ConcurrentUploadLimitReached";
    public const string UploadFrequencyLimitReached = "Attachment.UploadFrequencyLimitReached";
    public const string DailyUploadSizeLimitReached = "Attachment.DailyUploadSizeLimitReached";
    public const string FileTooLarge = "Attachment.FileTooLarge";
    public const string UnsupportedMediaType = "Attachment.UnsupportedMediaType";
    public const string ContentMismatch = "Attachment.ContentMismatch";
    public const string StorageFailed = "Attachment.StorageFailed";
    public const string ProcessingFailed = "Attachment.ProcessingFailed";
    public const string ChunkedUploadDisabled = "Attachment.ChunkedUploadDisabled";
    public const string UploadSessionInvalid = "Attachment.UploadSessionInvalid";
    public const string UploadSessionNotFound = "Attachment.UploadSessionNotFound";
    public const string UploadSessionExpired = "Attachment.UploadSessionExpired";
    public const string UploadSessionStateConflict = "Attachment.UploadSessionStateConflict";
    public const string UploadChunkInvalid = "Attachment.UploadChunkInvalid";
    public const string UploadChunksIncomplete = "Attachment.UploadChunksIncomplete";

    public static string ResolveMessageKey(string errorCode)
    {
        return errorCode switch
        {
            FileEmpty => "error.attachment.file_empty",
            AttachmentNotFound => "error.attachment.not_found",
            ImageTypeUnsupported => "error.attachment.image_type_unsupported",
            DocumentTypeUnsupported => "error.attachment.document_type_unsupported",
            BusinessTypeUnsupported => "error.attachment.business_type_unsupported",
            UploadForbidden => "error.attachment.upload_forbidden",
            RateLimited => "error.attachment.rate_limited",
            ConcurrentUploadLimitReached => "error.attachment.concurrent_upload_limit_reached",
            UploadFrequencyLimitReached => "error.attachment.upload_frequency_limit_reached",
            DailyUploadSizeLimitReached => "error.attachment.daily_upload_size_limit_reached",
            FileTooLarge => "error.attachment.file_too_large",
            UnsupportedMediaType => "error.attachment.unsupported_media_type",
            ContentMismatch => "error.attachment.content_mismatch",
            StorageFailed => "error.attachment.storage_failed",
            ProcessingFailed => "error.attachment.processing_failed",
            ChunkedUploadDisabled => "error.attachment.chunked_upload_disabled",
            UploadSessionInvalid => "error.attachment.upload_session_invalid",
            UploadSessionNotFound => "error.attachment.upload_session_not_found",
            UploadSessionExpired => "error.attachment.upload_session_expired",
            UploadSessionStateConflict => "error.attachment.upload_session_state_conflict",
            UploadChunkInvalid => "error.attachment.upload_chunk_invalid",
            UploadChunksIncomplete => "error.attachment.upload_chunks_incomplete",
            _ => throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "Unknown attachment error code.")
        };
    }
}
