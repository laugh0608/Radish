using Asp.Versioning;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.CacheTool;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.IService.Base;
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
    private readonly ICaching _caching;

    public CommentHighlightController(
        IBaseService<CommentHighlight, CommentHighlightVo> highlightService,
        ICaching caching)
    {
        _highlightService = highlightService;
        _caching = caching;
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
        // 🚀 Redis 缓存优化：先查缓存
        var cacheKey = $"god_comments:post:{postId}";
        var cached = await _caching.GetAsync<List<CommentHighlightVo>>(cacheKey);

        if (cached != null && cached.Any())
        {
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功（缓存）",
                ResponseData = cached
            };
        }

        // 缓存未命中，查询数据库
        var godComments = await _highlightService.QueryAsync(
            h => h.PostId == postId &&
                 h.HighlightType == 1 &&
                 h.IsCurrent);

        // 在内存中按排名升序排序
        var sortedComments = godComments.OrderBy(h => h.VoRank).ToList();

        // 写入缓存（TTL 1小时）
        if (sortedComments.Any())
        {
            await _caching.SetAsync(cacheKey, sortedComments, TimeSpan.FromHours(1));
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = sortedComments
        };
    }

    /// <summary>
    /// 批量获取帖子的当前神评（每帖 Top1）
    /// </summary>
    /// <param name="postIds">帖子 ID 列表</param>
    /// <returns>帖子 ID → 神评（Top1）</returns>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel<Dictionary<long, CommentHighlightVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetCurrentGodCommentsBatch([FromBody] List<long> postIds)
    {
        if (postIds == null || postIds.Count == 0)
        {
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = new Dictionary<long, CommentHighlightVo>()
            };
        }

        var uniqueIds = postIds.Where(id => id > 0).Distinct().ToList();
        if (uniqueIds.Count == 0)
        {
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = new Dictionary<long, CommentHighlightVo>()
            };
        }

        var result = new Dictionary<long, CommentHighlightVo>();
        var missingIds = new List<long>();

        foreach (var postId in uniqueIds)
        {
            var cacheKey = $"god_comments:post:{postId}";
            var cached = await _caching.GetAsync<List<CommentHighlightVo>>(cacheKey);
            if (cached != null && cached.Any())
            {
                var top = cached.OrderBy(h => h.VoRank).FirstOrDefault();
                if (top != null)
                {
                    result[postId] = top;
                }
            }
            else
            {
                missingIds.Add(postId);
            }
        }

        if (missingIds.Count > 0)
        {
            var highlights = await _highlightService.QueryAsync(
                h => missingIds.Contains(h.PostId) &&
                     h.HighlightType == 1 &&
                     h.IsCurrent);

            foreach (var group in highlights.GroupBy(h => h.VoPostId))
            {
                var sorted = group.OrderBy(h => h.VoRank).ToList();
                if (sorted.Count == 0)
                {
                    continue;
                }

                var top = sorted[0];
                result[group.Key] = top;

                var cacheKey = $"god_comments:post:{group.Key}";
                await _caching.SetAsync(cacheKey, sorted, TimeSpan.FromHours(1));
            }
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = result
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
        // 🚀 Redis 缓存优化：先查缓存
        var cacheKey = $"sofas:parent:{parentCommentId}";
        var cached = await _caching.GetAsync<List<CommentHighlightVo>>(cacheKey);

        if (cached != null && cached.Any())
        {
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功（缓存）",
                ResponseData = cached
            };
        }

        // 缓存未命中，查询数据库
        var sofas = await _highlightService.QueryAsync(
            h => h.ParentCommentId == parentCommentId &&
                 h.HighlightType == 2 &&
                 h.IsCurrent);

        // 在内存中按排名升序排序
        var sortedSofas = sofas.OrderBy(h => h.VoRank).ToList();

        // 写入缓存（TTL 1小时）
        if (sortedSofas.Any())
        {
            await _caching.SetAsync(cacheKey, sortedSofas, TimeSpan.FromHours(1));
        }

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
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
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
            ResponseData = new JobTriggerResultVo
            {
                VoJobId = jobId,
                VoStatDate = statDate ?? DateTime.Today.AddDays(-1)
            }
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
        var sortedTrend = trend.OrderByDescending(h => h.VoStatDate).ToList();

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = sortedTrend
        };
    }
}
