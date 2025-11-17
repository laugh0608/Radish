using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>用户服务接口类</summary>
public interface IUserService
{
    /// <summary>测试获取用户服务示例</summary>
    /// <remarks>返回的是视图 Vo 模型，隔离实际实体类</remarks>
    /// <returns></returns>
    Task<List<UserVo>> GetUsersAsync();
    
    /// <summary>测试使用同事务</summary>
    /// <returns></returns>
    Task<bool> TestTranPropagationUser();
}