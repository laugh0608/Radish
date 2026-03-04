using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>表情包控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("表情包管理")]
public class StickerController : ControllerBase
{
    private readonly IStickerService _stickerService;
    private readonly IHttpContextUser _httpContextUser;

    public StickerController(IStickerService stickerService, IHttpContextUser httpContextUser)
    {
        _stickerService = stickerService;
        _httpContextUser = httpContextUser;
    }

    /// <summary>获取启用的表情包分组（前台）</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetGroups()
    {
        var groups = await _stickerService.GetGroupsAsync(_httpContextUser.TenantId);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = groups
        };
    }

    /// <summary>获取全部表情包分组（管理端，包含禁用分组）</summary>
    [HttpGet]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetAdminGroups()
    {
        var groups = await _stickerService.GetAdminGroupsAsync(_httpContextUser.TenantId);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = groups
        };
    }

    /// <summary>按分组编码获取分组详情（前台）</summary>
    [HttpGet("{code}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetGroupDetail(string code)
    {
        var group = await _stickerService.GetGroupDetailAsync(_httpContextUser.TenantId, code);
        if (group == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "分组不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = group
        };
    }

    /// <summary>记录表情使用次数</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> RecordUse([FromBody] RecordStickerUseDto request)
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

        var recorded = await _stickerService.RecordUseAsync(
            _httpContextUser.TenantId,
            request.EmojiType,
            request.EmojiValue,
            _httpContextUser.UserId,
            _httpContextUser.UserName);

        if (!recorded)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "表情不存在或不可用"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "记录成功",
            ResponseData = true
        };
    }

    /// <summary>创建分组（管理端）</summary>
    [HttpPost]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> CreateGroup([FromBody] CreateStickerGroupDto request)
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
            var id = await _stickerService.CreateGroupAsync(
                _httpContextUser.TenantId,
                request,
                _httpContextUser.UserId,
                _httpContextUser.UserName);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "创建成功",
                ResponseData = id
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
        catch (InvalidOperationException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = 409,
                MessageInfo = ex.Message,
                Code = "CodeAlreadyExists"
            };
        }
    }

    /// <summary>更新分组（管理端）</summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> UpdateGroup(long id, [FromBody] UpdateStickerGroupDto request)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "分组ID无效"
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

        var updated = await _stickerService.UpdateGroupAsync(id, request, _httpContextUser.UserId, _httpContextUser.UserName);
        if (!updated)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "分组不存在"
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

    /// <summary>软删除分组（管理端）</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> DeleteGroup(long id)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "分组ID无效"
            };
        }

        var deleted = await _stickerService.DeleteGroupAsync(id, _httpContextUser.UserId, _httpContextUser.UserName);
        if (!deleted)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "分组不存在"
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

    /// <summary>新增单个表情（管理端）</summary>
    [HttpPost]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> AddSticker([FromBody] CreateStickerDto request)
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
            var id = await _stickerService.AddStickerAsync(request, _httpContextUser.UserId, _httpContextUser.UserName);
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "创建成功",
                ResponseData = id
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
        catch (InvalidOperationException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = 409,
                MessageInfo = ex.Message,
                Code = "CodeAlreadyExists"
            };
        }
    }

    /// <summary>批量新增表情（管理端）</summary>
    [HttpPost]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> BatchAddStickers([FromBody] BatchAddStickersDto request)
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
            var result = await _stickerService.BatchAddStickersAsync(request, _httpContextUser.UserId, _httpContextUser.UserName);

            if (result.VoConflicts.Count > 0)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = 409,
                    Code = "BatchCodeConflict",
                    MessageInfo = "存在重复标识符，请修改后重试",
                    ResponseData = result
                };
            }

            if (result.VoFailedItems.Count > 0)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                    Code = "ImageProcessFailed",
                    MessageInfo = "缩略图生成失败，请稍后重试",
                    ResponseData = result
                };
            }

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "批量保存成功",
                ResponseData = result
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
        catch (InvalidOperationException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = ex.Message,
                Code = "StickerGroupNotFound"
            };
        }
    }

    /// <summary>批量更新表情排序（管理端）</summary>
    [HttpPut]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> BatchUpdateSort([FromBody] BatchUpdateStickerSortDto request)
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

        var updatedCount = await _stickerService.BatchUpdateSortAsync(request.Items, _httpContextUser.UserId, _httpContextUser.UserName);
        var result = new StickerBatchUpdateSortResultVo
        {
            VoUpdatedCount = updatedCount
        };

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "排序已更新",
            ResponseData = result
        };
    }

    /// <summary>更新单个表情（管理端）</summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> UpdateSticker(long id, [FromBody] UpdateStickerDto request)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "表情ID无效"
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

        var updated = await _stickerService.UpdateStickerAsync(id, request, _httpContextUser.UserId, _httpContextUser.UserName);
        if (!updated)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "表情不存在"
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

    /// <summary>软删除单个表情（管理端）</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> DeleteSticker(long id)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "表情ID无效"
            };
        }

        var deleted = await _stickerService.DeleteStickerAsync(id, _httpContextUser.UserId, _httpContextUser.UserName);
        if (!deleted)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "表情不存在"
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

    /// <summary>分组编码唯一性预检（管理端）</summary>
    [HttpGet]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> CheckGroupCode([FromQuery] string code)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToLowerInvariant();
        var available = await _stickerService.CheckGroupCodeAvailableAsync(_httpContextUser.TenantId, normalizedCode);
        var response = new StickerCodeCheckVo
        {
            VoAvailable = available,
            VoCode = normalizedCode
        };

        if (!available)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = 409,
                Code = "CodeAlreadyExists",
                MessageInfo = "分组标识符已存在",
                ResponseData = response
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "ok",
            ResponseData = response
        };
    }

    /// <summary>组内表情编码唯一性预检（管理端）</summary>
    [HttpGet]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> CheckStickerCode([FromQuery] long groupId, [FromQuery] string code)
    {
        if (groupId <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "分组ID无效"
            };
        }

        var groupExists = await _stickerService.CheckGroupExistsAsync(groupId);
        if (!groupExists)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                Code = "StickerGroupNotFound",
                MessageInfo = "分组不存在或已删除"
            };
        }

        var normalizedCode = string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToLowerInvariant();
        var available = await _stickerService.CheckStickerCodeAvailableAsync(groupId, normalizedCode);
        var response = new StickerCodeCheckVo
        {
            VoAvailable = available,
            VoCode = normalizedCode,
            VoGroupId = groupId
        };

        if (!available)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = 409,
                Code = "CodeAlreadyExists",
                MessageInfo = "该分组内表情标识符已存在",
                ResponseData = response
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "ok",
            ResponseData = response
        };
    }

    /// <summary>文件名编码清洗预览（管理端）</summary>
    [HttpGet]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public MessageModel NormalizeCode([FromQuery] string filename)
    {
        var normalized = _stickerService.NormalizeCode(filename);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "ok",
            ResponseData = normalized
        };
    }

    /// <summary>获取分组内表情（管理端）</summary>
    [HttpGet("{groupId:long}")]
    [Authorize(Policy = "SystemOrAdmin")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetGroupStickers(long groupId)
    {
        if (groupId <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "分组ID无效"
            };
        }

        var stickers = await _stickerService.GetGroupStickersAsync(groupId, includeDisabled: true);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = stickers
        };
    }
}
