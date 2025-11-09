using Radish.Model;

namespace Radish.IRepository
{
    public interface IUserRepository
    {
        Task<List<User>> GetUsersAsync();
    }
}
