using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Api.Controllers.v1;

/// <summary>
/// 经验值系统控制器
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[ApiVersion(1)]
[Authorize(Policy = "Client")]
public class ExperienceController : ControllerBase
{
    private readonly IExperienceService _experienceService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExperienceController(
        IExperienceService experienceService,
        IHttpContextAccessor httpContextAccessor)
    {
        _experienceService = experienceService;
        _httpContextAccessor = httpContextAccessor;
    }

    #region 经验值查询

    /// <summary>
    /// 获取当前用户经验值信息
    /// </summary>
    /// <returns>用户经验值信息</returns>
    [HttpGet]
    public async Task<MessageModel<UserExperienceVo>> GetMyExperience()
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<UserExperienceVo>.Message(false, "未登录", default!);
        }

        var result = await _experienceService.GetUserExperienceAsync(userId);
        if (result == null)
        {
            return MessageModel<UserExperienceVo>.Message(false, "用户经验值信息不存在", default!);
        }

        return MessageModel<UserExperienceVo>.Success("查询成功", result);
    }

    /// <summary>
    /// 获取指定用户经验值信息
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>用户经验值信息</returns>
    [HttpGet("{userId:long}")]
    public async Task<MessageModel<UserExperienceVo>> GetUserExperience(long userId)
    {
        var result = await _experienceService.GetUserExperienceAsync(userId);
        if (result == null)
        {
            return MessageModel<UserExperienceVo>.Message(false, "用户经验值信息不存在", default!);
        }

        return MessageModel<UserExperienceVo>.Success("查询成功", result);
    }

    #endregion

    #region 等级配置

    /// <summary>
    /// 获取所有等级配置
    /// </summary>
    /// <returns>等级配置列表</returns>
    [HttpGet]
    [AllowAnonymous] // 等级配置可以公开查看
    public async Task<MessageModel<List<LevelConfigVo>>> GetLevelConfigs()
    {
        var result = await _experienceService.GetLevelConfigsAsync();
        return MessageModel<List<LevelConfigVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 获取指定等级的配置
    /// </summary>
    /// <param name="level">等级</param>
    /// <returns>等级配置</returns>
    [HttpGet("{level:int}")]
    [AllowAnonymous] // 等级配置可以公开查看
    public async Task<MessageModel<LevelConfigVo>> GetLevelConfig(int level)
    {
        var result = await _experienceService.GetLevelConfigAsync(level);
        if (result == null)
        {
            return MessageModel<LevelConfigVo>.Message(false, "等级配置不存在", default!);
        }

        return MessageModel<LevelConfigVo>.Success("查询成功", result);
    }

    #endregion

    #region 交易记录

    /// <summary>
    /// 获取当前用户经验值交易记录
    /// </summary>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="expType">经验值类型（可选）</param>
    /// <returns>分页的交易记录</returns>
    [HttpGet]
    public async Task<MessageModel<PageModel<ExpTransactionVo>>> GetMyTransactions(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? expType = null)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<PageModel<ExpTransactionVo>>.Message(false, "未登录", default!);
        }

        var result = await _experienceService.GetTransactionsAsync(userId, pageIndex, pageSize, expType);
        return MessageModel<PageModel<ExpTransactionVo>>.Success("查询成功", result);
    }

    #endregion

    #region 管理员操作

    /// <summary>
    /// 管理员调整用户经验值
    /// </summary>
    /// <param name="request">调整请求</param>
    /// <returns>是否成功</returns>
    [HttpPost]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<bool>> AdminAdjustExperience([FromBody] AdminAdjustExpRequest request)
    {
        var operatorId = GetCurrentUserId();
        var operatorName = GetCurrentUserName();

        if (operatorId <= 0)
        {
            return MessageModel<bool>.Message(false, "未登录", false);
        }

        var result = await _experienceService.AdminAdjustExperienceAsync(
            request.UserId,
            request.DeltaExp,
            request.Reason ?? "管理员调整",
            operatorId,
            operatorName ?? "Admin");

        return result
            ? MessageModel<bool>.Success("调整成功", true)
            : MessageModel<bool>.Message(false, "调整失败", false);
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 获取当前用户 ID
    /// </summary>
    private long GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return 0;
        }

        // 尝试从 sub claim 获取（OIDC 标准）
        var subClaim = httpContext.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(subClaim) && long.TryParse(subClaim, out var userId))
        {
            return userId;
        }

        // 尝试从 jti claim 获取（兼容）
        var jtiClaim = httpContext.User.FindFirst("jti")?.Value;
        if (!string.IsNullOrEmpty(jtiClaim) && long.TryParse(jtiClaim, out userId))
        {
            return userId;
        }

        return 0;
    }

    /// <summary>
    /// 获取当前用户名
    /// </summary>
    private string? GetCurrentUserName()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return httpContext.User.Identity.Name;
    }

    #endregion
}

/// <summary>
/// 管理员调整经验值请求
/// </summary>
public class AdminAdjustExpRequest
{
    /// <summary>用户 ID</summary>
    public long UserId { get; set; }

    /// <summary>经验值变动量（正数=增加，负数=减少）</summary>
    public int DeltaExp { get; set; }

    /// <summary>调整原因</summary>
    public string? Reason { get; set; }
}
