using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

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
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetRoleList()
    {
        var data = await _roleService.QueryAsync();
        return Ok(data);
    }
    
}
