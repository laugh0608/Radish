using Radish.Model;

namespace Radish.IService
{
    public interface IUserService
    {
        Task<List<UserVo>> GetUsersAsync();
    }
}
