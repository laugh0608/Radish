using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Radish.Api.Resources;
using SqlSugar;

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
    private readonly IAttachmentService _attachmentService;

    private readonly IUserService _userService;
    private readonly IHttpContextUser  _httpContextUser;

    private readonly IPostService _postService;
    private readonly ICommentService _commentService;
    private readonly INotificationPushService _notificationPushService;

    public UserController(
        IUserService userService,
        IHttpContextUser httpContextUser,
        IPostService postService,
        ICommentService commentService,
        IAttachmentService attachmentService,
        INotificationPushService notificationPushService)
    {
        _userService = userService;
        _httpContextUser = httpContextUser;
        _postService = postService;
        _commentService = commentService;
        _notificationPushService = notificationPushService;
        _attachmentService = attachmentService;
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
        var userId = _httpContextUser.UserId;
        var userName = _httpContextUser.UserName;
        var tenantId = _httpContextUser.TenantId;

        // 获取用户头像
        var avatar = await _attachmentService.QueryFirstAsync(a =>
            a.UploaderId == userId &&
            !a.IsDeleted &&
            a.BusinessType == "Avatar" &&
            a.BusinessId == userId);

        var userInfo = new {
            userId,
            userName,
            tenantId,
            avatarUrl = avatar?.Url,
            avatarThumbnailUrl = avatar?.ThumbnailUrl
        };
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

    /// <summary>
    /// 获取当前登录用户的个人资料（个人中心）
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Client")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMyProfile()
    {
        var userId = _httpContextUser.UserId;
        var user = await _userService.QueryFirstAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "用户不存在"
            };
        }

        // 当前方案：头像通过 Attachment 的 Avatar 业务类型关联（BusinessId = userId）实现
        var avatar = await _attachmentService.QueryFirstAsync(a =>
            a.UploaderId == userId &&
            !a.IsDeleted &&
            a.BusinessType == "Avatar" &&
            a.BusinessId == userId);

        var profile = new UserProfileVo
        {
            UserId = user.Uuid,
            UserName = user.VoUsName,
            UserEmail = user.VoUsEmail,
            RealName = user.VoReNa,
            Sex = user.VoSexDo,
            Age = user.VoAgeDo,
            Birth = user.VoBiTh,
            Address = user.VoAdRes,
            CreateTime = user.VoCreateTime,
            AvatarAttachmentId = avatar?.Id,
            AvatarUrl = avatar?.Url,
            AvatarThumbnailUrl = avatar?.ThumbnailUrl
        };

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = profile
        };
    }

    /// <summary>
    /// 更新当前登录用户的个人资料（个人中心）
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Client")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> UpdateMyProfile([FromBody] UpdateMyProfileDto dto)
    {
        var userId = _httpContextUser.UserId;

        var normalizedUserName = string.IsNullOrWhiteSpace(dto.UserName) ? null : dto.UserName.Trim();
        var normalizedUserEmail = string.IsNullOrWhiteSpace(dto.UserEmail) ? null : dto.UserEmail.Trim();
        var normalizedRealName = string.IsNullOrWhiteSpace(dto.RealName) ? null : dto.RealName.Trim();
        var normalizedAddress = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
        var sex = dto.Sex;
        var age = dto.Age;
        var birth = dto.Birth;
        var now = DateTime.Now;

        if (normalizedUserName != null && normalizedUserName.Length > 200)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "用户名长度不能超过 200"
            };
        }

        if (normalizedUserEmail != null)
        {
            if (normalizedUserEmail.Length > 200)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                    MessageInfo = "邮箱长度不能超过 200"
                };
            }

            try
            {
                _ = new System.Net.Mail.MailAddress(normalizedUserEmail);
            }
            catch
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                    MessageInfo = "邮箱格式不正确"
                };
            }
        }

        if (normalizedRealName != null && normalizedRealName.Length > 50)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "真实姓名长度不能超过 50"
            };
        }

        if (normalizedAddress != null && normalizedAddress.Length > 2000)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "地址长度不能超过 2000"
            };
        }

        if (sex.HasValue && (sex.Value < (int)UserSexEnum.Unknown || sex.Value > (int)UserSexEnum.Female))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "性别值不合法"
            };
        }

        if (age.HasValue && age.Value < 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "年龄不能为负数"
            };
        }

        if (normalizedUserName != null)
        {
            var nameExists = await _userService.QueryExistsAsync(u =>
                u.UserName == normalizedUserName &&
                !u.IsDeleted &&
                u.Id != userId);

            if (nameExists)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                    MessageInfo = "用户名已被占用"
                };
            }
        }

        if (normalizedUserEmail != null)
        {
            var emailExists = await _userService.QueryExistsAsync(u =>
                u.UserEmail == normalizedUserEmail &&
                !u.IsDeleted &&
                u.Id != userId);

            if (emailExists)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                    MessageInfo = "邮箱已被占用"
                };
            }
        }

        var affectedRows = await _userService.UpdateColumnsAsync(
            u => new User
            {
                UserName = normalizedUserName ?? u.UserName,
                UserEmail = normalizedUserEmail ?? u.UserEmail,
                UserRealName = normalizedRealName ?? u.UserRealName,
                UserSex = sex ?? u.UserSex,
                UserAge = age ?? u.UserAge,
                UserBirth = birth ?? u.UserBirth,
                UserAddress = normalizedAddress ?? u.UserAddress,
                UpdateTime = now
            },
            u => u.Id == userId && !u.IsDeleted);

        if (affectedRows <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "用户不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "更新成功"
        };
    }

    /// <summary>
    /// 设置当前用户头像（通过绑定 Avatar 附件）
    /// </summary>
    /// <param name="dto">附件 ID（0 表示清空头像）</param>
    [HttpPost]
    [Authorize(Policy = "Client")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> SetMyAvatar([FromBody] SetMyAvatarDto dto)
    {
        var userId = _httpContextUser.UserId;
        var now = DateTime.Now;
        var modifierName = _httpContextUser.UserName;

        // 如果 attachmentId == 0，表示清空头像
        if (dto.AttachmentId == 0)
        {
            await _attachmentService.UpdateColumnsAsync(
                a => new Attachment
                {
                    BusinessId = null,
                    ModifyTime = now,
                    ModifyBy = modifierName,
                    ModifyId = userId
                },
                a => a.UploaderId == userId &&
                     !a.IsDeleted &&
                     a.BusinessType == "Avatar" &&
                     a.BusinessId == userId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "已清空头像"
            };
        }

        var attachment = await _attachmentService.QueryFirstAsync(a => a.Id == dto.AttachmentId && !a.IsDeleted);
        if (attachment == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "附件不存在"
            };
        }

        // 只有上传者或管理员可以绑定为头像
        var roles = _httpContextUser.GetClaimValueByType("role");
        var isAdmin = roles.Contains("Admin") || roles.Contains("System");
        if (attachment.UploaderId != userId && !isAdmin)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Forbidden,
                MessageInfo = "无权设置该附件为头像"
            };
        }

        // 先取消旧头像关联（同一用户只保留最新 Avatar 关联）
        // 保留 BusinessType=Avatar，仅清空 BusinessId，便于在"我的附件"里仍能按 Avatar 过滤查看历史头像
        await _attachmentService.UpdateColumnsAsync(
            a => new Attachment
            {
                BusinessId = null,
                ModifyTime = now,
                ModifyBy = modifierName,
                ModifyId = userId
            },
            a => a.UploaderId == userId &&
                 !a.IsDeleted &&
                 a.BusinessType == "Avatar" &&
                 a.BusinessId == userId &&
                 a.Id != dto.AttachmentId);

        var updated = await _attachmentService.UpdateBusinessAssociationAsync(dto.AttachmentId, "Avatar", userId);
        if (!updated)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = "设置头像失败"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "设置成功"
        };
    }

    /// <summary>
    /// 获取当前用户积分余额（占位，M6 将接入真实积分系统）
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Client")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMyPoints()
    {
        await Task.CompletedTask;
        var userId = _httpContextUser.UserId;

        var vo = new UserPointsVo
        {
            UserId = userId,
            Balance = 0
        };

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = vo
        };
    }

    /// <summary>
    /// 获取当前用户未读消息数量（占位，将来接入真实通知系统）
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Client")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetUnreadMessageCount()
    {
        await Task.CompletedTask;
        var userId = _httpContextUser.UserId;

        var result = new
        {
            userId,
            unreadCount = 0
        };

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = result
        };
    }

    /// <summary>
    /// 【P0 测试接口】手动触发未读数推送（仅用于验证 SignalR 推送机制）
    /// </summary>
    /// <param name="count">要推送的未读数量，默认为随机数</param>
    [HttpPost]
    [Authorize(Policy = "Client")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> TestPushUnreadCount([FromQuery] int? count = null)
    {
        var userId = _httpContextUser.UserId;
        var unreadCount = count ?? new Random().Next(1, 100);

        await _notificationPushService.PushUnreadCountAsync(userId, unreadCount);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = $"已推送未读数 {unreadCount} 到用户 {userId}",
            ResponseData = new { userId, unreadCount }
        };
    }
}

