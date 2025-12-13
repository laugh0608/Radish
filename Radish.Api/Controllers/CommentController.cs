using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
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
    /// 获取帖子的评论树
    /// </summary>
    /// <param name="postId">帖子 ID</param>
    /// <returns>评论树（树形结构）</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetCommentTree(long postId)
    {
        var comments = await _commentService.GetCommentTreeAsync(postId);
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
    /// 点赞/取消点赞评论
    /// </summary>
    /// <param name="commentId">评论 ID</param>
    /// <param name="isLike">true 为点赞，false 为取消点赞</param>
    /// <returns>操作结果</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Like(long commentId, bool isLike = true)
    {
        await _commentService.UpdateLikeCountAsync(commentId, isLike ? 1 : -1);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = isLike ? "点赞成功" : "取消点赞成功"
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
