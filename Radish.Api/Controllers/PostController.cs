using System.Linq.Expressions;
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
/// 论坛帖子控制器
/// </summary>
/// <remarks>
/// 提供帖子的发布、查询、点赞、收藏等接口
/// </remarks>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("论坛帖子管理")]
public class PostController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly IHttpContextUser _httpContextUser;

    public PostController(IPostService postService, IHttpContextUser httpContextUser)
    {
        _postService = postService;
        _httpContextUser = httpContextUser;
    }

    /// <summary>
    /// 根据 ID 获取帖子详情
    /// </summary>
    /// <param name="id">帖子 ID</param>
    /// <returns>帖子详情（包含分类名称和标签）</returns>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetById(long id)
    {
        // 增加浏览次数
        await _postService.IncrementViewCountAsync(id);

        var post = await _postService.GetPostDetailAsync(id);
        if (post == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "帖子不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = post
        };
    }

    /// <summary>
    /// 获取帖子列表（支持分页、排序和搜索）
    /// </summary>
    /// <param name="categoryId">分类 ID（可选）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量（默认 20）</param>
    /// <param name="sortBy">排序方式：newest（最新，默认）、hottest（最热）、essence（精华）</param>
    /// <param name="keyword">搜索关键词（搜索标题和内容）</param>
    /// <returns>分页帖子列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetList(
        long? categoryId = null,
        int pageIndex = 1,
        int pageSize = 20,
        string sortBy = "newest",
        string? keyword = null)
    {
        // 参数校验
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;
        sortBy = sortBy?.ToLowerInvariant() ?? "newest";
        keyword = keyword?.Trim();

        // 构建基础查询条件
        Expression<Func<Post, bool>> baseCondition;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 有搜索关键词：搜索标题或内容包含关键词的帖子
            baseCondition = categoryId.HasValue
                ? p => p.CategoryId == categoryId.Value && p.IsPublished && !p.IsDeleted
                       && (p.Title.Contains(keyword) || p.Content.Contains(keyword))
                : p => p.IsPublished && !p.IsDeleted
                       && (p.Title.Contains(keyword) || p.Content.Contains(keyword));
        }
        else
        {
            // 无搜索关键词：正常查询
            baseCondition = categoryId.HasValue
                ? p => p.CategoryId == categoryId.Value && p.IsPublished && !p.IsDeleted
                : p => p.IsPublished && !p.IsDeleted;
        }

        List<PostVo> data;
        int totalCount;

        // 根据排序方式执行不同的查询逻辑
        switch (sortBy)
        {
            case "hottest":
                // 按热度排序：置顶在前，然后按热度值排序（综合浏览、点赞、评论）
                // 热度计算公式：ViewCount + LikeCount*2 + CommentCount*3
                var allPosts = await _postService.QueryAsync(baseCondition);
                totalCount = allPosts.Count;

                data = allPosts
                    .OrderByDescending(p => p.IsTop)
                    .ThenByDescending(p => p.ViewCount + p.LikeCount * 2 + p.CommentCount * 3)
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
                break;

            case "essence":
                // 按精华排序：置顶在前，然后精华在前，最后按创建时间倒序
                (data, totalCount) = await _postService.QueryPageAsync(
                    baseCondition,
                    pageIndex,
                    pageSize,
                    p => new { p.IsTop, p.IsEssence, p.CreateTime },
                    SqlSugar.OrderByType.Desc);
                break;

            case "newest":
            default:
                // 按最新排序：置顶在前，然后按创建时间倒序
                (data, totalCount) = await _postService.QueryPageAsync(
                    baseCondition,
                    pageIndex,
                    pageSize,
                    p => new { p.IsTop, p.CreateTime },
                    SqlSugar.OrderByType.Desc);
                break;
        }

        // 构建分页模型
        var pageModel = new PageModel<PostVo>
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
    /// 发布帖子
    /// </summary>
    /// <param name="request">帖子信息和标签列表</param>
    /// <returns>创建的帖子 ID</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> Publish([FromBody] PublishPostRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "帖子标题不能为空"
            };
        }

        var post = new Post(new PostInitializationOptions(request.Title, request.Content)
        {
            AuthorId = _httpContextUser.UserId,
            AuthorName = _httpContextUser.UserName,
            CategoryId = request.CategoryId,
            ContentType = request.ContentType ?? "markdown",
            IsPublished = true,
            TenantId = _httpContextUser.TenantId
        });

        var postId = await _postService.PublishPostAsync(post, request.TagNames);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "发布成功",
            ResponseData = postId
        };
    }

    /// <summary>
    /// 点赞/取消点赞帖子
    /// </summary>
    /// <param name="postId">帖子 ID</param>
    /// <param name="isLike">true 为点赞，false 为取消点赞</param>
    /// <returns>操作结果</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Like(long postId, bool isLike = true)
    {
        await _postService.UpdateLikeCountAsync(postId, isLike ? 1 : -1);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = isLike ? "点赞成功" : "取消点赞成功"
        };
    }

    /// <summary>
    /// 获取指定用户的帖子列表
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量（默认 20）</param>
    /// <returns>分页帖子列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetUserPosts(
        long userId,
        int pageIndex = 1,
        int pageSize = 20)
    {
        // 参数校验
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // 查询用户的帖子（只查询已发布且未删除的）
        var (data, totalCount) = await _postService.QueryPageAsync(
            p => p.AuthorId == userId && p.IsPublished && !p.IsDeleted,
            pageIndex,
            pageSize,
            p => p.CreateTime,
            SqlSugar.OrderByType.Desc);

        // 构建分页模型
        var pageModel = new PageModel<PostVo>
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
    /// 编辑帖子
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <returns>操作结果</returns>
    [HttpPut]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Update([FromBody] UpdatePostRequest request)
    {
        // 参数校验
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "帖子标题不能为空"
            };
        }

        // 查询帖子
        var post = await _postService.QueryFirstAsync(p => p.Id == request.PostId && !p.IsDeleted);
        if (post == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "帖子不存在"
            };
        }

        // 权限验证：只有作者本人可以编辑
        if (post.AuthorId != _httpContextUser.UserId)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Forbidden,
                MessageInfo = "无权编辑此帖子"
            };
        }

        // 更新帖子
        await _postService.UpdateColumnsAsync(
            p => new Post
            {
                Title = request.Title,
                Content = request.Content,
                CategoryId = request.CategoryId ?? post.CategoryId,
                ModifyTime = DateTime.Now,
                ModifyBy = _httpContextUser.UserName,
                ModifyId = _httpContextUser.UserId
            },
            p => p.Id == request.PostId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "编辑成功"
        };
    }

    /// <summary>
    /// 删除帖子（软删除）
    /// </summary>
    /// <param name="postId">帖子 ID</param>
    /// <returns>操作结果</returns>
    [HttpDelete]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Delete(long postId)
    {
        // 查询帖子
        var post = await _postService.QueryFirstAsync(p => p.Id == postId && !p.IsDeleted);
        if (post == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "帖子不存在"
            };
        }

        // 权限验证：只有作者本人或管理员可以删除
        var roles = _httpContextUser.GetClaimValueByType("role");
        var isAdmin = roles.Contains("Admin") || roles.Contains("System");
        if (post.AuthorId != _httpContextUser.UserId && !isAdmin)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Forbidden,
                MessageInfo = "无权删除此帖子"
            };
        }

        // 软删除：设置 IsDeleted = true，并记录删除者信息
        await _postService.UpdateColumnsAsync(
            p => new Post
            {
                IsDeleted = true,
                ModifyTime = DateTime.Now,
                ModifyBy = _httpContextUser.UserName,
                ModifyId = _httpContextUser.UserId
            },
            p => p.Id == postId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "删除成功"
        };
    }
}

/// <summary>
/// 编辑帖子请求对象
/// </summary>
public class UpdatePostRequest
{
    /// <summary>帖子 ID</summary>
    public long PostId { get; set; }

    /// <summary>帖子标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>帖子内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>分类 ID（可选，不传则保持原分类）</summary>
    public long? CategoryId { get; set; }
}

/// <summary>
/// 发布帖子请求对象
/// </summary>
public class PublishPostRequest
{
    /// <summary>帖子标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>帖子内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>内容类型（markdown、html、text）</summary>
    public string? ContentType { get; set; }

    /// <summary>分类 ID</summary>
    public long CategoryId { get; set; }

    /// <summary>标签名称列表</summary>
    public List<string>? TagNames { get; set; }
}
