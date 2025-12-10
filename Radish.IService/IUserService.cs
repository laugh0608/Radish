using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>用户服务接口类</summary>
public interface IUserService : IBaseService<User, UserVo>
{
    /// <summary>
    /// 通过登录用户名和登录密码查询用户的角色名称
    /// </summary>
    /// <param name="loginName">登录用户名</param>
    /// <param name="loginPwd">登陆密码</param>
    /// <returns>string RoleName, 可能为多个</returns>
    Task<string> GetUserRoleNameStrAsync(string loginName, string loginPwd);

    /// <summary>
    /// 获取所有的 角色-API 关系
    /// </summary>
    /// <returns>List RoleModulePermission</returns>
    Task<List<RoleModulePermission>> RoleModuleMaps();
    
    /// <summary>测试使用同事务</summary>
    /// <remarks>仅为示例，无任何作用</remarks>
    /// <returns></returns>
    Task<bool> TestTranPropagationUser();
}