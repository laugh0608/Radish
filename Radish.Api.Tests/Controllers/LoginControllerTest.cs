using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Localization;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Resources;
using Radish.Common.HelpTool;
using Radish.Extension;
using Radish.Extension.PermissionExtension;
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
        var errorsLocalizerMock = new Mock<IStringLocalizer<Errors>>();
        errorsLocalizerMock
            .Setup(l => l[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name));

        var controller = new LoginController(new FakeUserService(), NullLogger<LoginController>.Instance,
            new PermissionRequirement(), errorsLocalizerMock.Object);

        var result = await controller.GetJwtToken("test", "blogadmin");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ResponseData);
        Assert.False(string.IsNullOrEmpty(result.ResponseData.TokenInfo));

        // 额外校验：Token 中应包含统一的 OIDC 风格 Claim（sub/name/tenant_id）
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.ResponseData.TokenInfo);

        Assert.Equal("1", jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("test", jwt.Claims.First(c => c.Type == "name").Value);
        Assert.Equal("0", jwt.Claims.First(c => c.Type == "tenant_id").Value);
    }

    private sealed class FakeUserService : IUserService
    {
        private readonly List<User> _users;

        public FakeUserService()
        {
            var encryptedPwd = Md5Helper.Md5Encrypt32("blogadmin");
            _users = new List<User>
            {
                // new User
                // {
                //     Id = 1,
                //     LoginName = "blogadmin",
                //     LoginPassword = encryptedPwd,
                //     IsDeleted = false,
                //     TenantId = 0
                // }

                new User
                {
                    Id = 1,
                    LoginName = "test",
                    LoginPassword = encryptedPwd,
                    IsDeleted = false,
                    TenantId = 0
                }
            };
        }

        public Task<string> GetUserRoleNameStrAsync(string loginName, string loginPwd)
        {
            return Task.FromResult("Admin");
        }

        public Task<List<RoleModulePermission>> RoleModuleMaps()
        {
            throw new NotImplementedException();
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

        public Task<List<UserVo>> QueryWithCacheAsync(Expression<Func<User, bool>> whereExpression = null, int cacheTime = 10)
        {
            throw new NotImplementedException();
        }

        public Task<List<TResult>> QueryMuchAsync<T, T2, T3, TResult>(
            Expression<Func<T, T2, T3, object[]>> joinExpression, Expression<Func<T, T2, T3, TResult>> selectExpression,
            Expression<Func<T, T2, T3, bool>> whereLambda = null) where T : class, new()
        {
            throw new NotImplementedException();
        }

        public Task<List<User>> QuerySplitAsync(Expression<Func<User, bool>>? whereExpression,
            string orderByFields = "Id")
        {
            return Task.FromResult(new List<User>());
        }
    }
}