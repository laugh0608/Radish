using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.Model;

/// <summary>论坛内容提交意图记录。</summary>
[SugarTable("ContentSubmissionRecord")]
[SugarIndex(
    "idx_content_submission_client",
    nameof(TenantId), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc,
    nameof(OperationType), OrderByType.Asc,
    nameof(ClientSubmissionId), OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_content_submission_fingerprint",
    nameof(TenantId), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc,
    nameof(OperationType), OrderByType.Asc,
    nameof(ContentFingerprint), OrderByType.Asc,
    nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_content_submission_expires", nameof(ExpiresAt), OrderByType.Asc)]
[SugarIndex(
    "idx_content_submission_result",
    nameof(ResultType), OrderByType.Asc,
    nameof(ResultId), OrderByType.Asc)]
public class ContentSubmissionRecord : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>租户 ID。</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "租户ID")]
    public long TenantId { get; set; } = 0;

    /// <summary>提交用户 ID。</summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "提交用户ID")]
    public long UserId { get; set; }

    /// <summary>操作类型。</summary>
    [SugarColumn(Length = 40, IsNullable = false, ColumnDescription = "操作类型")]
    public string OperationType { get; set; } = string.Empty;

    /// <summary>客户端提交意图 ID；旧客户端由服务端生成短期记录 ID。</summary>
    [SugarColumn(Length = 80, IsNullable = false, ColumnDescription = "客户端提交意图ID")]
    public string ClientSubmissionId { get; set; } = string.Empty;

    /// <summary>目标对象类型。</summary>
    [SugarColumn(Length = 40, IsNullable = true, ColumnDescription = "目标对象类型")]
    public string? TargetType { get; set; }

    /// <summary>目标对象 ID。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "目标对象ID")]
    public long? TargetId { get; set; }

    /// <summary>规范化请求摘要的 SHA-256。</summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "请求摘要Digest")]
    public string RequestDigest { get; set; } = string.Empty;

    /// <summary>不含完整正文的请求摘要 JSON。</summary>
    [SugarColumn(Length = 1000, IsNullable = false, ColumnDescription = "请求摘要")]
    public string RequestSummary { get; set; } = string.Empty;

    /// <summary>短窗口内容指纹。</summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "内容指纹")]
    public string ContentFingerprint { get; set; } = string.Empty;

    /// <summary>处理状态。</summary>
    [SugarColumn(Length = 20, IsNullable = false, ColumnDescription = "处理状态")]
    public string Status { get; set; } = ContentSubmissionStatuses.Pending;

    /// <summary>成功后结果类型。</summary>
    [SugarColumn(Length = 40, IsNullable = true, ColumnDescription = "结果类型")]
    public string? ResultType { get; set; }

    /// <summary>成功后结果 ID。</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "结果ID")]
    public long? ResultId { get; set; }

    /// <summary>成功后公开 ID。</summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "公开ID")]
    public string? ResultPublicId { get; set; }

    /// <summary>错误码。</summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "错误码")]
    public string? ErrorCode { get; set; }

    /// <summary>错误信息。</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "错误信息")]
    public string? ErrorMessage { get; set; }

    /// <summary>记录过期时间。</summary>
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
    public long CreateId { get; set; }

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
