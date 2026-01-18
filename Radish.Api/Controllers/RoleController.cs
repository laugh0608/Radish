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
[Authorize(Policy = "RadishAuthPolicy")]
public class RoleController : ControllerBase
{
    private readonly IBaseService<Role, RoleVo> _roleService;

    public RoleController(IMapper mapper, IBaseService<Role, RoleVo> roleService, IServiceScopeFactory scopeFactory)
    {
        _roleService = roleService;
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
    
}
