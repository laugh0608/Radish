using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>Wiki 文档生命周期、访问策略与回滚的追加式治理事件。</summary>
[SugarTable("WikiDocumentGovernanceEvent")]
[SugarIndex("idx_wikigovernance_document_version", nameof(TenantId), OrderByType.Asc, nameof(DocumentId), OrderByType.Asc, nameof(ResultGovernanceVersion), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_wikigovernance_document_time", nameof(TenantId), OrderByType.Asc, nameof(DocumentId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
public sealed class WikiDocumentGovernanceEvent : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }

    public long DocumentId { get; set; }

    [SugarColumn(Length = 30, IsNullable = false)]
    public string Action { get; set; } = string.Empty;

    public int FromStatus { get; set; }

    public int ToStatus { get; set; }

    public int FromVisibility { get; set; }

    public int ToVisibility { get; set; }

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? FromAllowedRoles { get; set; }

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? ToAllowedRoles { get; set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? FromAllowedPermissions { get; set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? ToAllowedPermissions { get; set; }

    public bool FromIsDeleted { get; set; }

    public bool ToIsDeleted { get; set; }

    public int FromDocumentVersion { get; set; }

    public int ToDocumentVersion { get; set; }

    public int ExpectedGovernanceVersion { get; set; }

    public int ResultGovernanceVersion { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? SourceRevisionId { get; set; }

    [SugarColumn(Length = 500, IsNullable = false)]
    public string Reason { get; set; } = string.Empty;

    public long ActorUserId { get; set; }

    [SugarColumn(Length = 50, IsNullable = false)]
    public string ActorName { get; set; } = "System";

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
