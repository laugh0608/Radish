using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Radish.Model.ViewModels;

namespace Radish.Extension;

/// <summary>
/// JWT Token 生成类
/// </summary>
public class JwtTokenGenerate
{
    /// <summary>
    /// 获取基于JWT 的 Token
    /// </summary>
    /// <param name="claims">需要在登陆的时候配置</param>
    /// <param name="permissionRequirement">在 Program.cs 中 options.TokenValidationParameters 中定义的参数</param>
    /// <returns>TokenInfoVo</returns>
    public static TokenInfoVo BuildJwtToken(Claim[] claims, PermissionRequirement permissionRequirement)
    {
        // @luobo 20251122 这里需要说明一下，这里是一个正向过程
        // 也就是使用从数据库中读取的信息和密钥加密生成 Token 的一个过程
        // 而 在 Program.cs 中 options.TokenValidationParameters 中定义的 AddJwtBearer 方法是一个逆向过程
        // 也就是在访问的时候使用 Token 来解密信息的从而判断用户是否有权限的一个过程
        
        // Token 有效时间，单位 s
        const double tokenTime = 60 * 60 * 12;
        
        var now = DateTime.Now;
        // TODO: 这个密钥后期要改
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("sdfsdfsrty45634kkhllghtdgdfss345t678fs"));
        // 实例化 JwtSecurityToken，这些信息必须要与 Program.cs 中 options.TokenValidationParameters 定义的一致！
        var jwt = new JwtSecurityToken(
            issuer: "Radish", // 发行
            audience: "luobo", // 订阅
            claims: claims, // 声明
            notBefore: now, // 颁发时间
            expires: DateTime.Now.AddSeconds(tokenTime), // Token 有效时间，单位 s
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256) // 加密密钥
        );
        // 生成 Token
        var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

        // 打包返回前台
        var responseJson = new TokenInfoVo
        {
            IsSuccess = true,
            TokenInfo = encodedJwt,
            ExpiresIn = tokenTime,
            TokenType = "Bearer"
        };
        return responseJson;
    }
}