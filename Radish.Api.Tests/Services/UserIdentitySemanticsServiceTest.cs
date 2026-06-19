using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class UserIdentitySemanticsServiceTest
{
    [Fact]
    public async Task AddAsync_ShouldAssignPublicIdAndPublicIndex()
    {
        var harness = CreateHarness();
        var user = new User
        {
            LoginName = "tester",
            UserName = "Tester",
            UserEmail = "tester@example.test",
            LoginPassword = "hash",
            IsEnable = true
        };

        harness.BaseRepository
            .Setup(repository => repository.QueryMaxAsync(
                It.IsAny<Expression<Func<User, long?>>>(),
                It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(1085);
        harness.BaseRepository
            .Setup(repository => repository.AddAsync(It.IsAny<User>()))
            .ReturnsAsync(20003);

        var userId = await harness.Service.AddAsync(user);

        Assert.Equal(20003, userId);
        Assert.StartsWith("usr_", user.PublicId);
        Assert.Equal(1086, user.PublicIndex);
    }

    [Fact]
    public async Task GetEnabledUserByLoginNameAsync_ShouldMatchNormalizedLoginNameAndEmail()
    {
        var harness = CreateHarness();
        var capturedWheres = new List<Expression<Func<User, bool>>?>();
        var user = new User
        {
            Id = 20003,
            LoginName = "alice",
            UserName = "Alice",
            UserEmail = "alice@example.test",
            LoginPassword = "hash",
            IsEnable = true,
            IsDeleted = false
        };

        harness.BaseRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .Callback<Expression<Func<User, bool>>?>(where => capturedWheres.Add(where))
            .ReturnsAsync(user);

        var loginResult = await harness.Service.GetEnabledUserByLoginNameAsync("Alice");
        var emailResult = await harness.Service.GetEnabledUserByLoginNameAsync("ALICE@EXAMPLE.TEST");

        Assert.NotNull(loginResult);
        Assert.NotNull(emailResult);
        Assert.Equal(2, capturedWheres.Count);

        var loginPredicate = capturedWheres[0]!.Compile();
        Assert.True(loginPredicate(user));
        Assert.True(loginPredicate(CloneUser(user, loginName: "Alice")));
        Assert.False(loginPredicate(CloneUser(user, loginName: "ignored", userEmail: "alice@example.test")));

        var emailPredicate = capturedWheres[1]!.Compile();
        Assert.True(emailPredicate(CloneUser(user, loginName: "ignored", userEmail: "ALICE@EXAMPLE.TEST")));
        Assert.True(emailPredicate(CloneUser(user, loginName: "ignored", userEmail: "alice@example.test")));
        Assert.False(emailPredicate(CloneUser(user, loginName: "ignored", userEmail: "ignored@example.test")));
    }

    [Fact]
    public async Task SearchUsersForMentionAsync_ShouldNotMatchLoginNameOrEmail()
    {
        var harness = CreateHarness();
        Expression<Func<User, bool>>? capturedWhere = null;
        harness.BaseRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<User, bool>>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<User, object>>?>(),
                It.IsAny<OrderByType>()))
            .Callback<Expression<Func<User, bool>>?, int, int, Expression<Func<User, object>>?, OrderByType>(
                (where, _, _, _, _) => capturedWhere = where)
            .ReturnsAsync((new List<User>(), 0));

        await harness.Service.SearchUsersForMentionAsync("secret", tenantId: 0);

        Assert.NotNull(capturedWhere);
        var predicate = capturedWhere!.Compile();
        var user = new User
        {
            Id = 20003,
            LoginName = "secret",
            UserEmail = "secret@example.test",
            UserName = "Public Alice",
            PublicId = "usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f",
            PublicIndex = 1086,
            TenantId = 0,
            IsEnable = true,
            IsDeleted = false
        };

        Assert.False(predicate(user));
    }

    [Fact]
    public async Task SearchUsersForMentionAsync_ShouldMatchDisplayHandle()
    {
        var harness = CreateHarness();
        var alice = new User
        {
            Id = 20003,
            LoginName = "alice-login",
            UserEmail = "alice@example.test",
            UserName = "Alice",
            PublicId = "usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f",
            PublicIndex = 1086,
            TenantId = 0,
            IsEnable = true,
            IsDeleted = false
        };

        Expression<Func<User, bool>>? capturedWhere = null;
        harness.BaseRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<User, bool>>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<User, object>>?>(),
                It.IsAny<OrderByType>()))
            .Callback<Expression<Func<User, bool>>?, int, int, Expression<Func<User, object>>?, OrderByType>(
                (where, _, _, _, _) => capturedWhere = where)
            .ReturnsAsync((new List<User> { alice }, 1));

        var result = await harness.Service.SearchUsersForMentionAsync("Alice#1086", tenantId: 0);

        Assert.NotNull(capturedWhere);
        var predicate = capturedWhere!.Compile();
        Assert.True(predicate(alice));
        Assert.False(predicate(new User
        {
            Id = 20004,
            UserName = "Alice",
            PublicIndex = 1087,
            TenantId = 0,
            IsEnable = true,
            IsDeleted = false
        }));
        Assert.Single(result);
        Assert.Equal("Alice#1086", result[0].VoDisplayHandle);
    }

    private static Harness CreateHarness()
    {
        var mapper = new Mock<IMapper>();
        mapper.Setup(item => item.Map<UserVo>(It.IsAny<User>()))
            .Returns<User>(user => new UserVo
            {
                Uuid = user.Id,
                VoPublicId = user.PublicId,
                VoPublicIndex = user.PublicIndex,
                VoUserName = User.NormalizeDisplayName(user.UserName, user.Id),
                VoDisplayName = User.NormalizeDisplayName(user.UserName, user.Id),
                VoDisplayHandle = User.BuildDisplayHandle(user.UserName, user.PublicIndex, user.Id),
                VoLoginName = user.LoginName,
                VoUserEmail = user.UserEmail,
                VoLoginPassword = user.LoginPassword,
                VoTenantId = user.TenantId,
                VoIsEnable = user.IsEnable,
                VoIsDeleted = user.IsDeleted
            });
        mapper.Setup(item => item.Map<List<UserVo>>(It.IsAny<List<User>>()))
            .Returns<List<User>>(users => users.Select(user => new UserVo
            {
                Uuid = user.Id,
                VoPublicId = user.PublicId,
                VoPublicIndex = user.PublicIndex,
                VoUserName = User.NormalizeDisplayName(user.UserName, user.Id),
                VoDisplayName = User.NormalizeDisplayName(user.UserName, user.Id),
                VoDisplayHandle = User.BuildDisplayHandle(user.UserName, user.PublicIndex, user.Id),
                VoTenantId = user.TenantId,
                VoIsEnable = user.IsEnable,
                VoIsDeleted = user.IsDeleted
            }).ToList());
        mapper.Setup(item => item.Map<List<UserMentionVo>>(It.IsAny<List<UserVo>>()))
            .Returns<List<UserVo>>(users => users.Select(user => new UserMentionVo
            {
                VoId = user.Uuid,
                VoPublicId = user.VoPublicId,
                VoPublicIndex = user.VoPublicIndex,
                VoUserName = user.VoDisplayName,
                VoDisplayName = user.VoDisplayName,
                VoDisplayHandle = user.VoDisplayHandle
            }).ToList());

        var baseRepository = new Mock<IBaseRepository<User>>();
        var userRepository = new Mock<IUserRepository>();
        var roleRepository = new Mock<IBaseRepository<Role>>();
        var userRoleRepository = new Mock<IBaseRepository<UserRole>>();
        var departmentService = new Mock<IDepartmentService>();
        var consoleAuthorizationService = new Mock<IConsoleAuthorizationService>();
        var service = new UserService(
            departmentService.Object,
            mapper.Object,
            baseRepository.Object,
            userRepository.Object,
            roleRepository.Object,
            userRoleRepository.Object,
            consoleAuthorizationService.Object);

        return new Harness(service, baseRepository);
    }

    private static User CloneUser(User user, string? loginName = null, string? userEmail = null)
    {
        return new User
        {
            Id = user.Id,
            LoginName = loginName ?? user.LoginName,
            UserName = user.UserName,
            UserEmail = userEmail ?? user.UserEmail,
            LoginPassword = user.LoginPassword,
            PublicId = user.PublicId,
            PublicIndex = user.PublicIndex,
            TenantId = user.TenantId,
            IsEnable = user.IsEnable,
            IsDeleted = user.IsDeleted
        };
    }

    private sealed record Harness(
        UserService Service,
        Mock<IBaseRepository<User>> BaseRepository);
}
