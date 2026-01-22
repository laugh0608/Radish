using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>角色接口控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = "SystemOrAdmin")]
public class RoleController : ControllerBase
{
    private readonly IBaseService<Role, RoleVo> _roleService;
    private readonly IMapper _mapper;

    public RoleController(IMapper mapper, IBaseService<Role, RoleVo> roleService, IServiceScopeFactory scopeFactory)
    {
        _roleService = roleService;
        _mapper = mapper;
    }

    /// <summary>获取全部角色，测试泛型基类和视图对象关系映射</summary>
    /// <returns>角色列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetRoleList()
    {
        var data = await _roleService.QueryAsync();
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

        var role = await _roleService.QueryByIdAsync(id);
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
    /// <param name="roleVo">角色信息</param>
    /// <returns>创建结果</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> CreateRole([FromBody] RoleVo roleVo)
    {
        if (roleVo == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "角色信息不能为空"
            };
        }

        if (string.IsNullOrWhiteSpace(roleVo.VoRoleName))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "角色名称不能为空"
            };
        }

        try
        {
            var role = _mapper.Map<Role>(roleVo);
            role.CreateTime = DateTime.Now;
            role.IsDeleted = false; // 新创建的角色默认不删除

            var result = await _roleService.AddAsync(role);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "创建成功",
                ResponseData = result
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"创建失败：{ex.Message}"
            };
        }
    }

    /// <summary>更新角色</summary>
    /// <param name="id">角色ID</param>
    /// <param name="roleVo">角色信息</param>
    /// <returns>更新结果</returns>
    [HttpPut]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> UpdateRole(long id, [FromBody] RoleVo roleVo)
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

        if (roleVo == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "角色信息不能为空"
            };
        }

        if (string.IsNullOrWhiteSpace(roleVo.VoRoleName))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "角色名称不能为空"
            };
        }

        try
        {
            var existingRole = await _roleService.QueryByIdAsync(id);
            if (existingRole == null)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.NotFound,
                    MessageInfo = "角色不存在"
                };
            }

            var role = _mapper.Map<Role>(roleVo);
            role.Id = id;
            role.ModifyTime = DateTime.Now;

            var result = await _roleService.UpdateAsync(role);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "更新成功",
                ResponseData = result
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"更新失败：{ex.Message}"
            };
        }
    }

    /// <summary>删除角色（软删除）</summary>
    /// <param name="id">角色ID</param>
    /// <returns>删除结果</returns>
    [HttpDelete]
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

        try
        {
            var existingRole = await _roleService.QueryByIdAsync(id);
            if (existingRole == null)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.NotFound,
                    MessageInfo = "角色不存在"
                };
            }

            var result = await _roleService.DeleteByIdAsync(id);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "删除成功",
                ResponseData = result
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"删除失败：{ex.Message}"
            };
        }
    }

    /// <summary>启用/禁用角色</summary>
    /// <param name="id">角色ID</param>
    /// <param name="enabled">是否启用</param>
    /// <returns>操作结果</returns>
    [HttpPut]
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

        try
        {
            var existingRole = await _roleService.QueryByIdAsync(id);
            if (existingRole == null)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.NotFound,
                    MessageInfo = "角色不存在"
                };
            }

            var role = _mapper.Map<Role>(existingRole);
            role.IsEnabled = enabled;
            role.ModifyTime = DateTime.Now;

            var result = await _roleService.UpdateAsync(role);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = enabled ? "启用成功" : "禁用成功",
                ResponseData = result
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"操作失败：{ex.Message}"
            };
        }
    }
}
