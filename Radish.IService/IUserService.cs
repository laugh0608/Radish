using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>用户服务接口类</summary>
public interface IUserService : IBaseService<User, UserVo>
{
    /// <summary>写入用户前确保公开访问标识存在。</summary>
    new Task<long> AddAsync(User entity);

    /// <summary>批量写入用户前确保公开访问标识存在。</summary>
    new Task<int> AddRangeAsync(List<User> entities);

    /// <summary>
    /// 根据邮箱获取可登录用户
    /// </summary>
    /// <param name="email">电子邮箱</param>
    /// <returns>单个用户视图模型，不存在则返回 null</returns>
    Task<UserVo?> GetEnabledUserByEmailAsync(string email);

    /// <summary>
    /// 根据公开主页标识获取可公开访问的用户。
    /// </summary>
    /// <param name="identifier">用户公开标识或旧 LongId 字符串</param>
    /// <returns>用户视图模型，不存在则返回 null</returns>
    Task<UserVo?> GetPublicUserByIdentifierAsync(string identifier);

    /// <summary>
    /// 根据用户 ID 获取角色名称列表
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>角色名称列表</returns>
    Task<List<string>> GetUserRoleNamesAsync(long userId);

    /// <summary>
    /// 获取所有的 角色-API 关系
    /// </summary>
    /// <returns>List RoleModulePermission</returns>
    Task<List<RoleModulePermission>> RoleModuleMaps();

    /// <summary>
    /// 根据角色列表获取当前 Console 权限快照
    /// </summary>
    /// <param name="roleNames">角色名列表</param>
    /// <returns>权限标识列表</returns>
    Task<List<string>> GetPermissionKeysByRolesAsync(IReadOnlyCollection<string> roleNames);

    /// <summary>
    /// 修改当前用户登录密码。
    /// </summary>
    /// <param name="userId">当前用户 ID</param>
    /// <param name="currentPassword">当前密码</param>
    /// <param name="newPassword">新密码</param>
    /// <param name="confirmPassword">确认密码</param>
    Task ChangeMyLoginPasswordAsync(long userId, string currentPassword, string newPassword, string confirmPassword);

    /// <summary>
    /// 修改用户公开展示名，并记录变更审计。
    /// </summary>
    /// <param name="userId">目标用户 ID</param>
    /// <param name="displayName">新的展示名</param>
    /// <param name="context">变更上下文</param>
    /// <returns>展示名是否实际发生变化</returns>
    Task<bool> ChangeDisplayNameAsync(long userId, string displayName, UserDisplayNameChangeContext context);

    /// <summary>
    /// 搜索用户（用于@提及功能）
    /// </summary>
    /// <param name="keyword">搜索关键词（匹配用户名）</param>
    /// <param name="tenantId">当前租户 Id</param>
    /// <param name="limit">返回结果数量限制（默认10）</param>
    /// <returns>用户提及视图模型列表</returns>
    Task<List<UserMentionVo>> SearchUsersForMentionAsync(string keyword, long tenantId, int limit = 10);
}
