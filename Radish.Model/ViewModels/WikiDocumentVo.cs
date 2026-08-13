namespace Radish.Model.ViewModels;

/// <summary>Wiki 文档列表项视图模型</summary>
public class WikiDocumentVo
{
    public long VoId { get; set; }
    public string VoTitle { get; set; } = string.Empty;
    public string VoSlug { get; set; } = string.Empty;
    public string? VoSummary { get; set; }
    public long? VoCoverAttachmentId { get; set; }
    public long? VoParentId { get; set; }
    public int VoSort { get; set; }
    public int VoStatus { get; set; }
    public int VoVisibility { get; set; }
    public List<string> VoAllowedRoles { get; set; } = new();
    public List<string> VoAllowedPermissions { get; set; } = new();
    public string VoSourceType { get; set; } = string.Empty;
    public string? VoSourcePath { get; set; }
    public int VoVersion { get; set; }
    public int VoGovernanceVersion { get; set; }
    public DateTime? VoPublishedAt { get; set; }
    public bool VoIsDeleted { get; set; }
    public DateTime? VoDeletedAt { get; set; }
    public string? VoDeletedBy { get; set; }
    public DateTime VoCreateTime { get; set; }
    public DateTime? VoModifyTime { get; set; }
}

/// <summary>Wiki 文档详情视图模型</summary>
public class WikiDocumentDetailVo
{
    public long VoId { get; set; }
    public string VoTitle { get; set; } = string.Empty;
    public string VoSlug { get; set; } = string.Empty;
    public string? VoSummary { get; set; }
    public string VoMarkdownContent { get; set; } = string.Empty;
    public long? VoCoverAttachmentId { get; set; }
    public long? VoParentId { get; set; }
    public int VoSort { get; set; }
    public int VoStatus { get; set; }
    public int VoVisibility { get; set; }
    public List<string> VoAllowedRoles { get; set; } = new();
    public List<string> VoAllowedPermissions { get; set; } = new();
    public string VoSourceType { get; set; } = string.Empty;
    public string? VoSourcePath { get; set; }
    public int VoVersion { get; set; }
    public int VoGovernanceVersion { get; set; }
    public DateTime? VoPublishedAt { get; set; }
    public bool VoIsDeleted { get; set; }
    public DateTime? VoDeletedAt { get; set; }
    public string? VoDeletedBy { get; set; }
    public DateTime VoCreateTime { get; set; }
    public DateTime? VoModifyTime { get; set; }
}

/// <summary>Wiki 文档树节点视图模型</summary>
public class WikiDocumentTreeNodeVo
{
    public long VoId { get; set; }
    public string VoTitle { get; set; } = string.Empty;
    public string VoSlug { get; set; } = string.Empty;
    public long? VoParentId { get; set; }
    public int VoSort { get; set; }
    public int VoStatus { get; set; }
    public int VoVisibility { get; set; }
    public List<WikiDocumentTreeNodeVo> VoChildren { get; set; } = new();
}

/// <summary>Wiki 文档版本列表项视图模型</summary>
public class WikiDocumentRevisionItemVo
{
    public long VoId { get; set; }
    public long VoDocumentId { get; set; }
    public int VoVersion { get; set; }
    public string VoTitle { get; set; } = string.Empty;
    public string? VoChangeSummary { get; set; }
    public string VoSourceType { get; set; } = string.Empty;
    public DateTime VoCreateTime { get; set; }
    public string VoCreateBy { get; set; } = string.Empty;
    public bool VoIsCurrent { get; set; }
}

/// <summary>Wiki 文档版本详情视图模型</summary>
public class WikiDocumentRevisionDetailVo
{
    public long VoId { get; set; }
    public long VoDocumentId { get; set; }
    public int VoVersion { get; set; }
    public string VoTitle { get; set; } = string.Empty;
    public string VoMarkdownContent { get; set; } = string.Empty;
    public string? VoChangeSummary { get; set; }
    public string VoSourceType { get; set; } = string.Empty;
    public DateTime VoCreateTime { get; set; }
    public string VoCreateBy { get; set; } = string.Empty;
    public long VoCreateId { get; set; }
    public bool VoIsCurrent { get; set; }
}

/// <summary>Wiki 文档追加式治理事件视图模型。</summary>
public sealed class WikiDocumentGovernanceEventVo
{
    public long VoId { get; set; }
    public long VoDocumentId { get; set; }
    public string VoAction { get; set; } = string.Empty;
    public int VoFromStatus { get; set; }
    public int VoToStatus { get; set; }
    public int VoFromVisibility { get; set; }
    public int VoToVisibility { get; set; }
    public List<string> VoFromAllowedRoles { get; set; } = [];
    public List<string> VoToAllowedRoles { get; set; } = [];
    public List<string> VoFromAllowedPermissions { get; set; } = [];
    public List<string> VoToAllowedPermissions { get; set; } = [];
    public bool VoFromIsDeleted { get; set; }
    public bool VoToIsDeleted { get; set; }
    public int VoFromDocumentVersion { get; set; }
    public int VoToDocumentVersion { get; set; }
    public int VoExpectedGovernanceVersion { get; set; }
    public int VoResultGovernanceVersion { get; set; }
    public long? VoSourceRevisionId { get; set; }
    public string VoReason { get; set; } = string.Empty;
    public long VoActorUserId { get; set; }
    public string VoActorName { get; set; } = string.Empty;
    public DateTime VoCreateTime { get; set; }
}

/// <summary>Wiki 文档治理写入的权威响应快照。</summary>
public sealed class WikiDocumentGovernanceMutationVo
{
    public WikiDocumentDetailVo VoDocument { get; set; } = new();
    public WikiDocumentGovernanceEventVo VoEvent { get; set; } = new();
}
