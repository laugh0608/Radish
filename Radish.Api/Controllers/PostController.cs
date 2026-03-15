using System.Linq.Expressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
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
    private readonly IContentModerationService _contentModerationService;
    private readonly IBaseService<Attachment, AttachmentVo> _attachmentService;
    private readonly IBaseService<Comment, CommentVo> _commentService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public PostController(
        IPostService postService,
        IContentModerationService contentModerationService,
        IBaseService<Attachment, AttachmentVo> attachmentService,
        IBaseService<Comment, CommentVo> commentService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _postService = postService;
        _contentModerationService = contentModerationService;
        _attachmentService = attachmentService;
        _commentService = commentService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

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

        var viewerUserId = Current.UserId > 0 ? (long?)Current.UserId : null;
        var post = await _postService.GetPostDetailAsync(id, viewerUserId);
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
    /// <param name="sortBy">排序方式：全部帖子支持 newest（最新，默认）、hottest（最热）、essence（精华）；问答视图支持 newest、pending（待解决优先）、answers（回答数）</param>
    /// <param name="keyword">搜索关键词（搜索标题和内容）</param>
    /// <param name="startTime">筛选起始时间（可选，基于帖子创建时间）</param>
    /// <param name="endTime">筛选结束时间（可选，基于帖子创建时间）</param>
    /// <param name="postType">帖子视图：all（默认）/ question（问答）</param>
    /// <param name="questionStatus">问答状态：all（默认）/ pending / solved</param>
    /// <returns>分页帖子列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetList(
        long? categoryId = null,
        int pageIndex = 1,
        int pageSize = 20,
        string sortBy = "newest",
        string? keyword = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string postType = "all",
        string questionStatus = "all")
    {
        // 参数校验
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;
        sortBy = sortBy?.ToLowerInvariant() ?? "newest";
        keyword = keyword?.Trim();
        if (startTime.HasValue && endTime.HasValue && startTime > endTime)
        {
            (startTime, endTime) = (endTime, startTime);
        }

        postType = postType?.Trim().ToLowerInvariant() ?? "all";
        questionStatus = questionStatus?.Trim().ToLowerInvariant() ?? "all";
        var isQuestionView = postType == "question";
        bool? isSolvedFilter = questionStatus switch
        {
            "pending" => false,
            "solved" => true,
            _ => null
        };

        List<PostVo> data;
        int totalCount;

        if (isQuestionView)
        {
            var normalizedQuestionSort = sortBy switch
            {
                "pending" => "pending",
                "answers" => "answers",
                _ => "newest"
            };

            (data, totalCount) = await _postService.GetQuestionPostPageAsync(
                categoryId,
                pageIndex,
                pageSize,
                normalizedQuestionSort,
                keyword,
                startTime,
                endTime,
                isSolvedFilter);
        }
        else
        {
            // 构建基础查询条件
            var normalizedKeyword = keyword ?? string.Empty;
            var hasKeyword = !string.IsNullOrWhiteSpace(normalizedKeyword);
            var hasCategory = categoryId.HasValue;
            var categoryValue = categoryId ?? 0;
            var hasStartTime = startTime.HasValue;
            var hasEndTime = endTime.HasValue;
            var startTimeValue = startTime ?? DateTime.MinValue;
            var endTimeValue = endTime ?? DateTime.MaxValue;

            Expression<Func<Post, bool>> baseCondition = post =>
                post.IsPublished &&
                !post.IsDeleted &&
                (!hasCategory || post.CategoryId == categoryValue) &&
                (!hasKeyword || post.Title.Contains(normalizedKeyword) || post.Content.Contains(normalizedKeyword)) &&
                (!hasStartTime || post.CreateTime >= startTimeValue) &&
                (!hasEndTime || post.CreateTime <= endTimeValue);

            // 根据排序方式执行不同的查询逻辑
            switch (sortBy)
            {
                case "hottest":
                    // 按热度排序：置顶在前，然后按热度值排序（综合浏览、点赞、评论）
                    // 热度计算公式：ViewCount + LikeCount*2 + CommentCount*3
                    var allPosts = await _postService.QueryAsync(baseCondition);
                    totalCount = allPosts.Count;

                    data = allPosts
                        .OrderByDescending(p => p.VoIsTop)
                        .ThenByDescending(p => p.VoViewCount + p.VoLikeCount * 2 + p.VoCommentCount * 3)
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
        }

        // 构建分页模型
        if (data.Any())
        {
            await _postService.FillPostListMetadataAsync(data);
            await FillPostAvatarAndInteractorsAsync(data);
        }

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

    private async Task FillPostAvatarAndInteractorsAsync(List<PostVo> posts)
    {
        if (posts.Count == 0)
        {
            return;
        }

        var postIds = posts
            .Select(post => post.VoId)
            .Distinct()
            .ToList();

        var comments = await _commentService.QueryAsync(comment =>
            postIds.Contains(comment.PostId) &&
            comment.IsEnabled &&
            !comment.IsDeleted);

        var commentsByPost = comments
            .Where(comment => comment.VoAuthorId > 0)
            .GroupBy(comment => comment.VoPostId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(comment => comment.VoCreateTime)
                    .ToList());

        var userIds = posts
            .Select(post => post.VoAuthorId)
            .Concat(comments.Select(comment => comment.VoAuthorId))
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();

        var avatarUrlMap = new Dictionary<long, string>();
        if (userIds.Count > 0)
        {
            var avatarAttachments = await _attachmentService.QueryAsync(attachment =>
                attachment.BusinessType == "Avatar" &&
                attachment.BusinessId.HasValue &&
                userIds.Contains(attachment.BusinessId.Value) &&
                attachment.IsEnabled &&
                !attachment.IsDeleted);

            avatarUrlMap = avatarAttachments
                .Where(attachment => attachment.VoBusinessId.HasValue && !string.IsNullOrWhiteSpace(attachment.VoUrl))
                .OrderByDescending(attachment => attachment.VoCreateTime)
                .GroupBy(attachment => attachment.VoBusinessId!.Value)
                .ToDictionary(group => group.Key, group => group.First().VoUrl);
        }

        foreach (var post in posts)
        {
            if (avatarUrlMap.TryGetValue(post.VoAuthorId, out var authorAvatarUrl))
            {
                post.VoAuthorAvatarUrl = authorAvatarUrl;
            }

            if (!commentsByPost.TryGetValue(post.VoId, out var postComments))
            {
                post.VoLatestInteractors = new List<PostInteractorVo>();
                continue;
            }

            post.VoLatestInteractors = postComments
                .Where(comment => comment.VoAuthorId > 0 && comment.VoAuthorId != post.VoAuthorId)
                .GroupBy(comment => comment.VoAuthorId)
                .Select(group => group.OrderByDescending(comment => comment.VoCreateTime).First())
                .OrderByDescending(comment => comment.VoCreateTime)
                .Take(3)
                .Select(comment => new PostInteractorVo
                {
                    VoUserId = comment.VoAuthorId,
                    VoUserName = comment.VoAuthorName,
                    VoAvatarUrl = avatarUrlMap.GetValueOrDefault(comment.VoAuthorId)
                })
                .ToList();
        }
    }

    /// <summary>
    /// 发布帖子
    /// </summary>
    /// <param name="request">帖子信息和标签列表</param>
    /// <returns>创建的帖子 ID</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> Publish([FromBody] PublishPostDto request)
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

        var normalizedTagNames = request.TagNames?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (normalizedTagNames.Count is < 1 or > 5)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "发布帖子时标签数量必须在 1 到 5 个之间"
            };
        }

        var publishPermission = await _contentModerationService.GetPublishPermissionAsync(Current.UserId);
        if (!publishPermission.VoCanPublish)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Forbidden,
                MessageInfo = publishPermission.VoDenyReason ?? "当前状态无法发布内容"
            };
        }

        var allowCreateTag = Current.IsSystemOrAdmin();

        var post = new Post(new PostInitializationOptions(request.Title, request.Content)
        {
            AuthorId = Current.UserId,
            AuthorName = Current.UserName,
            CategoryId = request.CategoryId,
            ContentType = request.ContentType ?? "markdown",
            IsPublished = true,
            TenantId = Current.TenantId
        });

        try
        {
            var postId = await _postService.PublishPostAsync(post, request.Poll, request.IsQuestion, normalizedTagNames, allowCreateTag);
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "发布成功",
                ResponseData = postId
            };
        }
        catch (InvalidOperationException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Forbidden,
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

    /// <summary>
    /// 点赞/取消点赞帖子
    /// </summary>
    /// <param name="postId">帖子 ID</param>
    /// <param name="isLike">true 为点赞，false 为取消点赞（已废弃，使用toggle模式）</param>
    /// <returns>操作结果</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Like(long postId, bool isLike = true)
    {
        var userId = Current.UserId;
        var result = await _postService.ToggleLikeAsync(userId, postId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = result.IsLiked ? "点赞成功" : "取消点赞成功",
            ResponseData = new VoLikeResult { VoIsLiked = result.IsLiked, VoLikeCount = result.LikeCount }
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
    public async Task<MessageModel> Update([FromBody] UpdatePostDto request)
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

        var normalizedTagNames = request.TagNames?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (normalizedTagNames.Count is < 1 or > 5)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "编辑帖子时标签数量必须在 1 到 5 个之间"
            };
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "帖子内容不能为空"
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

        var isAdmin = Current.IsSystemOrAdmin();

        // 权限验证：作者本人或管理员可编辑
        if (post.VoAuthorId != Current.UserId && !isAdmin)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Forbidden,
                MessageInfo = "无权编辑此帖子"
            };
        }

        var allowCreateTag = Current.IsSystemOrAdmin();

        try
        {
            await _postService.UpdatePostAsync(
                postId: request.PostId,
                title: request.Title,
                content: request.Content,
                categoryId: request.CategoryId,
                tagNames: normalizedTagNames,
                allowCreateTag: allowCreateTag,
                operatorId: Current.UserId,
                operatorName: Current.UserName,
                isAdmin: isAdmin);
        }
        catch (InvalidOperationException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Forbidden,
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

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "编辑成功"
        };
    }

    /// <summary>
    /// 获取帖子编辑历史（分页）
    /// </summary>
    /// <param name="postId">帖子 Id</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小（默认 20）</param>
    /// <returns>编辑历史</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetEditHistory(long postId, int pageIndex = 1, int pageSize = 20)
    {
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

        var (histories, total) = await _postService.GetPostEditHistoryPageAsync(postId, pageIndex, pageSize);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = new VoPagedResult<PostEditHistoryVo>
            {
                VoItems = histories,
                VoTotal = total,
                VoPageIndex = pageIndex < 1 ? 1 : pageIndex,
                VoPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100)
            }
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
        var isAdmin = Current.IsSystemOrAdmin();
        if (post.VoAuthorId != Current.UserId && !isAdmin)
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
                ModifyBy = Current.UserName,
                ModifyId = Current.UserId
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
