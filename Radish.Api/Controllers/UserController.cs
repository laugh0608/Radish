using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;

namespace Radish.Api.Controllers;

/// <summary>用户接口控制器</summary>
[ApiController]
[Route("api/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = "RadishAuthPolicy")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IHttpContextUser  _httpContextUser;

    public UserController(IUserService userService, IHttpContextUser httpContextUser)
    {
        _userService = userService;
        _httpContextUser = httpContextUser;
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
    /// <remarks>
    /// <para>如果使用了路径参数，那么在 ApiModule 表中存 URL 的时候必须加上正则匹配：</para>
    /// <para>例如：/api/GetById/\d+</para>
    /// <para>api 前面的根符号别忘了</para>
    /// </remarks>
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

    /// <summary>
    /// 根据 HTTP 上下文来获取用户信息
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<MessageModel> GetUserByHttpContext()
    {
        await Task.CompletedTask;
        var userId = _httpContextUser.UserId;
        var userName = _httpContextUser.UserName;
        var tenantId = _httpContextUser.TenantId;
        // var userInfo = await _userService.QueryAsync(d => d.Id == res);
        var userInfo = new { userId, userName, tenantId };
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = 200,
            ResponseData = userInfo
        };
    }
}
