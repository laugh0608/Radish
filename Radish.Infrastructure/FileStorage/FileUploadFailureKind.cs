namespace Radish.Infrastructure.FileStorage;

/// <summary>
/// 文件存储层可预期的上传失败类别。
/// </summary>
public enum FileUploadFailureKind
{
    None = 0,
    FileTooLarge,
    UnsupportedType,
    ContentMismatch,
    StorageFailed
}
