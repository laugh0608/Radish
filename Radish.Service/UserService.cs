using Radish.IService;
using Radish.Model;

namespace Radish.Service
{
    public class UserService : IUserService
    {
        public async Task<List<UserVo>> GetUsersAsync()
        {
            var _userRepository = new Repository.UserRepository();
            var userList = await _userRepository.GetUsersAsync();
            return userList.Select(u => new UserVo { UName = u.Name }).ToList();
        }
    }
}
