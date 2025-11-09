using Radish.IRepository.User;
using Radish.IService.User;
using Radish.Model;

namespace Radish.Service.User;

/// <summary>用户服务类</summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>测试获取用户服务示例</summary>
    /// <remarks>返回的是视图 Vo 模型，隔离实际实体类</remarks>
    /// <returns></returns>
    public async Task<List<UserVo>> GetUsersAsync()
    {
        // 将仓储层返回的实体映射为外部可用的 UserVo，供示例接口与测试调用。
        var userList = await _userRepository.GetUsersAsync();
        return userList.Select(u => new UserVo { VoUsName = u.UserName }).ToList();
    }
}
