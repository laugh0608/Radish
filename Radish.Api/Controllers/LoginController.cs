using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Radish.Common;
using Radish.Common.HelpTool;
using Radish.Extension;
using Radish.Extension.PermissionExtension;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;

namespace Radish.Api.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class LoginController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<LoginController> _logger;
    private readonly PermissionRequirement _requirement;

    public LoginController(IUserService userService, ILogger<LoginController> logger, PermissionRequirement requirement)
    {
        _userService = userService;
        _logger = logger;
        _requirement = requirement;
    }

    /// <summary>
    /// 使用用户名和密码获取登录 JWT Token
    /// </summary>
    /// <param name="name">明文用户名</param>
    /// <param name="pass">明文密码</param>
    /// <returns>TokenInfoViewModel</returns>
    [HttpGet]
    public async Task<MessageModel<TokenInfoVo>> GetJwtToken(string name = "", string pass = "")
    {
        // string jwtStr = string.Empty;

        // 用 32 位 MD5 加密密码
        pass = Md5Helper.Md5Encrypt32(pass);

        Log.Information($"测试自定义日志 -- {name}-{pass}");
        _logger.LogInformation($"测试自定义日志 -- {name}-{pass}");

        var user = await _userService.QueryAsync(d =>
            d.LoginName == name && d.LoginPassword == pass && d.IsDeleted == false);
        if (user.Count > 0)
        {
            var userRoles = await _userService.GetUserRoleNameStrAsync(name, pass);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, name),
                new Claim(JwtRegisteredClaimNames.Jti, user.FirstOrDefault().Uuid.ToString()),
                new Claim("TenantId", user.FirstOrDefault().VoTenId.ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTime.Now.DateToTimeStamp()),
                new Claim(ClaimTypes.Expiration,
                    DateTime.Now.AddSeconds(60 * 60 * 12).ToString()) // Token 有效期，单位 s
            };
            // 如果是基于用户的授权策略，这里要添加用户，如果是基于角色的授权策略，这里要添加角色
            claims.AddRange(userRoles.Split(',').Select(s => new Claim(ClaimTypes.Role, s)));
            
            var token = JwtTokenGenerate.BuildJwtToken(claims.ToArray(), _requirement);
            
            return MessageModel<TokenInfoVo>.Success("获取成功", token);
        }

        return MessageModel<TokenInfoVo>.Failed("认证失败");
    }
}