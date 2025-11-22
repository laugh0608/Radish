using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;

namespace Radish.Api.Controllers;

/// <summary>用户接口控制器</summary>
[ApiController]
[Route("api/[controller]/[action]")]
[Authorize(Policy = "RadishAuthPolicy")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// 获取全部用户
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<MessageModel> GetUserList()
    {
        var users = await _userService.QueryAsync();
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = 200,
            MessageInfo = "获取成功",
            ResponseData = users
        };
    }
    
    /// <summary>
    /// 根据 Uuid 获取用户信息
    /// </summary>
    /// <param name="id">用户 Id</param>
    /// <returns></returns>
    [HttpGet("{id:long}")]
    public async Task<MessageModel> GetUserById(long id)
    {
        // TODO: 这里瞎写的，后面要修改的
        var userInfo = await _userService.QueryAsync(d => d.Id == id && d.IsDeleted == false);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = 200,
            MessageInfo = "获取成功",
            ResponseData = userInfo
        };
    }
}
