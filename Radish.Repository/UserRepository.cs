using Radish.IRepository;
using Radish.Model;

namespace Radish.Repository
{
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
}
