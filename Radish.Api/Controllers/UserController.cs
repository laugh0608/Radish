using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Shared;
using Radish.Shared.CustomEnum;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Radish.Api.Resources;

namespace Radish.Api.Controllers;

/// <summary>
/// 用户管理控制器
/// </summary>
/// <remarks>
/// 提供用户信息查询、修改等接口。
/// 所有接口需要通过 JWT 认证和 RadishAuthPolicy 授权策略。
/// </remarks>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
//[Authorize(Policy = "RadishAuthPolicy")]
[Tags("用户管理")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IHttpContextUser  _httpContextUser;

    private readonly IPostService _postService;
    private readonly ICommentService _commentService;

    public UserController(
        IUserService userService,
        IHttpContextUser httpContextUser,
        IPostService postService,
        ICommentService commentService)
    {
        _userService = userService;
        _httpContextUser = httpContextUser;
        _postService = postService;
        _commentService = commentService;
    }

    /// <summary>
    /// 获取全部用户列表
    /// </summary>
    /// <returns>包含用户列表的响应对象</returns>
    /// <remarks>
    /// 查询所有用户信息，不包含已删除的用户。
    /// 需要认证和授权。
    /// </remarks>
    /// <response code="200">查询成功，返回用户列表</response>
    /// <response code="401">未授权，Token 无效或过期</response>
    /// <response code="403">禁止访问，权限不足</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status500InternalServerError)]
    public async Task<MessageModel> GetUserList()
    {
        var users = await _userService.QueryAsync();
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = users
        };
    }

    /// <summary>
    /// 根据用户 ID 获取用户信息
    /// </summary>
    /// <param name="id">用户 ID（长整型）</param>
    /// <returns>包含用户信息的响应对象</returns>
    /// <remarks>
    /// <para>根据用户 ID 查询单个用户的详细信息。</para>
    /// <para><strong>重要提示：</strong></para>
    /// <list type="bullet">
    /// <item>使用了路径参数 {id}</item>
    /// <item>在 ApiModule 权限表中配置 URL 时必须使用正则匹配</item>
    /// <item>示例：<c>/api/User/GetUserById/\d+</c></item>
    /// <item>URL 必须以 <c>/</c> 开头</item>
    /// </list>
    /// <para>请求示例：</para>
    /// <code>
    /// GET /api/User/GetUserById/123456789
    /// </code>
    /// </remarks>
    /// <response code="200">查询成功，返回用户信息</response>
    /// <response code="401">未授权，Token 无效或过期</response>
    /// <response code="403">禁止访问，权限不足</response>
    /// <response code="404">用户不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status500InternalServerError)]
    public async Task<MessageModel> GetUserById(long id)
    {
        var localizer = HttpContext.RequestServices.GetRequiredService<IStringLocalizer<Errors>>();

        var userInfo = await _userService.QueryAsync(d => d.Id == id && d.IsDeleted == false);

        if (userInfo == null || userInfo.Count == 0)
        {
            var notFoundMessage = localizer["error.user.not_found"];
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = notFoundMessage,
                Code = "User.NotFound",
                MessageKey = "error.user.not_found",
                ResponseData = null
            };
        }

        var successMessage = localizer["info.user.get_by_id_success"];
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = successMessage,
            Code = "User.GetByIdSuccess",
            MessageKey = "info.user.get_by_id_success",
            ResponseData = userInfo
        };
    }

    /// <summary>
    /// 获取当前登录用户信息
    /// </summary>
    /// <returns>包含当前用户基本信息的响应对象</returns>
    /// <remarks>
    /// <para>从 JWT Token 的 HttpContext 中解析当前登录用户信息。</para>
    /// <para>返回信息包括：</para>
    /// <list type="bullet">
    /// <item>userId: 用户 ID</item>
    /// <item>userName: 用户名</item>
    /// <item>tenantId: 租户 ID</item>
    /// </list>
    /// <para>无需传递任何参数，自动从当前请求上下文中获取。</para>
    /// </remarks>
    /// <response code="200">查询成功，返回当前用户信息</response>
    /// <response code="401">未授权，Token 无效或过期</response>
    /// <response code="403">禁止访问，权限不足</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [Authorize(Policy = "Client")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status500InternalServerError)]
    public async Task<MessageModel> GetUserByHttpContext()
    {
        await Task.CompletedTask;
        var userId = _httpContextUser.UserId;
        var userName = _httpContextUser.UserName;
        var tenantId = _httpContextUser.TenantId;
        // var userInfo = await _userService.QueryAsync(d => d.Id == res);
        var userInfo = new { userId, userName, tenantId };
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            ResponseData = userInfo
        };
    }

    /// <summary>
    /// 获取用户统计信息
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>用户统计信息（发帖数、评论数、获赞数）</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetUserStats(long userId)
    {
        // 统计发帖数
        var postCount = await _postService.QueryCountAsync(
            p => p.AuthorId == userId && p.IsPublished && !p.IsDeleted);

        // 统计评论数
        var commentCount = await _commentService.QueryCountAsync(
            c => c.AuthorId == userId && !c.IsDeleted);

        // 统计帖子获赞数
        var posts = await _postService.QueryAsync(
            p => p.AuthorId == userId && p.IsPublished && !p.IsDeleted);
        var postLikeCount = posts.Sum(p => p.LikeCount);

        // 统计评论获赞数
        var comments = await _commentService.QueryAsync(
            c => c.AuthorId == userId && !c.IsDeleted);
        var commentLikeCount = comments.Sum(c => c.LikeCount);

        var stats = new
        {
            postCount,
            commentCount,
            totalLikeCount = postLikeCount + commentLikeCount,
            postLikeCount,
            commentLikeCount
        };

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = stats
        };
    }

    /// <summary>
    /// 搜索用户（用于@提及功能）
    /// </summary>
    /// <param name="keyword">搜索关键词（匹配用户名）</param>
    /// <param name="limit">返回结果数量限制（默认10，最大50）</param>
    /// <returns>用户列表</returns>
    /// <remarks>
    /// 根据关键词搜索用户名，返回匹配的用户列表供@提及功能使用。
    /// 允许匿名访问。
    /// </remarks>
    /// <response code="200">搜索成功，返回用户列表</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> SearchForMention(string keyword, int limit = 10)
    {
        var users = await _userService.SearchUsersForMentionAsync(keyword, limit);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "搜索成功",
            ResponseData = users
        };
    }
}
