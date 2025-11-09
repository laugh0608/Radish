using Microsoft.AspNetCore.Mvc;
using Radish.IService;

namespace Radish.Server.Controllers;

/// <summary>
/// 用户示例接口控制器，用于演示 Service/Repository 分层协作与测试方式。
/// </summary>
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
