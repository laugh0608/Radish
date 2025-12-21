using SqlSugar;

namespace Radish.Model.LogModels;

/// <summary>
/// 业务审计日志模型类
/// 用于记录用户的敏感操作，如登录、登出、权限变更、数据删除等
/// </summary>
/// <remarks>
/// - 使用独立的 Log 数据库存储
/// - 按月分表，便于归档和查询
/// - 记录完整的请求和响应信息
/// </remarks>
[Tenant(configId: "Log")] // 使用 Log 数据库
[SplitTable(SplitType.Month)] // 按月分表
[SugarTable($@"{nameof(AuditLog)}_{{year}}{{month}}{{day}}")] // 标准格式：AuditLog_20251220
public class AuditLog : BaseLog
{
    /// <summary>用户 ID</summary>
    /// <remarks>未认证用户为 0</remarks>
    [SugarColumn(IsNullable = true)]
    public long? UserId { get; set; }

    /// <summary>用户名</summary>
    [SugarColumn(IsNullable = true, Length = 100)]
    public string? UserName { get; set; }

    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? TenantId { get; set; }

    /// <summary>操作类型</summary>
    /// <remarks>如：Login、Logout、Create、Update、Delete、PermissionChange 等</remarks>
    [SugarColumn(IsNullable = false, Length = 50)]
    public string OperationType { get; set; } = string.Empty;

    /// <summary>操作模块</summary>
    /// <remarks>如：User、Role、Client、Post、Comment 等</remarks>
    [SugarColumn(IsNullable = true, Length = 100)]
    public string? Module { get; set; }

    /// <summary>操作描述</summary>
    /// <remarks>人类可读的操作描述，如"删除用户 admin"</remarks>
    [SugarColumn(IsNullable = true, Length = 500)]
    public string? Description { get; set; }

    /// <summary>客户端 IP 地址</summary>
    [SugarColumn(IsNullable = true, Length = 50)]
    public string? IpAddress { get; set; }

    /// <summary>User-Agent</summary>
    [SugarColumn(IsNullable = true, Length = 500)]
    public string? UserAgent { get; set; }

    /// <summary>请求路径</summary>
    /// <remarks>如：/api/v1/User/Delete/123</remarks>
    [SugarColumn(IsNullable = true, Length = 500)]
    public string? RequestPath { get; set; }

    /// <summary>请求方法</summary>
    /// <remarks>GET、POST、PUT、DELETE 等</remarks>
    [SugarColumn(IsNullable = true, Length = 10)]
    public string? RequestMethod { get; set; }

    /// <summary>请求体</summary>
    /// <remarks>敏感信息（如密码）应该被脱敏</remarks>
    [SugarColumn(IsNullable = true, ColumnDataType = "longtext,text,clob")]
    public string? RequestBody { get; set; }

    /// <summary>响应状态码</summary>
    [SugarColumn(IsNullable = true)]
    public int? ResponseStatusCode { get; set; }

    /// <summary>响应体</summary>
    /// <remarks>可选，仅记录关键操作的响应</remarks>
    [SugarColumn(IsNullable = true, ColumnDataType = "longtext,text,clob")]
    public string? ResponseBody { get; set; }

    /// <summary>执行时长（毫秒）</summary>
    [SugarColumn(IsNullable = true)]
    public long? Duration { get; set; }

    /// <summary>是否成功</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsSuccess { get; set; } = true;

    /// <summary>错误信息</summary>
    /// <remarks>操作失败时记录错误详情</remarks>
    [SugarColumn(IsNullable = true, ColumnDataType = "longtext,text,clob")]
    public string? ErrorMessage { get; set; }

    /// <summary>额外数据</summary>
    /// <remarks>JSON 格式，存储特定操作的额外信息</remarks>
    [SugarColumn(IsNullable = true, ColumnDataType = "longtext,text,clob")]
    public string? ExtraData { get; set; }
}
