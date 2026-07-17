using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Radish.Api.Filters;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers.v1;

/// <summary>
/// 经验值系统控制器
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[ApiVersion(1)]
[ApiErrorContract]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class ExperienceController : ControllerBase
{
    private readonly IExperienceService _experienceService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IStringLocalizer<Errors> _errorsLocalizer;

    public ExperienceController(
        IExperienceService experienceService,
        ICurrentUserAccessor currentUserAccessor,
        IStringLocalizer<Errors> errorsLocalizer)
    {
        _experienceService = experienceService;
        _currentUserAccessor = currentUserAccessor;
        _errorsLocalizer = errorsLocalizer;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    #region 经验值查询

    /// <summary>
    /// 获取用户经验值信息
    /// </summary>
    /// <param name="userId">用户 ID（可选，不传则返回当前登录用户）</param>
    /// <returns>用户经验值信息</returns>
    [HttpGet]
    public async Task<MessageModel<UserExperienceVo>> GetMyExperience(long? userId = null)
    {
        var currentUserId = GetCurrentUserId();
        var targetUserId = userId ?? currentUserId;
        if (targetUserId <= 0)
        {
            return BuildError<UserExperienceVo>(
                HttpStatusCodeEnum.Unauthorized,
                "请先登录后再继续操作",
                ApiErrorCodes.Unauthorized,
                "error.auth.unauthorized");
        }

        if (userId.HasValue && userId.Value != currentUserId)
        {
            return BuildError<UserExperienceVo>(
                HttpStatusCodeEnum.Forbidden,
                "无权查看其他用户经验信息",
                "Experience.OtherUserForbidden",
                "error.experience.other_user_forbidden");
        }

        var result = await _experienceService.GetUserExperienceAsync(targetUserId);
        if (result == null)
        {
            return BuildError<UserExperienceVo>(
                HttpStatusCodeEnum.NotFound,
                "用户经验值信息不存在",
                "Experience.NotFound",
                "error.experience.not_found");
        }

        return MessageModel<UserExperienceVo>.Success("查询成功", result);
    }

    /// <summary>
    /// 管理端按用户查询经验值信息
    /// </summary>
    [HttpGet("{userId:long}")]
    [RequireConsolePermission(ConsolePermissions.ExperienceView)]
    public async Task<MessageModel<UserExperienceVo>> GetUserExperience(long userId)
    {
        if (userId <= 0)
        {
            return BuildError<UserExperienceVo>(
                HttpStatusCodeEnum.BadRequest,
                "用户 ID 无效",
                ApiErrorCodes.ValidationFailed,
                "error.common.validation_failed");
        }

        var result = await _experienceService.GetUserExperienceAsync(userId);
        if (result == null)
        {
            return BuildError<UserExperienceVo>(
                HttpStatusCodeEnum.NotFound,
                "用户经验值信息不存在",
                "Experience.NotFound",
                "error.experience.not_found");
        }

        return MessageModel<UserExperienceVo>.Success("查询成功", result);
    }

    /// <summary>
    /// 管理端按用户查询每日经验统计
    /// </summary>
    [HttpGet("{userId:long}")]
    [RequireConsolePermission(ConsolePermissions.ExperienceView)]
    public async Task<MessageModel<UserExpDailyStatsWindowVo>> GetUserDailyStats(long userId, [FromQuery] int days = 7)
    {
        if (userId <= 0)
        {
            return BuildError<UserExpDailyStatsWindowVo>(
                HttpStatusCodeEnum.BadRequest,
                "用户 ID 无效",
                ApiErrorCodes.ValidationFailed,
                "error.common.validation_failed");
        }

        var normalizedDays = days <= 0 ? 7 : Math.Min(days, 30);
        var result = await _experienceService.GetDailyStatsAsync(userId, normalizedDays);
        return MessageModel<UserExpDailyStatsWindowVo>.Success("查询成功", result);
    }

    /// <summary>
    /// 管理端按用户查询经验治理留痕
    /// </summary>
    [HttpGet("{userId:long}")]
    [RequireConsolePermission(ConsolePermissions.ExperienceView)]
    public async Task<MessageModel<List<UserExperienceGovernanceActionVo>>> GetUserGovernanceActions(
        long userId,
        [FromQuery] int take = 20)
    {
        if (userId <= 0)
        {
            return BuildError<List<UserExperienceGovernanceActionVo>>(
                HttpStatusCodeEnum.BadRequest,
                "用户 ID 无效",
                ApiErrorCodes.ValidationFailed,
                "error.common.validation_failed");
        }

        var result = await _experienceService.GetGovernanceActionsAsync(userId, take);
        return MessageModel<List<UserExperienceGovernanceActionVo>>.Success("查询成功", result);
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
    /// <param name="expType">经验值类型（可选，支持逗号分隔多个类型）</param>
    /// <param name="startDate">开始时间（可选）</param>
    /// <param name="endDate">结束时间（可选）</param>
    /// <returns>分页的交易记录</returns>
    [HttpGet]
    public async Task<MessageModel<PageModel<ExpTransactionVo>>> GetTransactions(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? expType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return BuildError<PageModel<ExpTransactionVo>>(
                HttpStatusCodeEnum.Unauthorized,
                "请先登录后再继续操作",
                ApiErrorCodes.Unauthorized,
                "error.auth.unauthorized");
        }

        var result = await _experienceService.GetTransactionsAsync(userId, pageIndex, pageSize, expType, startDate, endDate);
        return MessageModel<PageModel<ExpTransactionVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 管理端按用户查询经验值交易记录
    /// </summary>
    [HttpGet("{userId:long}")]
    [RequireConsolePermission(ConsolePermissions.ExperienceView)]
    public async Task<MessageModel<PageModel<ExpTransactionVo>>> GetUserTransactions(
        long userId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? expType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (userId <= 0)
        {
            return BuildError<PageModel<ExpTransactionVo>>(
                HttpStatusCodeEnum.BadRequest,
                "用户 ID 无效",
                ApiErrorCodes.ValidationFailed,
                "error.common.validation_failed");
        }

        var result = await _experienceService.GetTransactionsAsync(userId, pageIndex, pageSize, expType, startDate, endDate);
        return MessageModel<PageModel<ExpTransactionVo>>.Success("查询成功", result);
    }

    #endregion

    #region 排行榜

    /// <summary>
    /// 获取经验值排行榜
    /// </summary>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量（默认 50，最大 100）</param>
    /// <returns>分页的排行榜</returns>
    [HttpGet]
    [AllowAnonymous] // 排行榜可以公开查看
    public async Task<MessageModel<PageModel<LeaderboardItemVo>>> GetLeaderboard(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50)
    {
        var currentUserId = GetCurrentUserId(); // 获取当前用户 ID（如果已登录）
        var result = await _experienceService.GetLeaderboardAsync(pageIndex, pageSize, currentUserId > 0 ? currentUserId : null);
        return MessageModel<PageModel<LeaderboardItemVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 获取当前用户排名
    /// </summary>
    /// <returns>用户排名（0 表示未上榜）</returns>
    [HttpGet]
    public async Task<MessageModel<int>> GetMyRank()
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<int>.Message(false, "未登录", 0);
        }

        var rank = await _experienceService.GetUserRankAsync(userId);
        return MessageModel<int>.Success("查询成功", rank);
    }

    #endregion

    #region 管理员操作

    /// <summary>
    /// 管理员调整用户经验值
    /// </summary>
    /// <param name="request">调整请求</param>
    /// <returns>是否成功</returns>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.ExperienceAdjust)]
    public async Task<MessageModel<bool>> AdminAdjustExperience([FromBody] AdminAdjustExpDto request)
    {
        var operatorId = GetCurrentUserId();
        if (operatorId <= 0)
        {
            return BuildError<bool>(
                HttpStatusCodeEnum.Unauthorized,
                "请先登录后再继续操作",
                ApiErrorCodes.Unauthorized,
                "error.auth.unauthorized");
        }

        var result = await _experienceService.AdminAdjustExperienceAsync(
            request.UserId,
            request.DeltaExp,
            request.Reason ?? "管理员调整",
            operatorId,
            GetCurrentOperatorName());

        return result
            ? MessageModel<bool>.Success("调整成功", true)
            : BuildError<bool>(
                HttpStatusCodeEnum.BadRequest,
                "经验调整失败",
                "Experience.AdminAdjustRejected",
                "error.experience.admin_adjust_rejected");
    }

    /// <summary>
    /// 管理员冻结用户经验值
    /// </summary>
    /// <param name="request">冻结请求</param>
    /// <returns>是否成功</returns>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.ExperienceFreeze)]
    public async Task<MessageModel<bool>> AdminFreezeExperience([FromBody] AdminFreezeExperienceDto request)
    {
        var operatorId = GetCurrentUserId();
        if (operatorId <= 0)
        {
            return BuildError<bool>(
                HttpStatusCodeEnum.Unauthorized,
                "请先登录后再继续操作",
                ApiErrorCodes.Unauthorized,
                "error.auth.unauthorized");
        }

        if (request.FrozenUntil.HasValue && request.FrozenUntil.Value <= DateTime.Now)
        {
            return BuildError<bool>(
                HttpStatusCodeEnum.BadRequest,
                "冻结到期时间必须晚于当前时间",
                "Experience.FreezeUntilInvalid",
                "error.experience.freeze_until_invalid");
        }

        var result = await _experienceService.FreezeExperienceAsync(
            request.UserId,
            request.FrozenUntil,
            request.Reason.Trim(),
            operatorId,
            GetCurrentOperatorName());
        return result
            ? MessageModel<bool>.Success("冻结成功", true)
            : BuildError<bool>(
                HttpStatusCodeEnum.BadRequest,
                "经验冻结失败",
                "Experience.FreezeRejected",
                "error.experience.freeze_rejected");
    }

    /// <summary>
    /// 管理员解冻用户经验值
    /// </summary>
    /// <param name="request">解冻请求</param>
    /// <returns>是否成功</returns>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.ExperienceFreeze)]
    public async Task<MessageModel<bool>> AdminUnfreezeExperience([FromBody] AdminUnfreezeExperienceDto request)
    {
        var operatorId = GetCurrentUserId();
        if (operatorId <= 0)
        {
            return BuildError<bool>(
                HttpStatusCodeEnum.Unauthorized,
                "请先登录后再继续操作",
                ApiErrorCodes.Unauthorized,
                "error.auth.unauthorized");
        }

        var result = await _experienceService.UnfreezeExperienceAsync(
            request.UserId,
            operatorId,
            GetCurrentOperatorName());
        return result
            ? MessageModel<bool>.Success("解冻成功", true)
            : BuildError<bool>(
                HttpStatusCodeEnum.BadRequest,
                "经验解冻失败",
                "Experience.UnfreezeRejected",
                "error.experience.unfreeze_rejected");
    }

    /// <summary>
    /// 管理员记录经验治理人工复核结论
    /// </summary>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.ExperienceFreeze)]
    public async Task<MessageModel<bool>> AdminRecordGovernanceReview([FromBody] AdminRecordExperienceGovernanceReviewDto request)
    {
        var operatorId = GetCurrentUserId();
        if (operatorId <= 0)
        {
            return BuildError<bool>(
                HttpStatusCodeEnum.Unauthorized,
                "请先登录后再继续操作",
                ApiErrorCodes.Unauthorized,
                "error.auth.unauthorized");
        }

        var result = await _experienceService.RecordGovernanceReviewAsync(
            request,
            operatorId,
            GetCurrentOperatorName());

        return result
            ? MessageModel<bool>.Success("复核结论记录成功", true)
            : BuildError<bool>(
                HttpStatusCodeEnum.BadRequest,
                "复核结论记录失败",
                "Experience.ReviewRejected",
                "error.experience.review_rejected");
    }

    /// <summary>
    /// 管理员重新计算并更新所有等级配置
    /// </summary>
    /// <remarks>
    /// 根据 appsettings.json 中的 ExperienceCalculator 配置重新计算所有等级的经验值要求。
    /// 用于在修改配置参数后同步更新数据库中的等级配置。
    /// </remarks>
    /// <returns>更新后的等级配置列表</returns>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.ExperienceRecalculate)]
    public async Task<MessageModel<List<LevelConfigVo>>> RecalculateLevelConfigs()
    {
        var operatorId = GetCurrentUserId();
        if (operatorId <= 0)
        {
            return BuildError<List<LevelConfigVo>>(
                HttpStatusCodeEnum.Unauthorized,
                "请先登录后再继续操作",
                ApiErrorCodes.Unauthorized,
                "error.auth.unauthorized");
        }

        try
        {
            var result = await _experienceService.RecalculateLevelConfigsAsync(operatorId, GetCurrentOperatorName());
            return MessageModel<List<LevelConfigVo>>.Success($"成功重新计算 {result.Count} 个等级配置", result);
        }
        catch (Exception ex)
        {
            throw new BusinessException(
                "重新计算等级配置失败，请稍后重试",
                ex,
                StatusCodes.Status500InternalServerError,
                "System.UnexpectedError",
                "error.system.unexpected_error");
        }
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 获取当前用户 ID
    /// </summary>
    private long GetCurrentUserId() => Current.UserId;

    /// <summary>
    /// 获取当前用户名
    /// </summary>
    private string? GetCurrentUserName() => string.IsNullOrWhiteSpace(Current.UserName)
        ? null
        : Current.UserName;

    /// <summary>
    /// 获取当前操作者名称
    /// </summary>
    private string GetCurrentOperatorName() => GetCurrentUserName() ?? UserRoles.Admin;

    private MessageModel<T> BuildError<T>(
        HttpStatusCodeEnum statusCode,
        string fallbackMessage,
        string code,
        string messageKey)
    {
        var localizedMessage = _errorsLocalizer[messageKey];
        return new MessageModel<T>
        {
            IsSuccess = false,
            StatusCode = (int)statusCode,
            MessageInfo = localizedMessage.ResourceNotFound ? fallbackMessage : localizedMessage.Value,
            Code = code,
            MessageKey = messageKey
        };
    }

    #endregion
}
