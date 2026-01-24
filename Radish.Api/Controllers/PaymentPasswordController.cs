using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Controllers.Base;
using Radish.IService;
using Radish.Model.ViewModels;

namespace Radish.Api.Controllers;

/// <summary>
/// 支付密码控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "Client")]
public class PaymentPasswordController : BaseApiController
{
    private readonly IPaymentPasswordService _paymentPasswordService;

    public PaymentPasswordController(IPaymentPasswordService paymentPasswordService)
    {
        _paymentPasswordService = paymentPasswordService;
    }

    /// <summary>
    /// 获取支付密码状态
    /// </summary>
    /// <returns>支付密码状态</returns>
    [HttpGet("GetStatus")]
    public async Task<MessageModel<UserPaymentPasswordVo?>> GetStatus()
    {
        var status = await _paymentPasswordService.GetPaymentPasswordStatusAsync(CurrentUserId);
        return Success(status);
    }

    /// <summary>
    /// 设置支付密码
    /// </summary>
    /// <param name="request">设置请求</param>
    /// <returns>设置结果</returns>
    [HttpPost("SetPassword")]
    public async Task<MessageModel<bool>> SetPassword([FromBody] SetPaymentPasswordRequest request)
    {
        var result = await _paymentPasswordService.SetPaymentPasswordAsync(CurrentUserId, request);
        return Success(result, result ? "支付密码设置成功" : "支付密码设置失败");
    }

    /// <summary>
    /// 修改支付密码
    /// </summary>
    /// <param name="request">修改请求</param>
    /// <returns>修改结果</returns>
    [HttpPost("ChangePassword")]
    public async Task<MessageModel<bool>> ChangePassword([FromBody] ChangePaymentPasswordRequest request)
    {
        var result = await _paymentPasswordService.ChangePaymentPasswordAsync(CurrentUserId, request);
        return Success(result, result ? "支付密码修改成功" : "支付密码修改失败");
    }

    /// <summary>
    /// 验证支付密码
    /// </summary>
    /// <param name="request">验证请求</param>
    /// <returns>验证结果</returns>
    [HttpPost("VerifyPassword")]
    public async Task<MessageModel<PaymentPasswordVerifyResult>> VerifyPassword([FromBody] VerifyPaymentPasswordRequest request)
    {
        var result = await _paymentPasswordService.VerifyPaymentPasswordAsync(CurrentUserId, request);
        return Success(result, result.IsSuccess ? "密码验证成功" : result.ErrorMessage);
    }

    /// <summary>
    /// 检查密码强度
    /// </summary>
    /// <param name="password">密码</param>
    /// <returns>强度等级</returns>
    [HttpPost("CheckStrength")]
    public MessageModel<object> CheckStrength([FromBody] string password)
    {
        var strength = _paymentPasswordService.CheckPasswordStrength(password);
        var strengthDisplay = strength switch
        {
            0 => "无效",
            1 => "很弱",
            2 => "弱",
            3 => "中等",
            4 => "强",
            5 => "很强",
            _ => "未知"
        };

        return Success(new PasswordStrengthVo
        {
            VoLevel = strength,
            VoDisplay = strengthDisplay,
            VoIsValid = strength > 0
        });
    }

    /// <summary>
    /// 获取安全建议
    /// </summary>
    /// <returns>安全建议列表</returns>
    [HttpGet("GetSecuritySuggestions")]
    public async Task<MessageModel<List<string>>> GetSecuritySuggestions()
    {
        var suggestions = await _paymentPasswordService.GenerateSecuritySuggestionsAsync(CurrentUserId);
        return Success(suggestions);
    }

    #region 管理员接口

    /// <summary>
    /// 重置用户支付密码（管理员）
    /// </summary>
    /// <param name="request">重置请求</param>
    /// <returns>重置结果</returns>
    [HttpPost("Admin/ResetPassword")]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<bool>> AdminResetPassword([FromBody] ResetPaymentPasswordRequest request)
    {
        var result = await _paymentPasswordService.ResetPaymentPasswordAsync(CurrentUserId, request);
        return Success(result, result ? "重置支付密码成功" : "重置支付密码失败");
    }

    /// <summary>
    /// 解锁用户支付密码（管理员）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="reason">解锁原因</param>
    /// <returns>解锁结果</returns>
    [HttpPost("Admin/UnlockPassword")]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<bool>> AdminUnlockPassword([FromQuery] long userId, [FromBody] string reason)
    {
        var result = await _paymentPasswordService.UnlockPaymentPasswordAsync(CurrentUserId, userId, reason);
        return Success(result, result ? "解锁支付密码成功" : "解锁支付密码失败");
    }

    /// <summary>
    /// 获取支付密码统计信息（管理员）
    /// </summary>
    /// <returns>统计信息</returns>
    [HttpGet("Admin/GetStats")]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<object>> AdminGetStats()
    {
        var stats = await _paymentPasswordService.GetPaymentPasswordStatsAsync();
        return Success(stats);
    }

    /// <summary>
    /// 清理过期锁定状态（管理员）
    /// </summary>
    /// <returns>清理结果</returns>
    [HttpPost("Admin/ClearExpiredLocks")]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<int>> AdminClearExpiredLocks()
    {
        var clearedCount = await _paymentPasswordService.ClearExpiredLocksAsync();
        return Success(clearedCount, $"清理了{clearedCount}个过期锁定状态");
    }

    #endregion
}