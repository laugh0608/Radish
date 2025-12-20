using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;

namespace Radish.Api.Controllers.v1;

/// <summary>
/// 审计日志控制器
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("1.0")]
[Authorize(Policy = "SystemOrAdmin")] // 仅系统管理员和管理员可访问
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// 分页查询审计日志
    /// </summary>
    /// <param name="queryDto">查询参数</param>
    /// <returns>分页结果</returns>
    [HttpPost]
    public async Task<MessageModel<PageModel<AuditLogVo>>> QueryPage([FromBody] AuditLogQueryDto queryDto)
    {
        var result = await _auditLogService.QueryPageAsync(queryDto);
        return MessageModel<PageModel<AuditLogVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 根据 ID 获取审计日志详情
    /// </summary>
    /// <param name="id">日志 ID</param>
    /// <returns>日志详情</returns>
    [HttpGet("{id:long}")]
    public async Task<MessageModel<AuditLogVo>> GetById(long id)
    {
        var result = await _auditLogService.QueryByIdAsync(id);
        if (result == null)
        {
            return MessageModel<AuditLogVo>.Message(false, "日志不存在", default!);
        }

        return MessageModel<AuditLogVo>.Success("查询成功", result);
    }

    /// <summary>
    /// 根据用户 ID 查询审计日志
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>分页结果</returns>
    [HttpGet]
    public async Task<MessageModel<PageModel<AuditLogVo>>> QueryByUserId(
        [FromQuery] long userId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _auditLogService.QueryByUserIdAsync(userId, pageIndex, pageSize);
        return MessageModel<PageModel<AuditLogVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 根据操作类型查询审计日志
    /// </summary>
    /// <param name="operationType">操作类型</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>分页结果</returns>
    [HttpGet]
    public async Task<MessageModel<PageModel<AuditLogVo>>> QueryByOperationType(
        [FromQuery] string operationType,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _auditLogService.QueryByOperationTypeAsync(operationType, pageIndex, pageSize);
        return MessageModel<PageModel<AuditLogVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 根据时间范围查询审计日志
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>分页结果</returns>
    [HttpGet]
    public async Task<MessageModel<PageModel<AuditLogVo>>> QueryByTimeRange(
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _auditLogService.QueryByTimeRangeAsync(startTime, endTime, pageIndex, pageSize);
        return MessageModel<PageModel<AuditLogVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 获取操作类型统计
    /// </summary>
    /// <param name="startTime">开始时间（可选）</param>
    /// <param name="endTime">结束时间（可选）</param>
    /// <returns>操作类型统计</returns>
    [HttpGet]
    public async Task<MessageModel<Dictionary<string, int>>> GetOperationTypeStatistics(
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null)
    {
        var result = await _auditLogService.GetOperationTypeStatisticsAsync(startTime, endTime);
        return MessageModel<Dictionary<string, int>>.Success("查询成功", result);
    }

    /// <summary>
    /// 获取用户操作统计（Top N）
    /// </summary>
    /// <param name="startTime">开始时间（可选）</param>
    /// <param name="endTime">结束时间（可选）</param>
    /// <param name="topN">Top N 用户</param>
    /// <returns>用户操作统计</returns>
    [HttpGet]
    public async Task<MessageModel<Dictionary<string, int>>> GetUserStatistics(
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] int topN = 10)
    {
        var result = await _auditLogService.GetUserStatisticsAsync(startTime, endTime, topN);
        return MessageModel<Dictionary<string, int>>.Success("查询成功", result);
    }

    /// <summary>
    /// 获取所有操作类型常量
    /// </summary>
    /// <returns>操作类型列表</returns>
    [HttpGet]
    [AllowAnonymous] // 允许匿名访问，方便前端获取常量
    public MessageModel<List<string>> GetOperationTypes()
    {
        var operationTypes = typeof(AuditOperationType)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => f.GetValue(null)?.ToString())
            .Where(v => v != null)
            .Cast<string>()
            .ToList();

        return MessageModel<List<string>>.Success("查询成功", operationTypes);
    }

    /// <summary>
    /// 获取所有模块常量
    /// </summary>
    /// <returns>模块列表</returns>
    [HttpGet]
    [AllowAnonymous] // 允许匿名访问，方便前端获取常量
    public MessageModel<List<string>> GetModules()
    {
        var modules = typeof(AuditModule)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => f.GetValue(null)?.ToString())
            .Where(v => v != null)
            .Cast<string>()
            .ToList();

        return MessageModel<List<string>>.Success("查询成功", modules);
    }
}
