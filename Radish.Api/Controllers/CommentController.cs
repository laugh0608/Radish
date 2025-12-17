using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// 论坛评论控制器
/// </summary>
/// <remarks>
/// 提供评论的查询、发表、点赞等接口
/// </remarks>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("论坛评论管理")]
public class CommentController : ControllerBase
{
    private readonly ICommentService _commentService;
    private readonly IHttpContextUser _httpContextUser;

    public CommentController(ICommentService commentService, IHttpContextUser httpContextUser)
    {
        _commentService = commentService;
        _httpContextUser = httpContextUser;
    }

    /// <summary>
    /// 获取帖子的评论树（带点赞状态）
    /// </summary>
    /// <param name="postId">帖子 ID</param>
    /// <returns>评论树（树形结构，包含当前用户的点赞状态）</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetCommentTree(long postId)
    {
        // 获取当前用户ID（如果已登录）
        long? userId = _httpContextUser.UserId > 0 ? _httpContextUser.UserId : null;

        // 获取带点赞状态的评论树
        var comments = await _commentService.GetCommentTreeWithLikeStatusAsync(postId, userId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = comments
        };
    }

    /// <summary>
    /// 发表评论
    /// </summary>
    /// <param name="request">评论信息</param>
    /// <returns>创建的评论 ID</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> Create([FromBody] CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "评论内容不能为空"
            };
        }

        var comment = new Comment(new CommentInitializationOptions(request.Content)
        {
            PostId = request.PostId,
            ParentId = request.ParentId,
            ReplyToUserId = request.ReplyToUserId,
            ReplyToUserName = request.ReplyToUserName,
            AuthorId = _httpContextUser.UserId,
            AuthorName = _httpContextUser.UserName,
            TenantId = _httpContextUser.TenantId
        });

        var commentId = await _commentService.AddCommentAsync(comment);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "评论成功",
            ResponseData = commentId
        };
    }

    /// <summary>
    /// 切换评论点赞状态（点赞/取消点赞）
    /// </summary>
    /// <param name="commentId">评论 ID</param>
    /// <returns>点赞操作结果（当前状态和最新点赞数）</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel<CommentLikeResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> ToggleLike(long commentId)
    {
        try
        {
            var result = await _commentService.ToggleLikeAsync(_httpContextUser.UserId, commentId);
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = result.IsLiked ? "点赞成功" : "取消点赞成功",
                ResponseData = result
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
    }

    /// <summary>
    /// 批量查询评论点赞状态
    /// </summary>
    /// <param name="commentIds">评论 ID 列表</param>
    /// <returns>点赞状态字典（评论ID → 是否已点赞）</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel<Dictionary<long, bool>>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetLikeStatus([FromBody] List<long> commentIds)
    {
        var likeStatus = await _commentService.GetUserLikeStatusAsync(_httpContextUser.UserId, commentIds);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = likeStatus
        };
    }

    /// <summary>
    /// 获取指定用户的评论列表
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量（默认 20）</param>
    /// <returns>分页评论列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetUserComments(
        long userId,
        int pageIndex = 1,
        int pageSize = 20)
    {
        // 参数校验
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // 查询用户的评论（只查询未删除的）
        var (data, totalCount) = await _commentService.QueryPageAsync(
            c => c.AuthorId == userId && !c.IsDeleted,
            pageIndex,
            pageSize,
            c => c.CreateTime,
            SqlSugar.OrderByType.Desc);

        // 构建分页模型
        var pageModel = new PageModel<CommentVo>
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
    /// 删除评论（软删除）
    /// </summary>
    /// <param name="commentId">评论 ID</param>
    /// <returns>操作结果</returns>
    [HttpDelete]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Delete(long commentId)
    {
        // 查询评论
        var comment = await _commentService.QueryFirstAsync(c => c.Id == commentId && !c.IsDeleted);
        if (comment == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "评论不存在"
            };
        }

        // 权限验证：只有作者本人或管理员可以删除
        var roles = _httpContextUser.GetClaimValueByType("role");
        var isAdmin = roles.Contains("Admin") || roles.Contains("System");
        if (comment.AuthorId != _httpContextUser.UserId && !isAdmin)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Forbidden,
                MessageInfo = "无权删除此评论"
            };
        }

        // 软删除：设置 IsDeleted = true，并记录删除者信息
        await _commentService.UpdateColumnsAsync(
            c => new Comment
            {
                IsDeleted = true,
                ModifyTime = DateTime.Now,
                ModifyBy = _httpContextUser.UserName,
                ModifyId = _httpContextUser.UserId
            },
            c => c.Id == commentId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "删除成功"
        };
    }
}

/// <summary>
/// 创建评论请求对象
/// </summary>
public class CreateCommentRequest
{
    /// <summary>评论内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>帖子 ID</summary>
    public long PostId { get; set; }

    /// <summary>父评论 ID（回复评论时使用）</summary>
    public long? ParentId { get; set; }

    /// <summary>被回复用户 ID（@某人时使用）</summary>
    public long? ReplyToUserId { get; set; }

    /// <summary>被回复用户名称</summary>
    public string? ReplyToUserName { get; set; }
}
