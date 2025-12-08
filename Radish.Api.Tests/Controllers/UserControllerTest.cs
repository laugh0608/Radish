using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(UserController))]
public class UserControllerTest
{
    private static UserController CreateControllerWithUser(long userId, string userName, long tenantId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
            {
                new("sub", userId.ToString()),
                new("name", userName),
                new("tenant_id", tenantId.ToString()),
                new(ClaimTypes.Role, "System"),
                new("scope", "radish-api")
            }, "TestAuth"))
        };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var httpContextUser = new HttpContextUser(accessor, NullLogger<HttpContextUser>.Instance);
        var userService = new FakeUserService();

        var controller = new UserController(userService, httpContextUser)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return controller;
    }

    [Fact]
    public async Task GetUserByHttpContext_Should_Return_Current_User_Info_From_Claims()
    {
        // Arrange
        const long expectedUserId = 20002;
        const string expectedUserName = "test-user";
        const long expectedTenantId = 30000;

        var controller = CreateControllerWithUser(expectedUserId, expectedUserName, expectedTenantId);

        // Act
        var result = await controller.GetUserByHttpContext();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ResponseData);

        var json = System.Text.Json.JsonSerializer.Serialize(result.ResponseData);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(expectedUserId, root.GetProperty("userId").GetInt64());
        Assert.Equal(expectedUserName, root.GetProperty("userName").GetString());
        Assert.Equal(expectedTenantId, root.GetProperty("tenantId").GetInt64());
    }

    private sealed class FakeUserService : IUserService
    {
        public Task<List<UserVo>> QueryAsync(System.Linq.Expressions.Expression<System.Func<User, bool>>? whereExpression = null)
        {
            // 对于 GetUserByHttpContext，我们目前只关心 HttpContext 中的 Claim，
            // 不依赖 UserService，因此这里返回空列表即可。
            return Task.FromResult(new List<UserVo>());
        }

        #region 未在当前测试中使用的方法，抛出 NotImplemented 以防误用

        public Task<string> GetUserRoleNameStrAsync(string loginName, string loginPwd) => throw new System.NotImplementedException();
        public Task<List<RoleModulePermission>> RoleModuleMaps() => throw new System.NotImplementedException();
        public Task<List<UserVo>> GetUsersAsync() => throw new System.NotImplementedException();
        public Task<bool> TestTranPropagationUser() => throw new System.NotImplementedException();
        public Task<long> AddAsync(User entity) => throw new System.NotImplementedException();
        public Task<List<long>> AddSplitAsync(User entity) => throw new System.NotImplementedException();
        public Task<List<UserVo>> QueryWithCacheAsync(System.Linq.Expressions.Expression<System.Func<User, bool>>? whereExpression = null, int cacheTime = 10) => throw new System.NotImplementedException();
        public Task<List<TResult>> QueryMuchAsync<T, T2, T3, TResult>(
            System.Linq.Expressions.Expression<System.Func<T, T2, T3, object[]>> joinExpression,
            System.Linq.Expressions.Expression<System.Func<T, T2, T3, TResult>> selectExpression,
            System.Linq.Expressions.Expression<System.Func<T, T2, T3, bool>>? whereLambda = null) where T : class, new() => throw new System.NotImplementedException();
        public Task<List<User>> QuerySplitAsync(System.Linq.Expressions.Expression<System.Func<User, bool>>? whereExpression, string orderByFields = "Id") => throw new System.NotImplementedException();

        #endregion
    }
}
