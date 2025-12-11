// using Radish.Common.HelpTool;
// using Radish.Extension;
// using Radish.Extension.PermissionExtension;
// using Radish.IService;
// using Radish.Model;
// using Radish.Model.ViewModels;
// using Xunit;

// @luobo 20251210 该测试已经弃用

// namespace Radish.Api.Tests.Controllers;
//
// [TestSubject(typeof(LoginController))]
// public class LoginControllerTest
// {
//     [Fact]
//     public void PasswordHasher_ShouldWork()
//     {
//         // 测试 Argon2id 密码哈希和验证
//         const string password = "test123456";
//         var hash = PasswordHasher.HashPassword(password);
//
//         Assert.NotEmpty(hash);
//         Assert.True(PasswordHasher.VerifyPassword(password, hash));
//         Assert.False(PasswordHasher.VerifyPassword("wrongpassword", hash));
//     }
//
//     [Fact]
//     public async Task GetJwtToken_ShouldReturn410_WhenLegacyEndpointCalled()
//     {
//         var errorsLocalizerMock = new Mock<IStringLocalizer<Errors>>();
//         errorsLocalizerMock
//             .Setup(l => l[It.IsAny<string>()])
//             .Returns((string name) => new LocalizedString(name, name));
//     
//         var fakeUserService = new FakeUserService();
//         var controller = new LoginController(fakeUserService, NullLogger<LoginController>.Instance,
//             new PermissionRequirement(), errorsLocalizerMock.Object);
//     
//         var result = await controller.GetJwtToken("test", "test123456");
//     
//         // 旧接口已废弃，应返回 410，并带上废弃提示文案
//         Assert.False(result.IsSuccess);
//         Assert.Equal(410, result.StatusCode);
//         Assert.Null(result.ResponseData);
//         Assert.Equal("error.auth.legacy_login_deprecated", result.MessageInfo);
//     }
//
//     private sealed class FakeUserService : IUserService
//     {
//         private readonly List<User> _users;
//
//         public FakeUserService()
//         {
//             // 使用 Argon2id 加密密码 (与新的种子数据保持一致: test/test123456)
//             var encryptedPwd = PasswordHasher.HashPassword("test123456");
//             _users = new List<User>
//             {
//                 new User
//                 {
//                     Id = 1,
//                     LoginName = "test",
//                     LoginPassword = encryptedPwd,
//                     IsDeleted = false,
//                     TenantId = 0
//                 }
//             };
//         }
//
//         public Task<string> GetUserRoleNameStrAsync(string loginName, string loginPwd)
//         {
//             // 注意：在 Argon2id 方案下，这个方法不应该再使用密码进行查询
//             // 因为密码已经在 LoginController 中验证过了
//             // 这里只需要通过用户名返回角色即可
//             var user = _users.FirstOrDefault(u => u.LoginName == loginName && !u.IsDeleted);
//             return Task.FromResult(user != null ? "Admin" : "");
//         }
//
//         public Task<List<RoleModulePermission>> RoleModuleMaps()
//         {
//             throw new NotImplementedException();
//         }
//
//         public Task<List<UserVo>> GetUsersAsync() => Task.FromResult(new List<UserVo>());
//
//         public Task<bool> TestTranPropagationUser() => Task.FromResult(true);
//
//         public Task<long> AddAsync(User entity) => Task.FromResult(0L);
//
//         public Task<List<long>> AddSplitAsync(User entity) => Task.FromResult(new List<long>());
//
//         public Task<List<UserVo>> QueryAsync(Expression<Func<User, bool>>? whereExpression = null)
//         {
//             IEnumerable<User> data = _users;
//             if (whereExpression != null)
//             {
//                 var predicate = whereExpression.Compile();
//                 data = data.Where(predicate);
//             }
//
//             var result = data.Select(u => new UserVo
//             {
//                 Uuid = u.Id,
//                 VoLoName = u.LoginName,
//                 VoLoPwd = u.LoginPassword,
//                 VoIsDeleted = u.IsDeleted,
//                 VoTenId = u.TenantId,
//                 // 设置其他必要的字段以避免空引用
//                 VoUsName = u.UserName,
//                 VoUsEmail = u.UserEmail,
//                 VoReNa = u.UserRealName,
//                 VoSexDo = u.UserSex,
//                 VoAgeDo = u.UserAge,
//                 VoBiTh = u.UserBirth,
//                 VoAdRes = u.UserAddress,
//                 VoStaCo = u.StatusCode,
//                 VoCreateTime = u.CreateTime,
//                 VoUpdateTime = u.UpdateTime,
//                 VoCrModifyTime = u.CriticalModifyTime,
//                 VoLaErrTime = u.LastErrorTime,
//                 VoErrorCount = u.ErrorCount,
//                 VoIsEnable = u.IsEnable,
//                 VoDeId = u.DepartmentId,
//                 VoDeNa = u.DepartmentName,
//                 VoRoIds = u.RoleIds,
//                 VoRoNas = u.RoleNames,
//                 VoDeIds = u.DepartmentIds,
//                 VoRemark = u.Remark
//             }).ToList();
//
//             return Task.FromResult(result);
//         }
//
//         public Task<List<UserVo>> QueryWithCacheAsync(Expression<Func<User, bool>> whereExpression = null, int cacheTime = 10)
//         {
//             throw new NotImplementedException();
//         }
//
//         public Task<List<TResult>> QueryMuchAsync<T, T2, T3, TResult>(
//             Expression<Func<T, T2, T3, object[]>> joinExpression, Expression<Func<T, T2, T3, TResult>> selectExpression,
//             Expression<Func<T, T2, T3, bool>> whereLambda = null) where T : class, new()
//         {
//             throw new NotImplementedException();
//         }
//
//         public Task<List<User>> QuerySplitAsync(Expression<Func<User, bool>>? whereExpression,
//             string orderByFields = "Id")
//         {
//             return Task.FromResult(new List<User>());
//         }
//     }
// }