using Radish.IRepository;

namespace Radish.Repository;

/// <summary>用户仓储</summary>
public class UserRepository : IUserRepository
{
    /// <summary>测试获取用户方法示例</summary>
    /// <returns></returns>
    public async Task<List<Model.User>> GetUsersAsync() 
    {
        await Task.CompletedTask;
        var data = new List<Model.User>
        {
            new Model.User { Id = 1, UserName = "Alice" },
            new Model.User { Id = 2, UserName = "Bob" }
        };
        return data;
    }
}
