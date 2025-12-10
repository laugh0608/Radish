using System.Security.Claims;
using Radish.Extension.PermissionExtension;
using Radish.Model.ViewModels;

namespace Radish.Extension;

/// <summary>
/// JWT Token 生成类
/// </summary>
[Obsolete("已迁移到 OIDC/OpenIddict 统一签发，请不要再使用 JwtTokenGenerate。仅保留历史参考，后续版本将删除。")]
public class JwtTokenGenerate
{
    /// <summary>
    /// 获取基于JWT 的 Token（旧实现，已废弃）
    /// </summary>
    /// <param name="claims">需要在登陆的时候配置</param>
    /// <param name="permissionRequirement">在 Program.cs 中 options.TokenValidationParameters 中定义的参数</param>
    /// <returns>TokenInfoVo</returns>
    [Obsolete("已迁移到 OIDC/OpenIddict，请使用 Radish.Auth 提供的 OIDC 登录流程获取 Token。此方法仅为保留历史代码，不应再被调用。")]
    public static TokenInfoVo BuildJwtToken(Claim[] claims, PermissionRequirement permissionRequirement)
    {
        // 已迁移说明：
        // 1. 当前项目已经使用 Radish.Auth + OpenIddict 作为统一的 OIDC 授权服务器；
        // 2. API 侧通过 JwtBearer + Authority 验证由 OpenIddict 签发的 Token；
        // 3. 这里基于对称密钥的手动 JWT 生成逻辑已经与现有验证逻辑脱节，不再使用；
        // 4. 仅保留方法签名和原始实现注释，方便后续对比与代码审计，后续版本会在确认无引用后删除该类。

        // ===== 旧实现开始（仅保留为注释，不再执行） =====
        // // @luobo 20251122 这里需要说明一下，这里是一个正向过程
        // // 也就是使用从数据库中读取的信息和密钥加密生成 Token 的一个过程
        // // 而 在 Program.cs 中 options.TokenValidationParameters 中定义的 AddJwtBearer 方法是一个逆向过程
        // // 也就是在访问的时候使用 Token 来解密信息的从而判断用户是否有权限的一个过程
        //
        // // Token 有效时间，单位 s
        // const double tokenTime = 60 * 60 * 12;
        //
        // var now = DateTime.Now;
        // var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("wpH7A1jQRPuDDTyWv5ZDpCuAtwvMwmjzeKOMgBtvBe3ghDlfO3FhKx6vmZPAIazM"));
        // // 实例化 JwtSecurityToken，这些信息必须要与 Program.cs 中 options.TokenValidationParameters 定义的一致！
        // var jwt = new JwtSecurityToken(
        //     issuer: "Radish", // 发行
        //     audience: "luobo", // 订阅
        //     claims: claims, // 声明
        //     notBefore: now, // 颁发时间
        //     expires: DateTime.Now.AddSeconds(tokenTime), // Token 有效时间，单位 s
        //     signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256) // 加密密钥
        // );
        // // 生成 Token
        // var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
        //
        // // 打包返回前台
        // var responseJson = new TokenInfoVo
        // {
        //     IsSuccess = true,
        //     TokenInfo = encodedJwt,
        //     ExpiresIn = tokenTime,
        //     TokenType = "Bearer"
        // };
        // return responseJson;
        // ===== 旧实现结束 =====

        // 为了避免误用，直接返回失败结果，引导调用方改用 OIDC 登录流程。
        return new TokenInfoVo
        {
            IsSuccess = false,
            TokenInfo = string.Empty,
            ExpiresIn = 0,
            TokenType = "Bearer"
        };
    }
}