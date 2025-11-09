using Radish.Model;

namespace Radish.IService.User;

/// <summary>用户服务接口类</summary>
public interface IUserService
{
    /// <summary>测试获取用户服务示例</summary>
    /// <remarks>返回的是视图 Vo 模型，隔离实际实体类</remarks>
    /// <returns></returns>
    Task<List<UserVo>> GetUsersAsync();
}