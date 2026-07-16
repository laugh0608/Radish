namespace Radish.Shared.Constants;

/// <summary>
/// 附件上传业务域的稳定错误码。
/// </summary>
public static class AttachmentErrorCodes
{
    public const string FileEmpty = "Attachment.FileEmpty";
    public const string ImageTypeUnsupported = "Attachment.ImageTypeUnsupported";
    public const string DocumentTypeUnsupported = "Attachment.DocumentTypeUnsupported";
    public const string UploadForbidden = "Attachment.UploadForbidden";
    public const string RateLimited = "Attachment.RateLimited";
    public const string FileTooLarge = "Attachment.FileTooLarge";
    public const string UnsupportedMediaType = "Attachment.UnsupportedMediaType";
    public const string ContentMismatch = "Attachment.ContentMismatch";
    public const string StorageFailed = "Attachment.StorageFailed";
    public const string ProcessingFailed = "Attachment.ProcessingFailed";

    public static string ResolveMessageKey(string errorCode)
    {
        return errorCode switch
        {
            FileEmpty => "error.attachment.file_empty",
            ImageTypeUnsupported => "error.attachment.image_type_unsupported",
            DocumentTypeUnsupported => "error.attachment.document_type_unsupported",
            UploadForbidden => "error.attachment.upload_forbidden",
            RateLimited => "error.attachment.rate_limited",
            FileTooLarge => "error.attachment.file_too_large",
            UnsupportedMediaType => "error.attachment.unsupported_media_type",
            ContentMismatch => "error.attachment.content_mismatch",
            StorageFailed => "error.attachment.storage_failed",
            ProcessingFailed => "error.attachment.processing_failed",
            _ => throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "Unknown attachment error code.")
        };
    }
}
