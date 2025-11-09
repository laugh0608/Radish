using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.IService.User;

namespace Radish.Server.Controllers;

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

    [HttpGet]
    public async Task<ActionResult> GetUserList()
    {
        // 示例：经由 Service 调用仓储，返回演示用的用户清单。
        var users = await _userService.GetUsersAsync();
        return Ok(users);
    }
}
