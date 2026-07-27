using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>帖子版本的标签快照。</summary>
[SugarTable("PostContentRevisionTag")]
[SugarIndex(
    "idx_postcontentrevisiontag_tenant_revision_tag",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(RevisionId),
    OrderByType.Asc,
    nameof(TagId),
    OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_postcontentrevisiontag_tenant_revision_sort",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(RevisionId),
    OrderByType.Asc,
    nameof(SortOrder),
    OrderByType.Asc)]
public sealed class PostContentRevisionTag : RootEntityTKey<long>, ITenantEntity
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long RevisionId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long TagId { get; set; }

    [SugarColumn(Length = 50, IsNullable = false)]
    public string TagNameSnapshot { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public int SortOrder { get; set; }

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }
}
