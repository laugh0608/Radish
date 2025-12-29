using Asp.Versioning;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service.Jobs;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// 神评/沙发管理控制器
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("神评/沙发管理")]
public class CommentHighlightController : ControllerBase
{
    private readonly IBaseService<CommentHighlight, CommentHighlightVo> _highlightService;

    public CommentHighlightController(
        IBaseService<CommentHighlight, CommentHighlightVo> highlightService)
    {
        _highlightService = highlightService;
    }

    /// <summary>
    /// 获取帖子的当前神评列表
    /// </summary>
    /// <param name="postId">帖子 ID</param>
    /// <returns>神评列表（按排名升序）</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel<List<CommentHighlightVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetCurrentGodComments(long postId)
    {
        var godComments = await _highlightService.QueryAsync(
            h => h.PostId == postId &&
                 h.HighlightType == 1 &&
                 h.IsCurrent);

        // 在内存中按排名升序排序
        var sortedComments = godComments.OrderBy(h => h.Rank).ToList();

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = sortedComments
        };
    }

    /// <summary>
    /// 获取父评论的当前沙发列表
    /// </summary>
    /// <param name="parentCommentId">父评论 ID</param>
    /// <returns>沙发列表（按排名升序）</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel<List<CommentHighlightVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetCurrentSofas(long parentCommentId)
    {
        var sofas = await _highlightService.QueryAsync(
            h => h.ParentCommentId == parentCommentId &&
                 h.HighlightType == 2 &&
                 h.IsCurrent);

        // 在内存中按排名升序排序
        var sortedSofas = sofas.OrderBy(h => h.Rank).ToList();

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = sortedSofas
        };
    }

    /// <summary>
    /// 检查评论是否为神评或沙发
    /// </summary>
    /// <param name="commentId">评论 ID</param>
    /// <returns>高亮信息（如果是神评/沙发）</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel<CommentHighlightVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> CheckHighlight(long commentId)
    {
        var highlight = await _highlightService.QueryFirstAsync(
            h => h.CommentId == commentId && h.IsCurrent);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = highlight != null ? "该评论是神评/沙发" : "该评论不是神评/沙发",
            ResponseData = highlight
        };
    }

    /// <summary>
    /// 获取评论的历史高亮记录
    /// </summary>
    /// <param name="commentId">评论 ID</param>
    /// <param name="pageIndex">页码（默认 1）</param>
    /// <param name="pageSize">每页数量（默认 20）</param>
    /// <returns>历史记录列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel<PageModel<CommentHighlightVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetCommentHistory(
        long commentId,
        int pageIndex = 1,
        int pageSize = 20)
    {
        var (data, totalCount) = await _highlightService.QueryPageAsync(
            h => h.CommentId == commentId,
            pageIndex,
            pageSize,
            h => h.StatDate,
            SqlSugar.OrderByType.Desc);

        var pageModel = new PageModel<CommentHighlightVo>
        {
            Page = pageIndex,
            PageSize = pageSize,
            DataCount = totalCount,
            PageCount = (int)Math.Ceiling(totalCount / (double)pageSize),
            Data = data
        };

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = pageModel
        };
    }

    /// <summary>
    /// 手动触发神评/沙发统计任务（管理员专用）
    /// </summary>
    /// <param name="statDate">统计日期（可选，默认昨天）</param>
    /// <returns>统计结果</returns>
    [HttpPost]
    [Authorize(Roles = "Admin,System")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public Task<MessageModel> TriggerStatJob(DateTime? statDate = null)
    {
        var jobId = BackgroundJob.Enqueue<CommentHighlightJob>(
            job => job.ExecuteAsync(statDate));

        return Task.FromResult(new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = $"统计任务已触发，任务 ID: {jobId}",
            ResponseData = new { jobId, statDate = statDate ?? DateTime.Today.AddDays(-1) }
        });
    }

    /// <summary>
    /// 获取帖子的神评历史趋势
    /// </summary>
    /// <param name="postId">帖子 ID</param>
    /// <param name="days">查询天数（默认 30 天）</param>
    /// <returns>历史趋势数据</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel<List<CommentHighlightVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetGodCommentTrend(long postId, int days = 30)
    {
        var startDate = DateTime.Today.AddDays(-days);
        var trend = await _highlightService.QueryAsync(
            h => h.PostId == postId &&
                 h.HighlightType == 1 &&
                 h.StatDate >= startDate);

        // 在内存中按统计日期降序排序
        var sortedTrend = trend.OrderByDescending(h => h.StatDate).ToList();

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = sortedTrend
        };
    }
}
