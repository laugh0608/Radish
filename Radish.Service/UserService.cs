using Radish.IRepository;
using Radish.IService;
using Radish.Model;

namespace Radish.Service;

/// <summary>
/// 示例用户服务：演示如何通过仓储层获取实体并输出到 API。
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<List<UserVo>> GetUsersAsync()
    {
        // 将仓储层返回的实体映射为外部可用的 UserVo，供示例接口与测试调用。
        var userList = await _userRepository.GetUsersAsync();
        return userList.Select(u => new UserVo { UName = u.Name }).ToList();
    }
}
