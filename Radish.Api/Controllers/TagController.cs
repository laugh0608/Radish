using Asp.Versioning;
using Radish.Common.HttpContextTool;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Api.Controllers;

/// <summary>
/// 论坛标签控制器
/// </summary>
/// <remarks>
/// 提供标签的查询、热门标签等接口
/// </remarks>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("论坛标签管理")]
public class TagController : ControllerBase
{
    private readonly ITagService _tagService;
    private readonly IHttpContextUser _httpContextUser;

    public TagController(ITagService tagService, IHttpContextUser httpContextUser)
    {
        _tagService = tagService;
        _httpContextUser = httpContextUser;
    }

    /// <summary>
    /// 获取所有标签
    /// </summary>
    /// <returns>标签列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetAll()
    {
        var tags = await _tagService.QueryWithOrderAsync(
            t => t.IsEnabled && !t.IsDeleted,
            t => t.SortOrder,
            SqlSugar.OrderByType.Asc);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = tags
        };
    }

    /// <summary>
    /// 获取固定标签列表（前台）
    /// </summary>
    /// <returns>固定标签列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetFixedTags()
    {
        var tags = await _tagService.GetFixedTagsAsync();

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = tags
        };
    }

    /// <summary>
    /// 获取热门标签
    /// </summary>
    /// <param name="topCount">返回数量，默认 20</param>
    /// <returns>热门标签列表（按帖子数量降序）</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetHotTags(int topCount = 20)
    {
        var allTags = await _tagService.QueryAsync(t => t.IsEnabled && !t.IsDeleted);
        var hotTags = allTags.OrderByDescending(t => t.VoPostCount)
                            .Take(topCount)
                            .ToList();

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = hotTags
        };
    }

    /// <summary>
    /// 根据 ID 获取标签详情
    /// </summary>
    /// <param name="id">标签 ID</param>
    /// <returns>标签详情</returns>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetById(long id)
    {
        var tag = await _tagService.QueryByIdAsync(id);

        if (tag == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "标签不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = tag
        };
    }

    /// <summary>
    /// 获取标签分页（管理端）
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetPage(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        bool? isEnabled = null,
        bool? isFixed = null,
        bool includeDeleted = false)
    {
        var page = await _tagService.GetTagPageAsync(pageIndex, pageSize, keyword, isEnabled, isFixed, includeDeleted);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = page
        };
    }

    /// <summary>
    /// 创建标签（管理端）
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Create([FromBody] CreateTagDto createDto)
    {
        if (!ModelState.IsValid)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "请求参数验证失败"
            };
        }

        try
        {
            var tagId = await _tagService.CreateTagAsync(createDto, _httpContextUser.UserId, _httpContextUser.UserName);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "创建成功",
                ResponseData = tagId
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
    /// 更新标签（管理端）
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Update(long id, [FromBody] CreateTagDto updateDto)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "标签ID无效"
            };
        }

        if (!ModelState.IsValid)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "请求参数验证失败"
            };
        }

        try
        {
            var updated = await _tagService.UpdateTagAsync(id, updateDto, _httpContextUser.UserId, _httpContextUser.UserName);
            if (!updated)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.NotFound,
                    MessageInfo = "标签不存在"
                };
            }

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "更新成功",
                ResponseData = true
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
    /// 启用/禁用标签（管理端）
    /// </summary>
    [HttpPut("{id:long}/status")]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> ToggleStatus(long id, [FromQuery] bool enabled)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "标签ID无效"
            };
        }

        var updated = await _tagService.ToggleTagStatusAsync(id, enabled, _httpContextUser.UserId, _httpContextUser.UserName);
        if (!updated)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "标签不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = enabled ? "启用成功" : "禁用成功",
            ResponseData = true
        };
    }

    /// <summary>
    /// 更新标签排序（管理端）
    /// </summary>
    [HttpPut("{id:long}/sort")]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> UpdateSort(long id, [FromQuery] int sortOrder)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "标签ID无效"
            };
        }

        if (sortOrder < 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "排序值不能为负数"
            };
        }

        var updated = await _tagService.UpdateTagSortOrderAsync(id, sortOrder, _httpContextUser.UserId, _httpContextUser.UserName);
        if (!updated)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "标签不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "排序更新成功",
            ResponseData = true
        };
    }

    /// <summary>
    /// 删除标签（软删除，管理端）
    /// </summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Delete(long id)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "标签ID无效"
            };
        }

        var deleted = await _tagService.SoftDeleteTagAsync(id, _httpContextUser.UserId, _httpContextUser.UserName);
        if (!deleted)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "标签不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "删除成功",
            ResponseData = true
        };
    }

    /// <summary>
    /// 恢复标签（管理端）
    /// </summary>
    [HttpPut("{id:long}/restore")]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Restore(long id)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "标签ID无效"
            };
        }

        var restored = await _tagService.RestoreTagAsync(id, _httpContextUser.UserId, _httpContextUser.UserName);
        if (!restored)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "标签不存在或无需恢复"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "恢复成功",
            ResponseData = true
        };
    }
}
