using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Server.Controllers;

/// <summary>角色接口控制器</summary>
[ApiController]
[Route("api/[controller]/[action]")]
public class RoleController : ControllerBase
{
    private readonly IMapper _mapper;
    private readonly IBaseServices<Role, RoleVo> _roleService;

    public RoleController(IMapper mapper, IBaseServices<Role, RoleVo> roleService)
    {
        _mapper = mapper;
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