using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// 萝卜币管理控制器
/// </summary>
/// <remarks>
/// 提供萝卜币余额查询、交易记录查询、管理员调账等接口。
/// </remarks>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = "Client")]
[Tags("萝卜币系统")]
public class CoinController : ControllerBase
{
    private readonly ICoinService _coinService;
    private readonly IHttpContextUser _httpContextUser;

    public CoinController(
        ICoinService coinService,
        IHttpContextUser httpContextUser)
    {
        _coinService = coinService;
        _httpContextUser = httpContextUser;
    }

    #region 余额查询

    /// <summary>
    /// 获取当前用户余额信息
    /// </summary>
    /// <returns>用户余额信息</returns>
    /// <remarks>
    /// 查询当前登录用户的萝卜币余额信息，包括可用余额、冻结余额、累计获得/消费等统计数据。
    /// </remarks>
    /// <response code="200">查询成功</response>
    /// <response code="401">未授权</response>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status401Unauthorized)]
    public async Task<MessageModel> GetBalance()
    {
        var userId = _httpContextUser.UserId;

        var balance = await _coinService.GetBalanceAsync(userId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取余额成功",
            ResponseData = balance
        };
    }

    /// <summary>
    /// 根据用户 ID 获取余额信息
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>用户余额信息</returns>
    /// <remarks>
    /// 查询指定用户的萝卜币余额信息（仅管理员可用）。
    /// </remarks>
    /// <response code="200">查询成功</response>
    /// <response code="401">未授权</response>
    /// <response code="403">权限不足</response>
    [HttpGet]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    public async Task<MessageModel> GetBalanceByUserId(long userId)
    {
        var balance = await _coinService.GetBalanceAsync(userId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取余额成功",
            ResponseData = balance
        };
    }

    #endregion

    #region 交易记录查询

    /// <summary>
    /// 获取当前用户交易记录（分页）
    /// </summary>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量（默认 20）</param>
    /// <param name="transactionType">交易类型（可选，用于筛选）</param>
    /// <param name="status">交易状态（可选，用于筛选）</param>
    /// <returns>分页的交易记录</returns>
    /// <remarks>
    /// 查询当前登录用户的萝卜币交易记录，支持分页和筛选。
    ///
    /// **交易类型枚举值**：
    /// - SYSTEM_GRANT: 系统赠送
    /// - LIKE_REWARD: 点赞奖励
    /// - COMMENT_REWARD: 评论奖励
    /// - TRANSFER: 用户转账
    /// - TIP: 打赏
    /// - CONSUME: 消费
    /// - REFUND: 退款
    /// - PENALTY: 惩罚扣除
    /// - ADMIN_ADJUST: 管理员调整
    ///
    /// **交易状态枚举值**：
    /// - PENDING: 待处理
    /// - SUCCESS: 成功
    /// - FAILED: 失败
    /// </remarks>
    /// <response code="200">查询成功</response>
    /// <response code="401">未授权</response>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status401Unauthorized)]
    public async Task<MessageModel> GetTransactions(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? transactionType = null,
        [FromQuery] string? status = null)
    {
        var userId = _httpContextUser.UserId;

        var transactions = await _coinService.GetTransactionsAsync(
            userId, pageIndex, pageSize, transactionType, status);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取交易记录成功",
            ResponseData = transactions
        };
    }

    /// <summary>
    /// 根据交易流水号获取交易详情
    /// </summary>
    /// <param name="transactionNo">交易流水号</param>
    /// <returns>交易详情</returns>
    /// <remarks>
    /// 查询指定交易流水号的详细信息。
    /// </remarks>
    /// <response code="200">查询成功</response>
    /// <response code="401">未授权</response>
    /// <response code="404">交易记录不存在</response>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetTransactionByNo(string transactionNo)
    {
        var transaction = await _coinService.GetTransactionByNoAsync(transactionNo);

        if (transaction == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "交易记录不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取交易详情成功",
            ResponseData = transaction
        };
    }

    #endregion

    #region 转账功能

    /// <summary>
    /// 用户转账
    /// </summary>
    /// <param name="request">转账请求参数</param>
    /// <returns>交易流水号</returns>
    /// <remarks>
    /// 用户之间转账萝卜币。
    ///
    /// **请求示例**：
    /// ```json
    /// {
    ///   "toUserId": 12345,
    ///   "amount": 100,
    ///   "remark": "感谢分享",
    ///   "paymentPassword": "******"
    /// }
    /// ```
    ///
    /// **参数说明**：
    /// - toUserId: 收款人用户 ID
    /// - amount: 转账金额（单位：胡萝卜），必须大于 0
    /// - remark: 备注信息，可选
    /// - paymentPassword: 支付密码，必填
    ///
    /// **注意事项**：
    /// - 不能向自己转账
    /// - 转账金额不能超过当前余额
    /// - 支付密码错误会导致转账失败
    /// - 支付密码连续错误 5 次会被锁定 30 分钟
    /// </remarks>
    /// <response code="200">转账成功</response>
    /// <response code="400">参数错误（如余额不足、支付密码错误）</response>
    /// <response code="401">未授权</response>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status401Unauthorized)]
    public async Task<MessageModel> Transfer([FromBody] TransferDto request)
    {
        try
        {
            var fromUserId = _httpContextUser.UserId;

            var transactionNo = await _coinService.TransferAsync(
                fromUserId,
                request.ToUserId,
                request.Amount,
                request.PaymentPassword,
                request.Remark
            );

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "转账成功",
                ResponseData = new TransactionResultVo { VoTransactionNo = transactionNo }
            };
        }
        catch (InvalidOperationException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = ex.Message
            };
        }
        catch (ArgumentException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = ex.Message
            };
        }
    }

    #endregion

    #region 统计数据

    /// <summary>
    /// 获取当前用户统计数据
    /// </summary>
    /// <param name="timeRange">时间范围（month/quarter/year，默认 month）</param>
    /// <returns>统计数据</returns>
    /// <remarks>
    /// 查询当前登录用户的萝卜币统计数据，包括趋势数据和分类统计。
    ///
    /// **时间范围说明**：
    /// - month: 最近一个月
    /// - quarter: 最近三个月
    /// - year: 最近一年
    ///
    /// **返回数据说明**：
    /// - trendData: 按日期统计的收入和支出趋势
    /// - categoryStats: 按交易类型统计的金额和次数
    /// </remarks>
    /// <response code="200">查询成功</response>
    /// <response code="401">未授权</response>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status401Unauthorized)]
    public async Task<MessageModel> GetStatistics([FromQuery] string timeRange = "month")
    {
        var userId = _httpContextUser.UserId;

        var statistics = await _coinService.GetStatisticsAsync(userId, timeRange);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取统计数据成功",
            ResponseData = statistics
        };
    }

    #endregion

    #region 管理员操作

    /// <summary>
    /// 管理员调整用户余额
    /// </summary>
    /// <param name="request">调账请求参数</param>
    /// <returns>交易流水号</returns>
    /// <remarks>
    /// 管理员手动调整用户余额（增加或减少）。
    /// 仅 System 或 Admin 角色可用。
    ///
    /// **请求示例**：
    /// ```json
    /// {
    ///   "userId": 12345,
    ///   "deltaAmount": 100,
    ///   "reason": "活动奖励"
    /// }
    /// ```
    ///
    /// **参数说明**：
    /// - deltaAmount: 正数表示增加余额，负数表示减少余额（单位：胡萝卜）
    /// - reason: 调整原因，必填
    /// </remarks>
    /// <response code="200">调整成功</response>
    /// <response code="400">参数错误（如余额不足）</response>
    /// <response code="401">未授权</response>
    /// <response code="403">权限不足</response>
    [HttpPost]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    public async Task<MessageModel> AdminAdjustBalance([FromBody] AdminAdjustBalanceDto request)
    {
        try
        {
            var operatorId = _httpContextUser.UserId;
            var operatorName = _httpContextUser.UserName;

            var transactionNo = await _coinService.AdminAdjustBalanceAsync(
                request.UserId,
                request.DeltaAmount,
                request.Reason,
                operatorId,
                operatorName
            );

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "余额调整成功",
                ResponseData = new TransactionResultVo { VoTransactionNo = transactionNo }
            };
        }
        catch (InvalidOperationException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = ex.Message
            };
        }
        catch (ArgumentException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = ex.Message
            };
        }
    }

    #endregion
}
