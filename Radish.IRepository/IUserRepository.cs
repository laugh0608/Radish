using Radish.Model;

namespace Radish.IRepository;

public sealed record ConsoleUserPageQuery(
    int PageIndex,
    int PageSize,
    string Keyword,
    bool? IsEnabled,
    string RoleName);

public sealed record ConsoleUserPageResult(IReadOnlyList<User> Items, int Total);

/// <summary>用户仓储接口</summary>
public interface IUserRepository
{
    Task<ConsoleUserPageResult> QueryConsolePageAsync(ConsoleUserPageQuery query);

    Task<IReadOnlyDictionary<long, IReadOnlyList<string>>> GetRoleNamesByUserIdsAsync(
        IReadOnlyCollection<long> userIds);

    Task<IReadOnlyList<long>> GetActiveUserIdsAsync(
        long tenantId,
        IReadOnlyCollection<long> userIds);

    // /// <summary>
    // /// 获取所有的 角色-API 关系
    // /// </summary>
    // /// <returns>List RoleModulePermission</returns>
    // Task<List<RoleModulePermission>> RoleModuleMaps();
}
