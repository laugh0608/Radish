using Radish.Model;

namespace Radish.IRepository;

/// <summary>
/// 用户仓储契约，示例 API 依赖此接口获取演示数据。
/// </summary>
public interface IUserRepository
{
    Task<List<User>> GetUsersAsync();
}
