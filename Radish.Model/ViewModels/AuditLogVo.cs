namespace Radish.Model.ViewModels;

/// <summary>
/// 审计日志视图模型
/// </summary>
public class AuditLogVo
{
    /// <summary>日志 ID</summary>
    public long VoId { get; set; }

    /// <summary>日志记录时间</summary>
    public DateTime VoDateTime { get; set; }

    /// <summary>用户 ID</summary>
    public long? VoUserId { get; set; }

    /// <summary>用户名</summary>
    public string? VoUserName { get; set; }

    /// <summary>租户 ID</summary>
    public long? VoTenantId { get; set; }

    /// <summary>操作类型</summary>
    public string VoOperationType { get; set; } = string.Empty;

    /// <summary>操作模块</summary>
    public string? VoModule { get; set; }

    /// <summary>操作描述</summary>
    public string? VoDescription { get; set; }

    /// <summary>客户端 IP 地址</summary>
    public string? VoIpAddress { get; set; }

    /// <summary>User-Agent</summary>
    public string? VoUserAgent { get; set; }

    /// <summary>请求路径</summary>
    public string? VoRequestPath { get; set; }

    /// <summary>请求方法</summary>
    public string? VoRequestMethod { get; set; }

    /// <summary>响应状态码</summary>
    public int? VoResponseStatusCode { get; set; }

    /// <summary>执行时长（毫秒）</summary>
    public long? VoDuration { get; set; }

    /// <summary>是否成功</summary>
    public bool VoIsSuccess { get; set; }

    /// <summary>错误信息</summary>
    public string? VoErrorMessage { get; set; }

    /// <summary>日志级别</summary>
    public string VoLevel { get; set; } = "Information";
}

/// <summary>
/// 审计日志查询参数
/// </summary>
public class AuditLogQueryDto
{
    /// <summary>用户 ID</summary>
    public long? UserId { get; set; }

    /// <summary>用户名（模糊查询）</summary>
    public string? UserName { get; set; }

    /// <summary>操作类型</summary>
    public string? OperationType { get; set; }

    /// <summary>操作模块</summary>
    public string? Module { get; set; }

    /// <summary>IP 地址</summary>
    public string? IpAddress { get; set; }

    /// <summary>请求路径（模糊查询）</summary>
    public string? RequestPath { get; set; }

    /// <summary>是否成功</summary>
    public bool? IsSuccess { get; set; }

    /// <summary>开始时间</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>结束时间</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>页码（从 1 开始）</summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>每页数量</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>排序字段</summary>
    public string? OrderBy { get; set; } = "DateTime";

    /// <summary>排序方向（asc/desc）</summary>
    public string? OrderDirection { get; set; } = "desc";
}

/// <summary>
/// 创建审计日志 DTO
/// </summary>
public class CreateAuditLogDto
{
    /// <summary>用户 ID</summary>
    public long? UserId { get; set; }

    /// <summary>用户名</summary>
    public string? UserName { get; set; }

    /// <summary>租户 ID</summary>
    public long? TenantId { get; set; }

    /// <summary>操作类型</summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>操作模块</summary>
    public string? Module { get; set; }

    /// <summary>操作描述</summary>
    public string? Description { get; set; }

    /// <summary>客户端 IP 地址</summary>
    public string? IpAddress { get; set; }

    /// <summary>User-Agent</summary>
    public string? UserAgent { get; set; }

    /// <summary>请求路径</summary>
    public string? RequestPath { get; set; }

    /// <summary>请求方法</summary>
    public string? RequestMethod { get; set; }

    /// <summary>请求体</summary>
    public string? RequestBody { get; set; }

    /// <summary>响应状态码</summary>
    public int? ResponseStatusCode { get; set; }

    /// <summary>响应体</summary>
    public string? ResponseBody { get; set; }

    /// <summary>执行时长（毫秒）</summary>
    public long? Duration { get; set; }

    /// <summary>是否成功</summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>额外数据</summary>
    public string? ExtraData { get; set; }
}
