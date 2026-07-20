using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>Wiki 文档级编辑协作者邀请。</summary>
[SugarTable("WikiDocumentCollaborator")]
[SugarIndex("idx_wikicollab_document_user", nameof(TenantId), OrderByType.Asc, nameof(DocumentId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_wikicollab_user_state", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, nameof(InviteState), OrderByType.Asc)]
public sealed class WikiDocumentCollaborator : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    public long TenantId { get; set; }
    public long DocumentId { get; set; }
    public long UserId { get; set; }
    public int Role { get; set; } = (int)WikiDocumentCollaboratorRole.Editor;
    public int InviteState { get; set; } = (int)WikiDocumentCollaboratorState.Pending;
    public long InvitedBy { get; set; }
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    [SugarColumn(IsNullable = true)]
    public DateTime? RespondedAt { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? RevokedBy { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? RevokedAt { get; set; }
    public bool IsDeleted { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? DeletedAt { get; set; }
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";
    public long CreateId { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
