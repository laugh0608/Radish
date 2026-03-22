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
