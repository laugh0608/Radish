namespace Radish.IRepository;

/// <summary>用户仓储接口</summary>
public interface IUserRepository
{
    /// <summary>测试获取用户方法示例</summary>
    /// <returns></returns>
    Task<List<Model.User>> GetUsersAsync();
}
