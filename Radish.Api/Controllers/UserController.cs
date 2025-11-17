using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.IService.User;

namespace Radish.Api.Controllers;

/// <summary>用户接口控制器</summary>
[ApiController]
[Route("api/[controller]/[action]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>获取全部用户，测试仓储和服务分层</summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult> GetUserList()
    {
        var users = await _userService.GetUsersAsync();
        return Ok(users);
    }
}
