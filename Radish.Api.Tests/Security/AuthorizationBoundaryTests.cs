using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Controllers;
using Radish.Api.Filters;
using Radish.Common.PermissionTool;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Security;

public class AuthorizationBoundaryTests
{
    [Fact]
    public void ConsoleProtectedActions_ShouldRequireAuthenticationAndNotAllowAnonymous()
    {
        var protectedActions = typeof(UserController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<RequireConsolePermissionAttribute>(true).Any())
                .Select(method => new { Controller = type, Action = method }))
            .ToList();

        protectedActions.ShouldNotBeEmpty();

        foreach (var item in protectedActions)
        {
            var hasAllowAnonymous =
                item.Controller.GetCustomAttributes<AllowAnonymousAttribute>(true).Any() ||
                item.Action.GetCustomAttributes<AllowAnonymousAttribute>(true).Any();
            var hasAuthorize =
                item.Controller.GetCustomAttributes<AuthorizeAttribute>(true).Any() ||
                item.Action.GetCustomAttributes<AuthorizeAttribute>(true).Any();

            hasAllowAnonymous.ShouldBeFalse($"{item.Controller.Name}.{item.Action.Name} 同时声明 Console 权限与匿名访问");
            hasAuthorize.ShouldBeTrue($"{item.Controller.Name}.{item.Action.Name} 声明 Console 权限但缺少认证入口");
        }
    }

    [Fact]
    public void PublicDiscoverFeed_ShouldRemainAnonymousAndDisableResponseCaching()
    {
        var action = typeof(PublicDiscoverController).GetMethod(nameof(PublicDiscoverController.GetFeed));

        action.ShouldNotBeNull();
        action.GetCustomAttributes<AllowAnonymousAttribute>(true).ShouldNotBeEmpty();
        var responseCache = action.GetCustomAttribute<ResponseCacheAttribute>(true);
        responseCache.ShouldNotBeNull();
        responseCache.NoStore.ShouldBeTrue();
        responseCache.Location.ShouldBe(ResponseCacheLocation.None);
    }

    [Theory]
    [InlineData("/api/v1/ConsoleAuthorization/GetResourceTree", ConsolePermissions.RolesView)]
    [InlineData("/api/v1/ConsoleAuthorization/SaveRoleAuthorization", ConsolePermissions.RolesEdit)]
    [InlineData("/api/v1/Shop/AdminGetProduct/10001", ConsolePermissions.ProductsView)]
    [InlineData("/api/v1/Shop/AdminGetOrder/10001", ConsolePermissions.OrdersView)]
    [InlineData("/api/v1/Experience/GetUserDailyStats/10001", ConsolePermissions.ExperienceView)]
    [InlineData("/api/v1/Experience/GetUserTransactions/10001", ConsolePermissions.ExperienceView)]
    [InlineData("/api/v1/Wiki/AdminGetById/10001", ConsolePermissions.DocsView)]
    [InlineData("/api/v1/Wiki/UpdateAccessPolicy/10001", ConsolePermissions.DocsPermissions)]
    [InlineData("/api/v1/ContentModeration/GetCaseQueue", ConsolePermissions.ModerationView)]
    [InlineData("/api/v1/ContentModeration/CaptureEvidence", ConsolePermissions.ModerationReview)]
    [InlineData("/api/v1/ContentModeration/GetAppealQueue", ConsolePermissions.ModerationView)]
    [InlineData("/api/v1/ContentModeration/ReviewAppeal", ConsolePermissions.ModerationAppeal)]
    [InlineData("/api/v1/ContentModeration/ExecuteAppealRelief", ConsolePermissions.ModerationAction)]
    [InlineData("/api/v1/ContentModeration/ApplyCorrectiveAction", ConsolePermissions.ModerationAction)]
    [InlineData("/api/v1/ChannelDiscoverability/GetPage", ConsolePermissions.ChannelDiscoverabilityView)]
    [InlineData("/api/v1/ChannelDiscoverability/GetById", ConsolePermissions.ChannelDiscoverabilityView)]
    [InlineData("/api/v1/ChannelDiscoverability/GetHistory", ConsolePermissions.ChannelDiscoverabilityView)]
    [InlineData("/api/v1/ChannelDiscoverability/UpdateVisibility/92900", ConsolePermissions.ChannelDiscoverabilityManage)]
    public void ConsolePermissions_ShouldResolveConsoleApiMappings(string apiUrl, string expectedPermission)
    {
        var permissions = ConsolePermissions.GetPermissionsByApiUrl(apiUrl);

        permissions.ShouldContain(expectedPermission);
    }

    [Theory]
    [InlineData("/api/v1/ContentModeration/GetReviewQueue")]
    [InlineData("/api/v1/ContentModeration/Review")]
    [InlineData("/api/v1/ContentModeration/ApplyUserAction")]
    [InlineData("/api/v1/ContentModeration/GetActionLogs")]
    public void ConsolePermissions_ShouldNotResolveRetiredModerationApiMappings(string apiUrl)
    {
        ConsolePermissions.GetPermissionsByApiUrl(apiUrl).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/api/v1/User/GetById/42", "/api/v1/User/GetById/\\d+", true)]
    [InlineData("/API/V1/User/GetById/42", "/api/v1/user/getbyid/\\d+", true)]
    [InlineData("/api/v1/User/GetById/abc", "/api/v1/User/GetById/\\d+", false)]
    [InlineData("/api/v1/User/GetById/42", "api/v1/User/GetById/\\d+", false)]
    [InlineData("/api/v1/User/GetById/42", "/api/v1/User/GetById/[", false)]
    public void PermissionUrlMatcher_ShouldMatchSafely(string requestPath, string permissionUrl, bool expected)
    {
        PermissionUrlMatcher.IsMatch(requestPath, permissionUrl).ShouldBe(expected);
    }
}
