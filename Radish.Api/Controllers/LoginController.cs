using Microsoft.AspNetCore.Mvc;
using Radish.IService;

namespace Radish.Api.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class LoginController : ControllerBase
{
    private readonly IUserService _userService;

    public LoginController(IUserService userService)
    {
        _userService = userService;
    }
}