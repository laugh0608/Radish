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
    public string VoSourceType { get; set; } = string.Empty;
    public string? VoSourcePath { get; set; }
    public int VoVersion { get; set; }
    public DateTime? VoPublishedAt { get; set; }
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
    public string VoSourceType { get; set; } = string.Empty;
    public string? VoSourcePath { get; set; }
    public int VoVersion { get; set; }
    public DateTime? VoPublishedAt { get; set; }
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
    public List<WikiDocumentTreeNodeVo> VoChildren { get; set; } = new();
}
