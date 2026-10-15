namespace Radish.Model;

/// <summary>固定文档启动同步的有限跳过原因；不携带目录或配置原文。</summary>
public enum WikiBuiltInSyncSkipReason
{
    None,
    Disabled,
    BothTablesMissing,
    DocumentTableMissing,
    RevisionTableMissing,
    DirectoryMissing,
    NoMarkdown
}
