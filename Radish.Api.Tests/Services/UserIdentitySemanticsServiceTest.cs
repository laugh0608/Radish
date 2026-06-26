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
using Radish.Model.DtoModels;
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
    public async Task AddAsync_ShouldSkipReservedPublicIndex()
    {
        var harness = CreateHarness(
            reservedIndexes: "[1086]",
            vanityRules: "{}");
        var user = new User
        {
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

        await harness.Service.AddAsync(user);

        Assert.Equal(1087, user.PublicIndex);
    }

    [Fact]
    public async Task AddAsync_ShouldSkipVanityRulePublicIndex()
    {
        var harness = CreateHarness(
            reservedIndexes: "[]",
            vanityRules: "{\"repeatedDigits\":true}");
        var user = new User
        {
            UserName = "Tester",
            UserEmail = "tester@example.test",
            LoginPassword = "hash",
            IsEnable = true
        };

        harness.BaseRepository
            .Setup(repository => repository.QueryMaxAsync(
                It.IsAny<Expression<Func<User, long?>>>(),
                It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(1110);
        harness.BaseRepository
            .Setup(repository => repository.AddAsync(It.IsAny<User>()))
            .ReturnsAsync(20003);

        await harness.Service.AddAsync(user);

        Assert.Equal(1112, user.PublicIndex);
    }

    [Fact]
    public async Task AddAsync_ShouldExposeDuplicateReservedPublicIndexConfig()
    {
        var harness = CreateHarness(
            reservedIndexes: "[1086,1086]",
            vanityRules: "{}");
        var user = new User
        {
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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.AddAsync(user));

        Assert.Contains("重复值", exception.Message);
    }

    [Fact]
    public async Task AddAsync_ShouldExposeReservedPublicIndexBelowNormalStart()
    {
        var harness = CreateHarness(
            reservedIndexes: "[999]",
            vanityRules: "{}");
        var user = new User
        {
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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.AddAsync(user));

        Assert.Contains("不能小于 1000", exception.Message);
    }

    [Fact]
    public async Task GetEnabledUserByEmailAsync_ShouldMatchNormalizedEmailOnly()
    {
        var harness = CreateHarness();
        var capturedWheres = new List<Expression<Func<User, bool>>?>();
        var user = new User
        {
            Id = 20003,
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

        var emailResult = await harness.Service.GetEnabledUserByEmailAsync("ALICE@EXAMPLE.TEST");

        Assert.NotNull(emailResult);
        Assert.Single(capturedWheres);

        var emailPredicate = capturedWheres[0]!.Compile();
        Assert.True(emailPredicate(CloneUser(user, userEmail: "ALICE@EXAMPLE.TEST")));
        Assert.True(emailPredicate(CloneUser(user, userEmail: "alice@example.test")));
        Assert.False(emailPredicate(CloneUser(user, userEmail: "ignored@example.test")));
    }

    [Fact]
    public async Task SearchUsersForMentionAsync_ShouldNotMatchEmail()
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

    [Fact]
    public async Task ChangeDisplayNameAsync_ShouldUpdateUserAndAddAuditRecord()
    {
        var harness = CreateHarness();
        SetupDisplayNameSettings(harness, cooldownDays: 30, windowDays: 365, windowMaxCount: 3);
        var user = new User
        {
            Id = 1001,
            UserName = "OldName",
            TenantId = 7,
            IsEnable = true,
            IsDeleted = false
        };
        UserDisplayNameChangeRecord? capturedRecord = null;

        harness.BaseRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync(user);
        harness.DisplayNameChangeRecordRepository
            .Setup(repository => repository.QueryWithOrderAsync(
                It.IsAny<Expression<Func<UserDisplayNameChangeRecord, bool>>?>(),
                It.IsAny<Expression<Func<UserDisplayNameChangeRecord, object>>>(),
                OrderByType.Desc,
                1))
            .ReturnsAsync(new List<UserDisplayNameChangeRecord>());
        harness.DisplayNameChangeRecordRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<UserDisplayNameChangeRecord, bool>>?>()))
            .ReturnsAsync(0);
        harness.BaseRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<User, User>>>(),
                It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(1);
        harness.DisplayNameChangeRecordRepository
            .Setup(repository => repository.AddAsync(It.IsAny<UserDisplayNameChangeRecord>()))
            .Callback<UserDisplayNameChangeRecord>(record => capturedRecord = record)
            .ReturnsAsync(9001);

        var changed = await harness.Service.ChangeDisplayNameAsync(
            user.Id,
            "NewName",
            new UserDisplayNameChangeContext
            {
                OperatorUserId = user.Id,
                OperatorUserName = "OldName",
                Source = UserDisplayNameChangeSources.Profile,
                Reason = "用户个人资料修改"
            });

        Assert.True(changed);
        Assert.NotNull(capturedRecord);
        Assert.Equal(user.TenantId, capturedRecord!.TenantId);
        Assert.Equal(user.Id, capturedRecord.UserId);
        Assert.Equal("OldName", capturedRecord.OldDisplayName);
        Assert.Equal("NewName", capturedRecord.NewDisplayName);
        Assert.Equal(UserDisplayNameChangeSources.Profile, capturedRecord.ChangeSource);
    }

    [Fact]
    public async Task ChangeDisplayNameAsync_ShouldReturnFalse_WhenDisplayNameIsUnchanged()
    {
        var harness = CreateHarness();
        SetupDisplayNameSettings(harness);
        var user = new User
        {
            Id = 1001,
            UserName = "SameName",
            TenantId = 7,
            IsEnable = true,
            IsDeleted = false
        };

        harness.BaseRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync(user);

        var changed = await harness.Service.ChangeDisplayNameAsync(
            user.Id,
            "SameName",
            new UserDisplayNameChangeContext { OperatorUserId = user.Id, OperatorUserName = "SameName" });

        Assert.False(changed);
        harness.BaseRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<User, User>>>(),
                It.IsAny<Expression<Func<User, bool>>>()),
            Times.Never);
        harness.DisplayNameChangeRecordRepository.Verify(
            repository => repository.AddAsync(It.IsAny<UserDisplayNameChangeRecord>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangeDisplayNameAsync_ShouldRejectUnsupportedCharacters()
    {
        var harness = CreateHarness();
        SetupDisplayNameSettings(harness);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Service.ChangeDisplayNameAsync(
                1001,
                "New Name",
                new UserDisplayNameChangeContext { OperatorUserId = 1001, OperatorUserName = "OldName" }));

        Assert.Contains("展示名只能包含中文、英文字母和数字", exception.Message);
        harness.BaseRepository.Verify(
            repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangeDisplayNameAsync_ShouldRejectChange_WhenCooldownIsActive()
    {
        var harness = CreateHarness();
        SetupDisplayNameSettings(harness, cooldownDays: 30, windowDays: 365, windowMaxCount: 3);
        var user = new User
        {
            Id = 1001,
            UserName = "OldName",
            TenantId = 7,
            IsEnable = true,
            IsDeleted = false
        };

        harness.BaseRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync(user);
        harness.DisplayNameChangeRecordRepository
            .Setup(repository => repository.QueryWithOrderAsync(
                It.IsAny<Expression<Func<UserDisplayNameChangeRecord, bool>>?>(),
                It.IsAny<Expression<Func<UserDisplayNameChangeRecord, object>>>(),
                OrderByType.Desc,
                1))
            .ReturnsAsync(new List<UserDisplayNameChangeRecord>
            {
                new()
                {
                    TenantId = user.TenantId,
                    UserId = user.Id,
                    ChangeTime = DateTime.UtcNow.AddDays(-5)
                }
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.ChangeDisplayNameAsync(
                user.Id,
                "NewName",
                new UserDisplayNameChangeContext { OperatorUserId = user.Id, OperatorUserName = "OldName" }));

        Assert.Contains("显示名修改过于频繁", exception.Message);
        harness.BaseRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<User, User>>>(),
                It.IsAny<Expression<Func<User, bool>>>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangeDisplayNameAsync_ShouldRejectChange_WhenWindowCountExceeded()
    {
        var harness = CreateHarness();
        SetupDisplayNameSettings(harness, cooldownDays: 0, windowDays: 365, windowMaxCount: 3);
        var user = new User
        {
            Id = 1001,
            UserName = "OldName",
            TenantId = 7,
            IsEnable = true,
            IsDeleted = false
        };

        harness.BaseRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync(user);
        harness.DisplayNameChangeRecordRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<UserDisplayNameChangeRecord, bool>>?>()))
            .ReturnsAsync(3);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.ChangeDisplayNameAsync(
                user.Id,
                "NewName",
                new UserDisplayNameChangeContext { OperatorUserId = user.Id, OperatorUserName = "OldName" }));

        Assert.Equal("显示名在 365 天内最多可修改 3 次", exception.Message);
        harness.BaseRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<User, User>>>(),
                It.IsAny<Expression<Func<User, bool>>>()),
            Times.Never);
    }

    private static Harness CreateHarness(
        string? reservedIndexes = null,
        string? vanityRules = null)
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
        var displayNameChangeRecordRepository = new Mock<IBaseRepository<UserDisplayNameChangeRecord>>();
        var departmentService = new Mock<IDepartmentService>();
        var consoleAuthorizationService = new Mock<IConsoleAuthorizationService>();
        var systemSettingProvider = new Mock<ISystemSettingProvider>();
        SetupPublicIndexSettings(systemSettingProvider, reservedIndexes, vanityRules);
        var service = new UserService(
            departmentService.Object,
            mapper.Object,
            baseRepository.Object,
            userRepository.Object,
            roleRepository.Object,
            userRoleRepository.Object,
            displayNameChangeRecordRepository.Object,
            consoleAuthorizationService.Object,
            systemSettingProvider.Object);

        return new Harness(service, baseRepository, displayNameChangeRecordRepository, systemSettingProvider);
    }

    private static void SetupPublicIndexSettings(
        Mock<ISystemSettingProvider> systemSettingProvider,
        string? reservedIndexes,
        string? vanityRules)
    {
        systemSettingProvider
            .Setup(provider => provider.GetEffectiveValueAsync(SystemConfigDefaults.PublicIndexReservedIndexesKey))
            .ReturnsAsync(reservedIndexes ?? SystemConfigDefaults.DefaultPublicIndexReservedIndexes);
        systemSettingProvider
            .Setup(provider => provider.GetEffectiveValueAsync(SystemConfigDefaults.PublicIndexVanityRulesKey))
            .ReturnsAsync(vanityRules ?? SystemConfigDefaults.DefaultPublicIndexVanityRules);
    }

    private static void SetupDisplayNameSettings(
        Harness harness,
        int minLength = 2,
        int maxLength = 24,
        int cooldownDays = 0,
        int windowDays = 365,
        int windowMaxCount = 3)
    {
        harness.SystemSettingProvider
            .Setup(provider => provider.GetInt32Async(SystemConfigDefaults.DisplayNameMinLengthKey))
            .ReturnsAsync(minLength);
        harness.SystemSettingProvider
            .Setup(provider => provider.GetInt32Async(SystemConfigDefaults.DisplayNameMaxLengthKey))
            .ReturnsAsync(maxLength);
        harness.SystemSettingProvider
            .Setup(provider => provider.GetInt32Async(SystemConfigDefaults.DisplayNameChangeCooldownDaysKey))
            .ReturnsAsync(cooldownDays);
        harness.SystemSettingProvider
            .Setup(provider => provider.GetInt32Async(SystemConfigDefaults.DisplayNameChangeWindowDaysKey))
            .ReturnsAsync(windowDays);
        harness.SystemSettingProvider
            .Setup(provider => provider.GetInt32Async(SystemConfigDefaults.DisplayNameChangeWindowMaxCountKey))
            .ReturnsAsync(windowMaxCount);
    }

    private static User CloneUser(User user, string? userEmail = null)
    {
        return new User
        {
            Id = user.Id,
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
        Mock<IBaseRepository<User>> BaseRepository,
        Mock<IBaseRepository<UserDisplayNameChangeRecord>> DisplayNameChangeRecordRepository,
        Mock<ISystemSettingProvider> SystemSettingProvider);
}
