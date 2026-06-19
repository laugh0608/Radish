using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.Model;

/// <summary>资产写操作幂等记录。</summary>
[SugarTable("OperationIdempotencyRecord")]
[SugarIndex(
    "idx_operation_idempotency_unique",
    nameof(TenantId), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc,
    nameof(OperationType), OrderByType.Asc,
    nameof(IdempotencyKey), OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_operation_idempotency_user_operation_time",
    nameof(UserId), OrderByType.Asc,
    nameof(OperationType), OrderByType.Asc,
    nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_operation_idempotency_expires", nameof(ExpiresAt), OrderByType.Asc)]
public class OperationIdempotencyRecord : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>租户 ID。</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "租户ID")]
    public long TenantId { get; set; } = 0;

    /// <summary>发起用户 ID。</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "发起用户ID")]
    public long UserId { get; set; }

    /// <summary>操作类型。</summary>
    [SugarColumn(Length = 40, IsNullable = false, ColumnDescription = "操作类型")]
    public string OperationType { get; set; } = string.Empty;

    /// <summary>客户端提交的幂等键。</summary>
    [SugarColumn(Length = 80, IsNullable = false, ColumnDescription = "幂等键")]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>规范化请求摘要的 SHA-256。</summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "请求摘要Hash")]
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>不含敏感信息的请求摘要 JSON。</summary>
    [SugarColumn(Length = 1000, IsNullable = false, ColumnDescription = "请求摘要")]
    public string RequestSummary { get; set; } = string.Empty;

    /// <summary>处理状态。</summary>
    [SugarColumn(Length = 20, IsNullable = false, ColumnDescription = "处理状态")]
    public string Status { get; set; } = OperationIdempotencyStatuses.Processing;

    /// <summary>成功后资源类型。</summary>
    [SugarColumn(Length = 40, IsNullable = true, ColumnDescription = "资源类型")]
    public string? ResourceType { get; set; }

    /// <summary>成功后资源 ID。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "资源ID")]
    public long? ResourceId { get; set; }

    /// <summary>成功后资源编号。</summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "资源编号")]
    public string? ResourceNo { get; set; }

    /// <summary>可安全返回的终态响应 JSON。</summary>
    [SugarColumn(Length = 4000, IsNullable = true, ColumnDescription = "响应载荷")]
    public string? ResponsePayload { get; set; }

    /// <summary>错误码。</summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "错误码")]
    public string? ErrorCode { get; set; }

    /// <summary>错误信息，不写入支付口令。</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "错误信息")]
    public string? ErrorMessage { get; set; }

    /// <summary>过期时间。</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "过期时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime ExpiresAt { get; set; }

    /// <summary>创建时间。</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者名称。</summary>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "创建者名称")]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 ID。</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "创建者ID")]
    public long CreateId { get; set; } = 0;

    /// <summary>完成时间。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "完成时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? CompleteTime { get; set; }

    /// <summary>修改时间。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者名称。</summary>
    [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "修改者名称")]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 ID。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改者ID")]
    public long? ModifyId { get; set; }
}
