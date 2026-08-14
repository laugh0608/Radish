using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>角色接口控制器</summary>
[ApiController]
[ApiErrorContract]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class RoleController : ControllerBase
{
    private readonly IRoleGovernanceService _roleGovernanceService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public RoleController(
        IRoleGovernanceService roleGovernanceService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _roleGovernanceService = roleGovernanceService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>获取全部角色，测试泛型基类和视图对象关系映射</summary>
    /// <returns>角色列表</returns>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.RolesView)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetRoleList()
    {
        var data = await _roleGovernanceService.GetRolesAsync();
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = data
        };
    }

    /// <summary>根据ID获取角色详情</summary>
    /// <param name="id">角色ID</param>
    /// <returns>角色详情</returns>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.RolesView, ConsolePermissions.RolesEdit)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetRoleById(long id)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "角色ID无效"
            };
        }

        var role = await _roleGovernanceService.GetRoleAsync(id);
        if (role == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "角色不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = role
        };
    }

    /// <summary>创建角色</summary>
    /// <param name="request">角色信息</param>
    /// <returns>创建结果</returns>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.RolesCreate)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> CreateRole([FromBody] RoleMutationDto request)
    {
        if (request == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "角色信息不能为空"
            };
        }

        var result = await _roleGovernanceService.CreateRoleAsync(
            request,
            Current.UserId,
            Current.UserName);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "创建成功",
            ResponseData = result
        };
    }

    /// <summary>更新角色</summary>
    /// <param name="id">角色ID</param>
    /// <param name="request">角色信息</param>
    /// <returns>更新结果</returns>
    [HttpPut]
    [RequireConsolePermission(ConsolePermissions.RolesEdit)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> UpdateRole(long id, [FromBody] RoleMutationDto request)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "角色ID无效"
            };
        }

        if (request == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "角色信息不能为空"
            };
        }

        var result = await _roleGovernanceService.UpdateRoleAsync(
            id,
            request,
            Current.UserId,
            Current.UserName);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "更新成功",
            ResponseData = result
        };
    }

    /// <summary>删除角色（软删除）</summary>
    /// <param name="id">角色ID</param>
    /// <returns>删除结果</returns>
    [HttpDelete]
    [RequireConsolePermission(ConsolePermissions.RolesDelete)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> DeleteRole(long id)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "角色ID无效"
            };
        }

        await _roleGovernanceService.DeleteRoleAsync(
            id,
            Current.UserId,
            Current.UserName);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "删除成功"
        };
    }

    /// <summary>启用/禁用角色</summary>
    /// <param name="id">角色ID</param>
    /// <param name="enabled">是否启用</param>
    /// <returns>操作结果</returns>
    [HttpPut]
    [RequireConsolePermission(ConsolePermissions.RolesToggle)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> ToggleRoleStatus(long id, [FromQuery] bool enabled)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "角色ID无效"
            };
        }

        var result = await _roleGovernanceService.ToggleRoleAsync(
            id,
            enabled,
            Current.UserId,
            Current.UserName);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = enabled ? "启用成功" : "禁用成功",
            ResponseData = result
        };
    }
}
