using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Radish.Api.Filters;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Radish.Shared.Security;

namespace Radish.Api.Controllers;

/// <summary>
/// 支付密码控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[ApiErrorContract]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class PaymentPasswordController : ControllerBase
{
    private readonly IPaymentPasswordService _paymentPasswordService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IStringLocalizer<Errors> _errorsLocalizer;

    public PaymentPasswordController(
        IPaymentPasswordService paymentPasswordService,
        ICurrentUserAccessor currentUserAccessor,
        IStringLocalizer<Errors> errorsLocalizer)
    {
        _paymentPasswordService = paymentPasswordService;
        _currentUserAccessor = currentUserAccessor;
        _errorsLocalizer = errorsLocalizer;
    }

    /// <summary>
    /// 获取支付密码状态
    /// </summary>
    /// <returns>支付密码状态</returns>
    [HttpGet("GetStatus")]
    public async Task<MessageModel<UserPaymentPasswordVo?>> GetStatus()
    {
        var status = await _paymentPasswordService.GetPaymentPasswordStatusAsync(_currentUserAccessor.Current.UserId);
        return MessageModel<UserPaymentPasswordVo?>.Success("查询成功", status);
    }

    /// <summary>
    /// 设置支付密码
    /// </summary>
    /// <param name="request">设置请求</param>
    /// <returns>设置结果</returns>
    [HttpPost("SetPassword")]
    [ProducesResponseType(typeof(MessageModel<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel<bool>), StatusCodes.Status409Conflict)]
    public async Task<MessageModel<bool>> SetPassword([FromBody] SetPaymentPasswordRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            var result = await _paymentPasswordService.SetPaymentPasswordAsync(_currentUserAccessor.Current.UserId, request);
            return MessageModel<bool>.Success(result ? "支付口令设置成功" : "支付口令设置失败", result);
        });
    }

    /// <summary>
    /// 修改支付密码
    /// </summary>
    /// <param name="request">修改请求</param>
    /// <returns>修改结果</returns>
    [HttpPost("ChangePassword")]
    [ProducesResponseType(typeof(MessageModel<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MessageModel<bool>), StatusCodes.Status429TooManyRequests)]
    public async Task<MessageModel<bool>> ChangePassword([FromBody] ChangePaymentPasswordRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            var result = await _paymentPasswordService.ChangePaymentPasswordAsync(_currentUserAccessor.Current.UserId, request);
            return MessageModel<bool>.Success(result ? "支付口令修改成功" : "支付口令修改失败", result);
        });
    }

    /// <summary>
    /// 验证支付密码
    /// </summary>
    /// <param name="request">验证请求</param>
    /// <returns>验证结果</returns>
    [HttpPost("VerifyPassword")]
    [ProducesResponseType(typeof(MessageModel<PaymentPasswordVerifyResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel<PaymentPasswordVerifyResult>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel<PaymentPasswordVerifyResult>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MessageModel<PaymentPasswordVerifyResult>), StatusCodes.Status429TooManyRequests)]
    public async Task<MessageModel<PaymentPasswordVerifyResult>> VerifyPassword([FromBody] VerifyPaymentPasswordRequest request)
    {
        var result = await _paymentPasswordService.VerifyPaymentPasswordAsync(_currentUserAccessor.Current.UserId, request);
        if (!result.IsSuccess)
        {
            var statusCode = result.IsLocked
                ? HttpStatusCodeEnum.TooManyRequests
                : result.ErrorCode is PaymentPasscodeErrorCodes.NotConfigured or PaymentPasscodeErrorCodes.UpgradeRequired
                    ? HttpStatusCodeEnum.Conflict
                    : HttpStatusCodeEnum.BadRequest;
            return BuildError(
                statusCode,
                result.ErrorMessage ?? "支付口令验证失败",
                result.ErrorCode ?? PaymentPasscodeErrorCodes.Invalid,
                result.MessageKey ?? PaymentPasscodeErrorCodes.ResolveMessageKey(result.ErrorCode),
                result);
        }

        return MessageModel<PaymentPasswordVerifyResult>.Success(
            "支付口令验证成功",
            result);
    }

    /// <summary>
    /// 检查密码强度
    /// </summary>
    /// <param name="password">密码</param>
    /// <returns>强度等级</returns>
    [HttpPost("CheckStrength")]
    public MessageModel<PasswordStrengthVo> CheckStrength([FromBody] string password)
    {
        var strength = _paymentPasswordService.CheckPasswordStrength(password);
        var strengthDisplay = strength switch
        {
            0 => "未完成",
            1 => "无效",
            2 => "较弱",
            3 => "一般",
            4 => "稳妥",
            5 => "较强",
            _ => "未知"
        };

        return MessageModel<PasswordStrengthVo>.Success("检查完成", new PasswordStrengthVo
        {
            VoLevel = strength,
            VoDisplay = strengthDisplay,
            VoIsValid = PaymentPasscodeRules.IsAccepted(password)
        });
    }

    /// <summary>
    /// 获取安全建议
    /// </summary>
    /// <returns>安全建议列表</returns>
    [HttpGet("GetSecuritySuggestions")]
    public async Task<MessageModel<List<string>>> GetSecuritySuggestions()
    {
        var suggestions = await _paymentPasswordService.GenerateSecuritySuggestionsAsync(_currentUserAccessor.Current.UserId);
        return MessageModel<List<string>>.Success("查询成功", suggestions);
    }

    /// <summary>
    /// 获取当前用户支付密码安全日志
    /// </summary>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>支付密码安全日志分页结果</returns>
    [HttpGet("GetSecurityLogs")]
    public async Task<MessageModel<PageModel<PaymentPasswordSecurityLogVo>>> GetSecurityLogs(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var logs = await _paymentPasswordService.GetSecurityLogsAsync(
            _currentUserAccessor.Current.UserId,
            pageIndex,
            pageSize);
        return MessageModel<PageModel<PaymentPasswordSecurityLogVo>>.Success("查询成功", logs);
    }

    #region 管理员接口

    /// <summary>
    /// 重置用户支付密码（管理员）
    /// </summary>
    /// <param name="request">重置请求</param>
    /// <returns>重置结果</returns>
    [HttpPost("Admin/ResetPassword")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> AdminResetPassword([FromBody] ResetPaymentPasswordRequest request)
    {
        var result = await _paymentPasswordService.ResetPaymentPasswordAsync(_currentUserAccessor.Current.UserId, request);
        return MessageModel<bool>.Success(result ? "重置支付口令成功" : "重置支付口令失败", result);
    }

    /// <summary>
    /// 解锁用户支付密码（管理员）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="reason">解锁原因</param>
    /// <returns>解锁结果</returns>
    [HttpPost("Admin/UnlockPassword")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> AdminUnlockPassword([FromQuery] long userId, [FromBody] string reason)
    {
        var result = await _paymentPasswordService.UnlockPaymentPasswordAsync(_currentUserAccessor.Current.UserId, userId, reason);
        return MessageModel<bool>.Success(result ? "解锁支付口令成功" : "解锁支付口令失败", result);
    }

    /// <summary>
    /// 获取支付密码统计信息（管理员）
    /// </summary>
    /// <returns>统计信息</returns>
    [HttpGet("Admin/GetStats")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<PaymentPasswordStatsVo>> AdminGetStats()
    {
        var stats = await _paymentPasswordService.GetPaymentPasswordStatsAsync();
        return MessageModel<PaymentPasswordStatsVo>.Success("查询成功", stats);
    }

    /// <summary>
    /// 清理过期锁定状态（管理员）
    /// </summary>
    /// <returns>清理结果</returns>
    [HttpPost("Admin/ClearExpiredLocks")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<int>> AdminClearExpiredLocks()
    {
        var clearedCount = await _paymentPasswordService.ClearExpiredLocksAsync();
        return MessageModel<int>.Success($"清理了{clearedCount}个过期锁定状态", clearedCount);
    }

    #endregion

    private async Task<MessageModel<T>> ExecuteAsync<T>(Func<Task<MessageModel<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (BusinessException ex)
        {
            return BuildError<T>(
                (HttpStatusCodeEnum)ex.StatusCode,
                ex.Message,
                ex.ErrorCode ?? PaymentPasscodeErrorCodes.Invalid,
                ex.MessageKey ?? "error.payment_password.invalid");
        }
    }

    private MessageModel<T> BuildError<T>(
        HttpStatusCodeEnum statusCode,
        string fallbackMessage,
        string code,
        string messageKey,
        T? responseData = default)
    {
        var localizedMessage = _errorsLocalizer[messageKey];
        return new MessageModel<T>
        {
            IsSuccess = false,
            StatusCode = (int)statusCode,
            MessageInfo = localizedMessage.ResourceNotFound ? fallbackMessage : localizedMessage.Value,
            Code = code,
            MessageKey = messageKey,
            ResponseData = responseData
        };
    }
}
