using Microsoft.AspNetCore.Mvc;

namespace Radish.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class UserController : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult> GetUserList() 
        {
            var _userService = new Service.UserService();
            var users = _userService.GetUsersAsync().Result;
            return Ok(users);
        }
    }
}
