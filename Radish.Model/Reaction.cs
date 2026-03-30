using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>表情回应实体</summary>
[SugarTable("Reaction")]
[SugarIndex("idx_reaction_target", nameof(TargetType), OrderByType.Asc, nameof(TargetId), OrderByType.Asc)]
[SugarIndex("idx_reaction_user_target", nameof(UserId), OrderByType.Asc, nameof(TargetType), OrderByType.Asc, nameof(TargetId), OrderByType.Asc)]
[SugarIndex("idx_reaction_user_target_emoji", nameof(UserId), OrderByType.Asc, nameof(TargetType), OrderByType.Asc, nameof(TargetId), OrderByType.Asc, nameof(EmojiValue), OrderByType.Asc, IsUnique = true)]
public class Reaction : RootEntityTKey<long>, IDeleteFilter, ITenantEntity
{
    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>用户名冗余字段</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>目标类型（Post/Comment）</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string TargetType { get; set; } = string.Empty;

    /// <summary>目标 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TargetId { get; set; }

    /// <summary>表情类型（unicode/sticker）</summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string EmojiType { get; set; } = string.Empty;

    /// <summary>表情值（如 😀 或 group/code）</summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string EmojiValue { get; set; } = string.Empty;

    /// <summary>贴纸附件快照 Id（sticker 可用）</summary>
    [SugarColumn(IsNullable = true)]
    public long? StickerAttachmentId { get; set; }

    /// <summary>是否软删除</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

    /// <summary>删除时间</summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    [SugarColumn(IsNullable = true)]
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除人</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>创建者</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; } = 0;

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
