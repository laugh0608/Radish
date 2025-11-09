using Radish.Model;

namespace Radish.IService
{
    /// <summary>
    /// 用户服务契约，供示例 API 与单元测试调用。
    /// </summary>
    public interface IUserService
    {
        Task<List<UserVo>> GetUsersAsync();
    }
}
