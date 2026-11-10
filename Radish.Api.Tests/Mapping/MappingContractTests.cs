using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Radish.Model;
using Radish.Model.Models;
using Radish.Model.OpenIddict;
using Radish.Model.ViewModels;
using Radish.Service.Mapping;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Mapping;

public class MappingContractTests
{
    [Fact]
    public void UserMapping_ShouldPreserveIdentityAndCopyCollectionsWithoutExposingCredentials()
    {
        var user = new User
        {
            Id = 2042219067430928384, UserName = "萝卜", PublicId = "public-user", PublicIndex = 1234,
            LoginPassword = "private-password", TenantId = 29, RoleIds = [4, 2], RoleNames = ["admin"], DepartmentIds = [8]
        };
        var result = UserMapper.ToUserVo(user);
        Assert.Equal(user.Id, result.Uuid);
        Assert.Equal(user.PublicId, result.VoPublicId);
        Assert.Equal(user.PublicIndex, result.VoPublicIndex);
        Assert.Equal(user.TenantId, result.VoTenantId);
        Assert.Equal("萝卜", result.VoDisplayName);
        Assert.Equal(User.BuildDisplayHandle(user.UserName, user.PublicIndex, user.Id), result.VoDisplayHandle);
        Assert.Equal(user.RoleIds, result.VoRoleIds);
        result.VoRoleIds.Add(9);
        result.VoRoleNames.Clear();
        Assert.Equal(new long[] { 4, 2 }, user.RoleIds);
        Assert.Single(user.RoleNames);
        Assert.DoesNotContain("private-password", JsonSerializer.Serialize(result));
        Assert.Null(UserMapper.ToCurrentUserVo(user).VoAvatarUrl);
        Assert.Null(UserMapper.ToUserMentionVo(user).VoAvatar);
        var reverse = UserMapper.ToUser(result);
        Assert.Equal(user.Id, reverse.Id);
        Assert.True(string.IsNullOrEmpty(reverse.LoginPassword));
        Assert.NotSame(result.VoDepartmentIds, reverse.DepartmentIds);
    }

    [Fact]
    public void NullsAndLists_ShouldKeepNullObjectAndEmptyCollectionContracts()
    {
        Assert.Null(UserMapper.ToUserVo(null));
        Assert.Null(ForumMapper.ToPostVo(null));
        Assert.Empty(UserMapper.ToUserVoList(null));
        Assert.Empty(ForumMapper.ToCommentVoList(null));
        var result = UserMapper.ToUserVo(new User { RoleIds = null!, RoleNames = null!, DepartmentIds = null! });
        Assert.Empty(result.VoRoleIds);
        Assert.Empty(result.VoRoleNames);
        Assert.Empty(result.VoDepartmentIds);
        Assert.Equal(new long[] { 9, 2 }, ForumMapper.ToTagVoList([new Tag("first") { Id = 9 }, new Tag("second") { Id = 2 }]).Select(x => x.VoId));
    }

    [Theory]
    [InlineData(false, "private message")]
    [InlineData(true, null)]
    public void RecalledChat_ShouldHideContent(bool recalled, string? content)
    {
        var result = ChatMapper.ToChannelMessageVo(new ChannelMessage { Id = 3, Content = "private message", IsDeleted = recalled });
        Assert.Equal(recalled, result.VoIsRecalled);
        Assert.Equal(content, result.VoContent);
    }

    [Fact]
    public void WikiAccess_ShouldNormalizeInvalidVisibilityAndDeduplicateAccessLists()
    {
        var result = WikiMapper.ToWikiDocumentDetailVo(new WikiDocument
        {
            Visibility = 999, AllowedRoles = "| Admin |admin| Author |", AllowedPermissions = "|wiki.read|WIKI.READ|wiki.write|"
        });
        Assert.Equal((int)WikiDocumentVisibilityEnum.Authenticated, result.VoVisibility);
        Assert.Equal(new[] { "Admin", "Author" }, result.VoAllowedRoles);
        Assert.Equal(new[] { "wiki.read", "wiki.write" }, result.VoAllowedPermissions);
        var reverse = WikiMapper.ToWikiDocument(new WikiDocumentVo { VoAllowedRoles = [" Admin ", "ADMIN", ""], VoAllowedPermissions = [] });
        Assert.Equal("|admin|", reverse.AllowedRoles);
        Assert.Null(reverse.AllowedPermissions);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("short", "short")]
    public void OidcToken_ShouldHandleMissingAndShortPayload(string? payload, string? expected)
    {
        Assert.Equal(expected, OidcMapper.ToOidcTokenVo(new RadishToken { Payload = payload! }).VoPayloadPreview);
    }

    [Fact]
    public void Oidc_ShouldExcludeSecretAndLimitPayloadPreview()
    {
        var app = OidcMapper.ToOidcAppVo(new RadishApplication { ClientId = "client", ClientSecret = "private-secret" });
        Assert.True(string.IsNullOrEmpty(app.VoClientSecret));
        Assert.DoesNotContain("private-secret", JsonSerializer.Serialize(app));
        Assert.Equal(new string('x', 100) + "...", OidcMapper.ToOidcTokenVo(new RadishToken { Payload = new string('x', 150) }).VoPayloadPreview);
        var reverse = OidcMapper.ToRadishToken(new OidcTokenVo { VoPayloadPreview = "must-not-be-payload" });
        Assert.True(string.IsNullOrEmpty(reverse.Payload));
    }

    [Fact]
    public void ProductUpdate_ShouldPreserveServerOwnedFields()
    {
        var created = new DateTime(2025, 1, 1);
        var product = new Product { Id = 101, IsOnSale = true, SoldCount = 12, CreateTime = created, CreateId = 42, IsDeleted = true };
        ShopMapper.UpdateProduct(new UpdateProductDto { Id = 999, Name = "updated", Price = 250, ExpectedVersion = 99 }, product);
        // 更新 ID 沿用 DTO；服务已按该 ID 查询目标实体。
        Assert.Equal(999, product.Id);
        Assert.True(product.IsOnSale);
        Assert.True(product.IsDeleted);
        Assert.Equal(12, product.SoldCount);
        Assert.Equal(created, product.CreateTime);
        Assert.Equal(42, product.CreateId);
        Assert.Equal("updated", product.Name);
        Assert.Equal(250, product.Price);
    }

    [Fact]
    public void ReverseMapping_ShouldKeepConstructorValidation()
    {
        Assert.Throws<ArgumentNullException>(() => ForumMapper.ToCategory(new CategoryVo { VoName = null! }));
        Assert.Throws<ArgumentNullException>(() => ForumMapper.ToTag(new TagVo { VoName = null! }));
    }

    [Fact]
    public void ForumReverseMapping_ShouldKeepTenantAndAuditOwnershipInService()
    {
        var result = ForumMapper.ToPost(new PostVo
        {
            VoId = 8, VoTitle = "title", VoContent = "body", VoSummary = null,
            VoAuthorId = 42, VoAuthorName = "author", VoContentRevision = 3
        });
        Assert.Equal(8, result.Id);
        Assert.Equal(42, result.AuthorId);
        Assert.Equal(3, result.ContentRevision);
        Assert.Equal(0, result.TenantId);
        Assert.Equal(0, result.CreateId);
        Assert.Null(result.Summary);
        Assert.False(result.IsDeleted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-json")]
    [InlineData("null")]
    public void LevelPrivileges_ShouldKeepLegacyEmptyListForMissingOrInvalidData(string? privileges)
    {
        var result = ExperienceMapper.ToLevelConfigVo(new LevelConfig { Privileges = privileges });
        Assert.NotNull(result.VoPrivileges);
        Assert.Empty(result.VoPrivileges);
    }

    [Fact]
    public void LevelPrivileges_ShouldRoundTripValidJsonAndLeaveAssetUrlsForService()
    {
        var result = ExperienceMapper.ToLevelConfigVo(new LevelConfig { Privileges = "[\"first\",\"second\"]", IconAttachmentId = 9 });
        Assert.Equal(new[] { "first", "second" }, result.VoPrivileges);
        Assert.Null(result.VoIconUrl);
        Assert.Equal(9, result.VoIconAttachmentId);
        Assert.Equal("[\"first\",\"second\"]", ExperienceMapper.ToLevelConfig(result).Privileges);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PaymentPassword_ShouldExposeStatusWithoutHash(bool hasPassword)
    {
        var result = PaymentPasswordMapper.ToUserPaymentPasswordVo(new UserPaymentPassword
        {
            PasswordHash = hasPassword ? "private-passcode-hash" : "", LockedUntil = DateTime.Now.AddMinutes(30), PasscodeVersion = null
        });
        Assert.Equal(hasPassword, result.VoHasPaymentPassword);
        Assert.Equal(hasPassword, result.VoRequiresPasscodeUpgrade);
        Assert.True(result.VoIsLocked);
        Assert.InRange(result.VoLockedRemainingMinutes, 29, 30);
        Assert.DoesNotContain("private-passcode-hash", JsonSerializer.Serialize(result));
    }
}
