using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Api.Controllers;
using Radish.Common.HelpTool;
using Radish.Extension;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(LoginController))]
public class LoginControllerTest
{
    [Fact]
    public async Task GetJwtToken_ShouldReturnToken_WhenUserExists()
    {
        var controller = new LoginController(new FakeUserService(), NullLogger<LoginController>.Instance,
            new PermissionRequirement());

        var result = await controller.GetJwtToken("blogadmin", "blogadmin");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ResponseData);
        Assert.False(string.IsNullOrEmpty(result.ResponseData.TokenInfo));
    }

    private sealed class FakeUserService : IUserService
    {
        private readonly List<User> _users;

        public FakeUserService()
        {
            var encryptedPwd = Md5Helper.Md5Encrypt32("blogadmin");
            _users = new List<User>
            {
                new User
                {
                    Id = 1,
                    LoginName = "blogadmin",
                    LoginPassword = encryptedPwd,
                    IsDeleted = false,
                    TenantId = 1000001
                }
            };
        }

        public Task<string> GetUserRoleNameStrAsync(string loginName, string loginPwd)
        {
            return Task.FromResult("Admin");
        }

        public Task<List<UserVo>> GetUsersAsync() => Task.FromResult(new List<UserVo>());

        public Task<bool> TestTranPropagationUser() => Task.FromResult(true);

        public Task<long> AddAsync(User entity) => Task.FromResult(0L);

        public Task<List<long>> AddSplitAsync(User entity) => Task.FromResult(new List<long>());

        public Task<List<UserVo>> QueryAsync(Expression<Func<User, bool>>? whereExpression = null)
        {
            IEnumerable<User> data = _users;
            if (whereExpression != null)
            {
                var predicate = whereExpression.Compile();
                data = data.Where(predicate);
            }

            var result = data.Select(u => new UserVo
            {
                Uuid = u.Id,
                VoLoName = u.LoginName,
                VoLoPwd = u.LoginPassword,
                VoIsDeleted = u.IsDeleted,
                VoTenId = u.TenantId
            }).ToList();

            return Task.FromResult(result);
        }

        public Task<List<User>> QuerySplitAsync(Expression<Func<User, bool>>? whereExpression, string orderByFields = "Id")
        {
            return Task.FromResult(new List<User>());
        }
    }
}
