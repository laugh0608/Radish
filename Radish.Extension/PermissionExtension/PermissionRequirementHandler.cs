using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Radish.Common;
using Radish.IService;
using Radish.Model;

namespace Radish.Extension.PermissionExtension;

public class PermissionRequirementHandler : AuthorizationHandler<PermissionRequirement>, IAuthorizationRequirement
{
    private readonly IHttpContextAccessor _accessor;
    private readonly IUserService _userService;
    public IAuthenticationSchemeProvider Schemes { get; set; }

    public PermissionRequirementHandler(IAuthenticationSchemeProvider schemes,
        IHttpContextAccessor accessor,
        IUserService userService)
    {
        Schemes = schemes;
        _accessor = accessor;
        _userService = userService;
    }

    // public List<PermissionItem> Permissions { get; set; }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var httpContext = _accessor.HttpContext;

        // 获取系统中所有的角色和菜单的关系集合
        if (!requirement.PermissionItems.Any())
        {
            var data = await _userService.RoleModuleMaps();
            List<PermissionItem> list = (from item in data
                where item.IsDeleted == false
                orderby item.Id
                select new PermissionItem
                {
                    Url = item.ApiModule?.LinkUrl,
                    Role = item.Role?.RoleName.ObjToString(),
                }).ToList();

            requirement.PermissionItems = list;
        }

        if (httpContext != null)
        {
            var questUrl = httpContext.Request.Path.Value.ToLower();

            // 整体结构类似认证中间件 UseAuthentication 的逻辑，具体查看开源地址
            // https://github.com/dotnet/aspnetcore/blob/master/src/Security/Authentication/Core/src/AuthenticationMiddleware.cs
            httpContext.Features.Set<IAuthenticationFeature>(new AuthenticationFeature
            {
                OriginalPath = httpContext.Request.Path,
                OriginalPathBase = httpContext.Request.PathBase
            });


            //判断请求是否拥有凭据，即有没有登录
            var defaultAuthenticate = await Schemes.GetDefaultAuthenticateSchemeAsync();
            if (defaultAuthenticate != null)
            {
                var result = await httpContext.AuthenticateAsync(defaultAuthenticate.Name);

                // 是否开启测试环境
                var isTestCurrent = AppSettingsTool.RadishApp(new string[] { "AppSettings", "UseLoadTest" }).ObjToBool();

                // result?.Principal 不为空即登录成功
                if (result?.Principal != null || isTestCurrent)
                {
                    if (!isTestCurrent) httpContext.User = result.Principal;

                    // 应该要先校验用户的信息，再校验菜单权限相关的
                    User user = new();
                    // 判断 token 是否过期，过期则重新登录
                    var isExp = false;
                    // jwt
                    isExp = (httpContext.User.Claims.FirstOrDefault(s => s.Type == ClaimTypes.Expiration)
                                ?.Value) != null &&
                            DateTime.Parse(httpContext.User.Claims
                                .FirstOrDefault(s => s.Type == ClaimTypes.Expiration)?.Value) >= DateTime.Now;

                    if (!isExp)
                    {
                        context.Fail(new AuthorizationFailureReason(this, "授权已过期，请重新授权"));
                        return;
                    }

                    // 获取当前用户的角色信息
                    List<string> currentUserRoles = (from item in httpContext.User.Claims
                        where item.Type == ClaimTypes.Role
                        select item.Value).ToList();
                    if (!currentUserRoles.Any())
                    {
                        currentUserRoles = (from item in httpContext.User.Claims
                            where item.Type == "role"
                            select item.Value).ToList();
                    }

                    // 超级管理员 默认拥有所有权限
                    if (currentUserRoles.All(s => s != "System"))
                    {
                        var isMatchRole = false;
                        var permisssionRoles =
                            requirement.PermissionItems.Where(w => currentUserRoles.Contains(w.Role));
                        foreach (var item in permisssionRoles)
                        {
                            try
                            {
                                // 需要注意的是，数据库中的 URL 需要加上根符号，比如 /api/test。而 api/test 这里是不通过的
                                // 忽略 URL 的大小写，如果有路径参数，数据库中的 URL 要做特殊处理，例如 /api/User/GetUserById/\d+
                                if (Regex.Match(questUrl, item.Url?.ObjToString().ToLower())?.Value == questUrl)
                                {
                                    isMatchRole = true;
                                    break;
                                }
                            }
                            catch (Exception)
                            {
                                // ignored
                            }
                        }

                        // 验证权限
                        if (currentUserRoles.Count <= 0 || !isMatchRole)
                        {
                            context.Fail();
                            return;
                        }
                    }

                    context.Succeed(requirement);
                    return;
                }
            }

            if ((!httpContext.Request.Method.Equals("POST") || !httpContext.Request.HasFormContentType))
            {
                context.Fail();
                return;
            }
        }
    }
}