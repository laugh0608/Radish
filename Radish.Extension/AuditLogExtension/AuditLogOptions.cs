using Radish.Common.OptionTool.Core;

namespace Radish.Extension.AuditLogExtension;

/// <summary>
/// 审计日志配置选项
/// </summary>
public class AuditLogOptions : IConfigurableOptions
{
    /// <summary>
    /// 是否启用审计日志
    /// </summary>
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 是否记录审计日志到 Serilog
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// 是否记录响应体
    /// </summary>
    /// <remarks>
    /// 响应体可能很大，建议仅在调试时启用
    /// </remarks>
    public bool LogResponseBody { get; set; } = false;

    /// <summary>
    /// 需要审计的 HTTP 方法
    /// </summary>
    /// <remarks>
    /// 为空表示审计所有方法
    /// 示例：["POST", "PUT", "DELETE"]
    /// </remarks>
    public List<string> AuditMethods { get; set; } = new();

    /// <summary>
    /// 需要审计的路径（前缀匹配）
    /// </summary>
    /// <remarks>
    /// 为空表示审计所有路径（除了排除的路径）
    /// 示例：["/api/User", "/api/Client"]
    /// </remarks>
    public List<string> IncludePaths { get; set; } = new();

    /// <summary>
    /// 排除的路径（前缀匹配）
    /// </summary>
    /// <remarks>
    /// 这些路径不会被审计
    /// 示例：["/health", "/metrics", "/swagger"]
    /// </remarks>
    public List<string> ExcludePaths { get; set; } = new()
    {
        "/health",
        "/ready",
        "/metrics",
        "/swagger",
        "/scalar",
        "/api/docs"
    };
}
