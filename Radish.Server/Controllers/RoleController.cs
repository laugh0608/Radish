using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;

namespace Radish.Server.Controllers;

/// <summary>角色接口控制器</summary>
[ApiController]
[Route("api/[controller]/[action]")]
public class RoleController : ControllerBase
{
    private readonly IBaseServices<RoleVo>  _roleService;

    public RoleController(IBaseServices<RoleVo> roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoleList()
    {
        var data = await _roleService.QueryAsync();
        return Ok(data);
    }
    
}