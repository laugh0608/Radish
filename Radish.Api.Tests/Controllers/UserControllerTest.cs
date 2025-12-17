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
        var postService = new FakePostService();
        var commentService = new FakeCommentService();

        var controller = new UserController(userService, httpContextUser, postService, commentService)
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
        public Task<List<UserMentionVo>> SearchUsersForMentionAsync(string keyword, int limit = 10) => throw new System.NotImplementedException();
        public Task<long> AddAsync(User entity) => throw new System.NotImplementedException();
        public Task<int> AddRangeAsync(List<User> entities) => throw new System.NotImplementedException();
        public Task<List<long>> AddSplitAsync(User entity) => throw new System.NotImplementedException();
        public Task<bool> DeleteByIdAsync(long id) => throw new System.NotImplementedException();
        public Task<bool> DeleteAsync(User entity) => throw new System.NotImplementedException();
        public Task<int> DeleteAsync(System.Linq.Expressions.Expression<System.Func<User, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<int> DeleteByIdsAsync(List<long> ids) => throw new System.NotImplementedException();
        public Task<bool> UpdateAsync(User entity) => throw new System.NotImplementedException();
        public Task<int> UpdateRangeAsync(List<User> entities) => throw new System.NotImplementedException();
        public Task<bool> UpdateColumnsAsync(User entity, System.Linq.Expressions.Expression<System.Func<User, object>> updateColumns) => throw new System.NotImplementedException();
        public Task<int> UpdateColumnsAsync(System.Linq.Expressions.Expression<System.Func<User, User>> updateColumns, System.Linq.Expressions.Expression<System.Func<User, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<UserVo?> QueryByIdAsync(long id) => throw new System.NotImplementedException();
        public Task<UserVo?> QueryFirstAsync(System.Linq.Expressions.Expression<System.Func<User, bool>>? whereExpression = null) => throw new System.NotImplementedException();
        public Task<UserVo?> QuerySingleAsync(System.Linq.Expressions.Expression<System.Func<User, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<(List<UserVo> data, int totalCount)> QueryPageAsync(System.Linq.Expressions.Expression<System.Func<User, bool>>? whereExpression = null, int pageIndex = 1, int pageSize = 20, System.Linq.Expressions.Expression<System.Func<User, object>>? orderByExpression = null, SqlSugar.OrderByType orderByType = SqlSugar.OrderByType.Asc) => throw new System.NotImplementedException();
        public Task<int> QueryCountAsync(System.Linq.Expressions.Expression<System.Func<User, bool>>? whereExpression = null) => throw new System.NotImplementedException();
        public Task<bool> QueryExistsAsync(System.Linq.Expressions.Expression<System.Func<User, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<List<UserVo>> QueryWithCacheAsync(System.Linq.Expressions.Expression<System.Func<User, bool>>? whereExpression = null, int cacheTime = 10) => throw new System.NotImplementedException();
        public Task<List<TResult>> QueryMuchAsync<T, T2, T3, TResult>(
            System.Linq.Expressions.Expression<System.Func<T, T2, T3, object[]>> joinExpression,
            System.Linq.Expressions.Expression<System.Func<T, T2, T3, TResult>> selectExpression,
            System.Linq.Expressions.Expression<System.Func<T, T2, T3, bool>>? whereLambda = null) where T : class, new() => throw new System.NotImplementedException();
        public Task<List<User>> QuerySplitAsync(System.Linq.Expressions.Expression<System.Func<User, bool>>? whereExpression, string orderByFields = "Id") => throw new System.NotImplementedException();

        #endregion
    }

    private sealed class FakePostService : IPostService
    {
        public Task<List<PostVo>> QueryAsync(System.Linq.Expressions.Expression<System.Func<Post, bool>>? whereExpression = null)
        {
            return Task.FromResult(new List<PostVo>());
        }

        public Task<int> QueryCountAsync(System.Linq.Expressions.Expression<System.Func<Post, bool>>? whereExpression = null)
        {
            return Task.FromResult(0);
        }

        #region 未在当前测试中使用的方法

        public Task<long> PublishPostAsync(Post post, List<string>? tagNames = null) => throw new System.NotImplementedException();
        public Task<PostVo?> GetPostDetailAsync(long postId) => throw new System.NotImplementedException();
        public Task IncrementViewCountAsync(long postId) => throw new System.NotImplementedException();
        public Task UpdateLikeCountAsync(long postId, int delta) => throw new System.NotImplementedException();
        public Task UpdateCommentCountAsync(long postId, int delta) => throw new System.NotImplementedException();
        public Task<long> AddAsync(Post entity) => throw new System.NotImplementedException();
        public Task<int> AddRangeAsync(List<Post> entities) => throw new System.NotImplementedException();
        public Task<List<long>> AddSplitAsync(Post entity) => throw new System.NotImplementedException();
        public Task<bool> DeleteByIdAsync(long id) => throw new System.NotImplementedException();
        public Task<bool> DeleteAsync(Post entity) => throw new System.NotImplementedException();
        public Task<int> DeleteAsync(System.Linq.Expressions.Expression<System.Func<Post, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<int> DeleteByIdsAsync(List<long> ids) => throw new System.NotImplementedException();
        public Task<bool> UpdateAsync(Post entity) => throw new System.NotImplementedException();
        public Task<int> UpdateRangeAsync(List<Post> entities) => throw new System.NotImplementedException();
        public Task<bool> UpdateColumnsAsync(Post entity, System.Linq.Expressions.Expression<System.Func<Post, object>> updateColumns) => throw new System.NotImplementedException();
        public Task<int> UpdateColumnsAsync(System.Linq.Expressions.Expression<System.Func<Post, Post>> updateColumns, System.Linq.Expressions.Expression<System.Func<Post, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<PostVo?> QueryByIdAsync(long id) => throw new System.NotImplementedException();
        public Task<PostVo?> QueryFirstAsync(System.Linq.Expressions.Expression<System.Func<Post, bool>>? whereExpression = null) => throw new System.NotImplementedException();
        public Task<PostVo?> QuerySingleAsync(System.Linq.Expressions.Expression<System.Func<Post, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<(List<PostVo> data, int totalCount)> QueryPageAsync(System.Linq.Expressions.Expression<System.Func<Post, bool>>? whereExpression = null, int pageIndex = 1, int pageSize = 20, System.Linq.Expressions.Expression<System.Func<Post, object>>? orderByExpression = null, SqlSugar.OrderByType orderByType = SqlSugar.OrderByType.Asc) => throw new System.NotImplementedException();
        public Task<bool> QueryExistsAsync(System.Linq.Expressions.Expression<System.Func<Post, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<List<PostVo>> QueryWithCacheAsync(System.Linq.Expressions.Expression<System.Func<Post, bool>>? whereExpression = null, int cacheTime = 10) => throw new System.NotImplementedException();
        public Task<List<TResult>> QueryMuchAsync<T, T2, T3, TResult>(System.Linq.Expressions.Expression<System.Func<T, T2, T3, object[]>> joinExpression, System.Linq.Expressions.Expression<System.Func<T, T2, T3, TResult>> selectExpression, System.Linq.Expressions.Expression<System.Func<T, T2, T3, bool>>? whereLambda = null) where T : class, new() => throw new System.NotImplementedException();
        public Task<List<Post>> QuerySplitAsync(System.Linq.Expressions.Expression<System.Func<Post, bool>>? whereExpression, string orderByFields = "Id") => throw new System.NotImplementedException();

        #endregion
    }

    private sealed class FakeCommentService : ICommentService
    {
        public Task<List<CommentVo>> QueryAsync(System.Linq.Expressions.Expression<System.Func<Comment, bool>>? whereExpression = null)
        {
            return Task.FromResult(new List<CommentVo>());
        }

        public Task<int> QueryCountAsync(System.Linq.Expressions.Expression<System.Func<Comment, bool>>? whereExpression = null)
        {
            return Task.FromResult(0);
        }

        #region 未在当前测试中使用的方法

        public Task<long> AddCommentAsync(Comment comment) => throw new System.NotImplementedException();
        public Task<List<CommentVo>> GetCommentTreeAsync(long postId) => throw new System.NotImplementedException();
        public Task UpdateLikeCountAsync(long commentId, int delta) => throw new System.NotImplementedException();
        public Task UpdateReplyCountAsync(long commentId, int delta) => throw new System.NotImplementedException();
        public Task<CommentLikeResultDto> ToggleLikeAsync(long userId, long commentId) => throw new System.NotImplementedException();
        public Task<System.Collections.Generic.Dictionary<long, bool>> GetUserLikeStatusAsync(long userId, System.Collections.Generic.List<long> commentIds) => throw new System.NotImplementedException();
        public Task<List<CommentVo>> GetCommentTreeWithLikeStatusAsync(long postId, long? userId = null) => throw new System.NotImplementedException();
        public Task<long> AddAsync(Comment entity) => throw new System.NotImplementedException();
        public Task<int> AddRangeAsync(List<Comment> entities) => throw new System.NotImplementedException();
        public Task<List<long>> AddSplitAsync(Comment entity) => throw new System.NotImplementedException();
        public Task<bool> DeleteByIdAsync(long id) => throw new System.NotImplementedException();
        public Task<bool> DeleteAsync(Comment entity) => throw new System.NotImplementedException();
        public Task<int> DeleteAsync(System.Linq.Expressions.Expression<System.Func<Comment, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<int> DeleteByIdsAsync(List<long> ids) => throw new System.NotImplementedException();
        public Task<bool> UpdateAsync(Comment entity) => throw new System.NotImplementedException();
        public Task<int> UpdateRangeAsync(List<Comment> entities) => throw new System.NotImplementedException();
        public Task<bool> UpdateColumnsAsync(Comment entity, System.Linq.Expressions.Expression<System.Func<Comment, object>> updateColumns) => throw new System.NotImplementedException();
        public Task<int> UpdateColumnsAsync(System.Linq.Expressions.Expression<System.Func<Comment, Comment>> updateColumns, System.Linq.Expressions.Expression<System.Func<Comment, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<CommentVo?> QueryByIdAsync(long id) => throw new System.NotImplementedException();
        public Task<CommentVo?> QueryFirstAsync(System.Linq.Expressions.Expression<System.Func<Comment, bool>>? whereExpression = null) => throw new System.NotImplementedException();
        public Task<CommentVo?> QuerySingleAsync(System.Linq.Expressions.Expression<System.Func<Comment, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<(List<CommentVo> data, int totalCount)> QueryPageAsync(System.Linq.Expressions.Expression<System.Func<Comment, bool>>? whereExpression = null, int pageIndex = 1, int pageSize = 20, System.Linq.Expressions.Expression<System.Func<Comment, object>>? orderByExpression = null, SqlSugar.OrderByType orderByType = SqlSugar.OrderByType.Asc) => throw new System.NotImplementedException();
        public Task<bool> QueryExistsAsync(System.Linq.Expressions.Expression<System.Func<Comment, bool>> whereExpression) => throw new System.NotImplementedException();
        public Task<List<CommentVo>> QueryWithCacheAsync(System.Linq.Expressions.Expression<System.Func<Comment, bool>>? whereExpression = null, int cacheTime = 10) => throw new System.NotImplementedException();
        public Task<List<TResult>> QueryMuchAsync<T, T2, T3, TResult>(System.Linq.Expressions.Expression<System.Func<T, T2, T3, object[]>> joinExpression, System.Linq.Expressions.Expression<System.Func<T, T2, T3, TResult>> selectExpression, System.Linq.Expressions.Expression<System.Func<T, T2, T3, bool>>? whereLambda = null) where T : class, new() => throw new System.NotImplementedException();
        public Task<List<Comment>> QuerySplitAsync(System.Linq.Expressions.Expression<System.Func<Comment, bool>>? whereExpression, string orderByFields = "Id") => throw new System.NotImplementedException();

        #endregion
    }
}
