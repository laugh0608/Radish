using Radish.IRepository;
using Radish.Model;

namespace Radish.Repository;

/// <summary>
/// 示例仓储：返回内存中的演示数据，用于串联 Service 与 API。
/// </summary>
public class UserRepository : IUserRepository
{
    public async Task<List<User>> GetUsersAsync() 
    {
        await Task.CompletedTask;
        var data = new List<User>
        {
            new User { Id = 1, Name = "Alice" },
            new User { Id = 2, Name = "Bob" }
        };
        return data;
    }
}
