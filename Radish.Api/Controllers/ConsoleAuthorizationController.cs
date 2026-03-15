using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// Console 授权控制器
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class ConsoleAuthorizationController : ControllerBase
{
    private readonly IConsoleAuthorizationService _consoleAuthorizationService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ConsoleAuthorizationController(
        IConsoleAuthorizationService consoleAuthorizationService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _consoleAuthorizationService = consoleAuthorizationService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>
    /// 获取资源树
    /// </summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.RolesView, ConsolePermissions.RolesEdit)]
    [ProducesResponseType(typeof(MessageModel<List<ConsoleResourceTreeNodeVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<ConsoleResourceTreeNodeVo>>> GetResourceTree()
    {
        var tree = await _consoleAuthorizationService.GetResourceTreeAsync();
        return MessageModel<List<ConsoleResourceTreeNodeVo>>.Success("获取成功", tree);
    }

    /// <summary>
    /// 获取角色授权快照
    /// </summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.RolesView, ConsolePermissions.RolesEdit)]
    [ProducesResponseType(typeof(MessageModel<RoleAuthorizationSnapshotVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel<RoleAuthorizationSnapshotVo>> GetRoleAuthorization(long roleId)
    {
        var snapshot = await _consoleAuthorizationService.GetRoleAuthorizationAsync(roleId);
        if (snapshot == null)
        {
            return new MessageModel<RoleAuthorizationSnapshotVo>
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "角色不存在",
                ResponseData = default
            };
        }

        return MessageModel<RoleAuthorizationSnapshotVo>.Success("获取成功", snapshot);
    }

    /// <summary>
    /// 获取角色接口权限预览
    /// </summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.RolesView, ConsolePermissions.RolesEdit)]
    [ProducesResponseType(typeof(MessageModel<List<ResourceApiBindingVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<ResourceApiBindingVo>>> GetRolePermissionPreview(long roleId)
    {
        var preview = await _consoleAuthorizationService.GetRolePermissionPreviewAsync(roleId);
        return MessageModel<List<ResourceApiBindingVo>>.Success("获取成功", preview);
    }

    /// <summary>
    /// 保存角色授权
    /// </summary>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.RolesEdit)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> SaveRoleAuthorization([FromBody] SaveRoleAuthorizationDto dto)
    {
        if (dto == null || dto.RoleId <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "角色信息无效"
            };
        }

        var operatorName = string.IsNullOrWhiteSpace(Current.UserName) ? "System" : Current.UserName;
        var saved = await _consoleAuthorizationService.SaveRoleAuthorizationAsync(dto, Current.UserId, operatorName);
        if (!saved)
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
            MessageInfo = "保存成功"
        };
    }
}
