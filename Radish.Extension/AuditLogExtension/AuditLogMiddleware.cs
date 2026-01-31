using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;
using Radish.IService;
using Radish.IService.Base;
using Serilog;

namespace Radish.Extension.AuditLogExtension;

/// <summary>
/// 审计日志中间件
/// 记录敏感操作的请求和响应信息
/// </summary>
public class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AuditLogOptions _options;

    public AuditLogMiddleware(RequestDelegate next, AuditLogOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        // 检查是否需要审计
        if (!ShouldAudit(context))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        try
        {
            // 读取请求体
            var requestBody = await ReadRequestBodyAsync(context.Request);

            // 创建响应体缓冲区
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            // 执行请求
            await _next(context);

            stopwatch.Stop();

            // 读取响应体
            var responseBodyText = await ReadResponseBodyAsync(responseBody);

            // 恢复原始响应流
            await responseBody.CopyToAsync(originalBodyStream);

            // 记录审计日志（在请求上下文中执行，确保 ServiceProvider 可用）
            try
            {
                await LogAuditAsync(context, requestBody, responseBodyText, stopwatch.ElapsedMilliseconds, serviceProvider);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AuditLog] 记录审计日志失败");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // 记录失败的审计日志（在请求上下文中执行）
            try
            {
                await LogAuditAsync(context, null, null, stopwatch.ElapsedMilliseconds, serviceProvider, ex);
            }
            catch (Exception logEx)
            {
                Log.Error(logEx, "[AuditLog] 记录失败审计日志失败");
            }

            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    /// <summary>
    /// 判断是否需要审计
    /// </summary>
    private bool ShouldAudit(HttpContext context)
    {
        if (!_options.Enable)
        {
            return false;
        }

        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

        // 排除的路径
        if (_options.ExcludePaths.Any(p => path.StartsWith(p.ToLower())))
        {
            return false;
        }

        // 只审计指定的路径
        if (_options.IncludePaths.Any())
        {
            return _options.IncludePaths.Any(p => path.StartsWith(p.ToLower()));
        }

        // 只审计指定的 HTTP 方法
        if (_options.AuditMethods.Any())
        {
            return _options.AuditMethods.Contains(context.Request.Method.ToUpper());
        }

        return true;
    }

    /// <summary>
    /// 读取请求体
    /// </summary>
    private async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (!request.Body.CanSeek)
        {
            request.EnableBuffering();
        }

        request.Body.Position = 0;

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        request.Body.Position = 0;

        // 脱敏处理
        return SanitizeRequestBody(body);
    }

    /// <summary>
    /// 读取响应体
    /// </summary>
    private async Task<string?> ReadResponseBodyAsync(MemoryStream responseBody)
    {
        responseBody.Seek(0, SeekOrigin.Begin);
        var text = await new StreamReader(responseBody).ReadToEndAsync();
        responseBody.Seek(0, SeekOrigin.Begin);

        return text;
    }

    /// <summary>
    /// 脱敏请求体（移除密码等敏感信息）
    /// </summary>
    private string? SanitizeRequestBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        try
        {
            // 尝试解析为 JSON
            var jsonDoc = JsonDocument.Parse(body);
            var root = jsonDoc.RootElement;

            // 检查是否包含敏感字段
            var sensitiveFields = new[] { "password", "pwd", "secret", "token", "apikey", "api_key" };
            var hasSensitiveData = false;

            foreach (var field in sensitiveFields)
            {
                if (root.TryGetProperty(field, out _))
                {
                    hasSensitiveData = true;
                    break;
                }
            }

            if (hasSensitiveData)
            {
                // 简单处理：返回提示信息
                return "[包含敏感信息，已脱敏]";
            }

            return body;
        }
        catch
        {
            // 不是 JSON 格式，直接返回
            return body;
        }
    }

    /// <summary>
    /// 记录审计日志
    /// </summary>
    private async Task LogAuditAsync(
        HttpContext context,
        string? requestBody,
        string? responseBody,
        long duration,
        IServiceProvider serviceProvider,
        Exception? exception = null)
    {
        // 获取用户信息
        var userId = GetUserId(context);
        var userName = GetUserName(context);
        var tenantId = GetTenantId(context);

        // 获取 IP 地址
        var ipAddress = GetClientIpAddress(context);

        // 创建审计日志 DTO
        var auditLogDto = new CreateAuditLogDto
        {
            UserId = userId,
            UserName = userName,
            TenantId = tenantId,
            OperationType = DetermineOperationType(context),
            Module = DetermineModule(context),
            Description = GenerateDescription(context),
            IpAddress = ipAddress,
            UserAgent = context.Request.Headers["User-Agent"].ToString(),
            RequestPath = context.Request.Path,
            RequestMethod = context.Request.Method,
            RequestBody = requestBody,
            ResponseStatusCode = context.Response.StatusCode,
            ResponseBody = _options.LogResponseBody ? responseBody : null,
            Duration = duration,
            IsSuccess = exception == null && context.Response.StatusCode < 400,
            ErrorMessage = exception?.Message
        };

        // 使用 Scoped 服务
        using var scope = serviceProvider.CreateScope();
        var auditLogService = scope.ServiceProvider.GetService<IBaseService<AuditLog, AuditLogVo>>();

        if (auditLogService != null)
        {
            try
            {
                // 映射并保存
                var auditLog = new AuditLog
                {
                    UserId = auditLogDto.UserId,
                    UserName = auditLogDto.UserName,
                    TenantId = auditLogDto.TenantId,
                    OperationType = auditLogDto.OperationType,
                    Module = auditLogDto.Module,
                    Description = auditLogDto.Description,
                    IpAddress = auditLogDto.IpAddress,
                    UserAgent = auditLogDto.UserAgent,
                    RequestPath = auditLogDto.RequestPath,
                    RequestMethod = auditLogDto.RequestMethod,
                    RequestBody = auditLogDto.RequestBody,
                    ResponseStatusCode = auditLogDto.ResponseStatusCode,
                    ResponseBody = auditLogDto.ResponseBody,
                    Duration = auditLogDto.Duration,
                    IsSuccess = auditLogDto.IsSuccess,
                    ErrorMessage = auditLogDto.ErrorMessage,
                    DateTime = DateTime.Now,
                    Level = auditLogDto.IsSuccess ? "Information" : "Warning"
                };

                // 使用 AddSplitAsync 因为 AuditLog 有 SplitTable 特性
                // 注意：AddSplitAsync 返回 List<long>，我们只需要第一个 ID
                var ids = await auditLogService.AddSplitAsync(auditLog);

                if (ids != null && ids.Count > 0)
                {
                    auditLog.Id = ids[0];
                }

                if (_options.EnableLogging)
                {
                    Log.Information("[AuditLog] {OperationType} by {UserName} ({IpAddress}) - {RequestPath} - {StatusCode} - {Duration}ms",
                        auditLog.OperationType, auditLog.UserName ?? "Anonymous", auditLog.IpAddress,
                        auditLog.RequestPath, auditLog.ResponseStatusCode, auditLog.Duration);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AuditLog] 保存审计日志到数据库失败");
            }
        }
    }

    /// <summary>
    /// 获取用户 ID
    /// </summary>
    private long? GetUserId(HttpContext context)
    {
        var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? context.User?.FindFirst("sub")?.Value;

        return long.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    /// <summary>
    /// 获取用户名
    /// </summary>
    private string? GetUserName(HttpContext context)
    {
        return context.User?.FindFirst(ClaimTypes.Name)?.Value
               ?? context.User?.FindFirst("name")?.Value
               ?? context.User?.FindFirst("preferred_username")?.Value;
    }

    /// <summary>
    /// 获取租户 ID
    /// </summary>
    private long? GetTenantId(HttpContext context)
    {
        var tenantIdClaim = context.User?.FindFirst("tenant_id")?.Value
                            ?? context.User?.FindFirst("TenantId")?.Value;

        return long.TryParse(tenantIdClaim, out var tenantId) ? tenantId : null;
    }

    /// <summary>
    /// 获取客户端 IP 地址
    /// </summary>
    private string? GetClientIpAddress(HttpContext context)
    {
        // 优先从 X-Forwarded-For 获取（反向代理场景）
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // 从 X-Real-IP 获取
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // 从 RemoteIpAddress 获取
        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// 确定操作类型
    /// </summary>
    private string DetermineOperationType(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
        var method = context.Request.Method.ToUpper();

        // 根据路径和方法推断操作类型
        if (path.Contains("/login"))
            return "Login";
        if (path.Contains("/logout"))
            return "Logout";
        if (method == "POST" && !path.Contains("/login"))
            return "Create";
        if (method == "PUT" || method == "PATCH")
            return "Update";
        if (method == "DELETE")
            return "Delete";

        return "Access";
    }

    /// <summary>
    /// 确定操作模块
    /// </summary>
    private string? DetermineModule(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

        // 从路径中提取模块名
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
        {
            return segments[1]; // 通常是 /api/User/... 中的 User
        }

        return null;
    }

    /// <summary>
    /// 生成操作描述
    /// </summary>
    private string? GenerateDescription(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value;

        return $"{method} {path}";
    }
}
