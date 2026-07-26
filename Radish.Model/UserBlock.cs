using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

public static class UserBlockOperationTypes
{
    public const string Block = "Block";
    public const string Unblock = "Unblock";
}

/// <summary>用户本人建立的方向性屏蔽关系，也是交互隔离的唯一真相源。</summary>
[SugarTable("UserBlock")]
[SugarIndex("idx_user_block_pair", nameof(TenantId), OrderByType.Asc, nameof(BlockerUserId), OrderByType.Asc, nameof(BlockedUserId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_user_block_mine", nameof(TenantId), OrderByType.Asc, nameof(BlockerUserId), OrderByType.Asc, nameof(IsDeleted), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc, nameof(Id), OrderByType.Desc)]
[SugarIndex("idx_user_block_target", nameof(TenantId), OrderByType.Asc, nameof(BlockedUserId), OrderByType.Asc, nameof(IsDeleted), OrderByType.Asc)]
public sealed class UserBlock : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long BlockerUserId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long BlockedUserId { get; set; }

    /// <summary>当前方向关系的单调版本；恢复和解除均递增。</summary>
    [SugarColumn(IsNullable = false)]
    public long RelationshipVersion { get; set; }

    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; }

    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}

/// <summary>Block / Unblock 的稳定 operation key 回放结果，不承担关系真相。</summary>
[SugarTable("UserBlockOperation")]
[SugarIndex("idx_user_block_operation_key", nameof(TenantId), OrderByType.Asc, nameof(ActorUserId), OrderByType.Asc, nameof(OperationKey), OrderByType.Asc, IsUnique = true)]
public sealed class UserBlockOperation : RootEntityTKey<long>, ITenantEntity
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long ActorUserId { get; set; }

    [SugarColumn(Length = 100, IsNullable = false)]
    public string OperationKey { get; set; } = string.Empty;

    [SugarColumn(Length = 16, IsNullable = false)]
    public string OperationType { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public long TargetUserId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long UserBlockId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long ResultRelationshipVersion { get; set; }

    [SugarColumn(IsNullable = false)]
    public bool ResultChanged { get; set; }

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }
}
