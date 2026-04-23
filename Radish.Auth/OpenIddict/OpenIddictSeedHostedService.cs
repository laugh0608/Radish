using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using Radish.Common.HttpContextTool;
using System.Text.Json;

namespace Radish.Auth.OpenIddict;

/// <summary>
/// 初始化 OpenIddict 所需的基础数据：
/// - radish-client：官方客户端（Web / Flutter，共享授权码 + refresh_token）
/// - radish-console：后台管理控制台（授权码 + refresh_token）
/// - radish-scalar：API 文档和调试工具（授权码）
/// - radish-api Scope：资源服务器标识
/// </summary>
public class OpenIddictSeedHostedService : IHostedService
{
    private const string OfficialDeveloperName = "Radish 官方";
    private const string ActiveStatus = "Active";
    private const string InternalAppType = "Internal";

    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly IConfiguration _configuration;

    public OpenIddictSeedHostedService(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        IConfiguration configuration)
    {
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[OpenIddictSeed] StartAsync 开始执行");
        var publicBaseUri = GetPublicBaseUri();
        Console.WriteLine($"[OpenIddictSeed] 当前公开回调基址: {publicBaseUri}");

        // 初始化 radish-api Scope
        if (await _scopeManager.FindByNameAsync(UserScopes.RadishApi, cancellationToken) is null)
        {
            Console.WriteLine("[OpenIddictSeed] 创建 radish-api scope");
            var descriptor = new OpenIddictScopeDescriptor
            {
                Name = UserScopes.RadishApi,
                DisplayName = "Radish API"
            };

            descriptor.Resources.Add(UserScopes.RadishApi);

            await _scopeManager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            Console.WriteLine("[OpenIddictSeed] radish-api scope 已存在");
        }

        // 初始化前端 Web 客户端：radish-client
        var existingClient = await _applicationManager.FindByClientIdAsync("radish-client", cancellationToken);
        if (existingClient is null)
        {
            Console.WriteLine("[OpenIddictSeed] 创建 radish-client 客户端");
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "radish-client",
                DisplayName = "Radish Official Client",
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit // SSO: 无需显式授权
            };

            // 通过 Gateway 访问（公开入口）
            descriptor.RedirectUris.Add(BuildPublicUri(publicBaseUri, "oidc/callback"));
            descriptor.PostLogoutRedirectUris.Add(publicBaseUri);
            descriptor.PostLogoutRedirectUris.Add(BuildPublicUri(publicBaseUri, string.Empty));

            // 直接访问前端开发服务器（开发环境）
            descriptor.RedirectUris.Add(new Uri("http://localhost:3000/oidc/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3000"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3000/"));

            // Flutter 原生客户端（Android 起步）
            descriptor.RedirectUris.Add(new Uri("radish://oidc/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("radish://oidc/logout-complete"));

            Console.WriteLine($"[OpenIddictSeed] PostLogoutRedirectUris 数量: {descriptor.PostLogoutRedirectUris.Count}");

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            // 允许 openid/profile/offline_access + radish-api 这几种 scope
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.OpenId);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.Profile);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.OfflineAccess);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.RadishApi);

            // 扩展属性：客户端展示信息
            ApplyOfficialMetadata(descriptor, "Radish 官方客户端，承载社区主站、WebOS 与 Flutter 原生壳层。");

            //descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

            await _applicationManager.CreateAsync(descriptor, cancellationToken);
            Console.WriteLine("[OpenIddictSeed] radish-client 客户端创建完成");
        }
        else
        {
            Console.WriteLine("[OpenIddictSeed] radish-client 客户端已存在，更新配置");
            // 如果客户端已存在，更新配置（确保包含所有访问方式的 URL）
            var descriptor = new OpenIddictApplicationDescriptor();
            await _applicationManager.PopulateAsync(descriptor, existingClient, cancellationToken);

            descriptor.RedirectUris.Clear();
            // 通过 Gateway 访问（公开入口）
            descriptor.RedirectUris.Add(BuildPublicUri(publicBaseUri, "oidc/callback"));
            // 直接访问前端开发服务器（开发环境）
            descriptor.RedirectUris.Add(new Uri("http://localhost:3000/oidc/callback"));
            // Flutter 原生客户端（Android 起步）
            descriptor.RedirectUris.Add(new Uri("radish://oidc/callback"));

            descriptor.PostLogoutRedirectUris.Clear();
            // 通过 Gateway 访问（公开入口）
            descriptor.PostLogoutRedirectUris.Add(publicBaseUri);
            descriptor.PostLogoutRedirectUris.Add(BuildPublicUri(publicBaseUri, string.Empty));
            // 直接访问前端开发服务器（开发环境）
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3000"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3000/"));
            // Flutter 原生客户端（Android 起步）
            descriptor.PostLogoutRedirectUris.Add(new Uri("radish://oidc/logout-complete"));

            Console.WriteLine($"[OpenIddictSeed] 更新后 PostLogoutRedirectUris 数量: {descriptor.PostLogoutRedirectUris.Count}");

            // 确保扩展属性存在
            ApplyOfficialMetadata(descriptor, "Radish 官方客户端，承载社区主站、WebOS 与 Flutter 原生壳层。");

            await _applicationManager.UpdateAsync(existingClient, descriptor, cancellationToken);
            Console.WriteLine("[OpenIddictSeed] radish-client 客户端更新完成");
        }

        // 初始化 Scalar 文档客户端：radish-scalar（用于 /scalar OAuth 调试）
        var existingScalar = await _applicationManager.FindByClientIdAsync("radish-scalar", cancellationToken);
        if (existingScalar is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "radish-scalar",
                DisplayName = "Radish API Documentation",
                ConsentType = OpenIddictConstants.ConsentTypes.Explicit // 需要用户显式授权
            };

            descriptor.RedirectUris.Add(BuildPublicUri(publicBaseUri, "scalar/oauth2-callback"));

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            // 当前 OpenIddict 版本未提供 Scopes.OpenId/Profile 常量，这里直接使用 Scope 前缀 + 字面值
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.OpenId);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.Profile);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.RadishApi);

            // 扩展属性：客户端展示信息
            ApplyOfficialMetadata(descriptor, "Radish 官方 API 文档与调试入口。");

            await _applicationManager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await _applicationManager.PopulateAsync(descriptor, existingScalar, cancellationToken);

            descriptor.RedirectUris.Clear();
            descriptor.RedirectUris.Add(BuildPublicUri(publicBaseUri, "scalar/oauth2-callback"));
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.OpenId);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.Profile);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.RadishApi);
            ApplyOfficialMetadata(descriptor, "Radish 官方 API 文档与调试入口。");

            await _applicationManager.UpdateAsync(existingScalar, descriptor, cancellationToken);
        }

        // 初始化后台管理控制台客户端：radish-console
        var existingConsole = await _applicationManager.FindByClientIdAsync("radish-console", cancellationToken);
        if (existingConsole is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "radish-console",
                DisplayName = "Radish Management Console",
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit // SSO: 无需显式授权
            };

            // 通过 Gateway 访问（公开入口）
            descriptor.RedirectUris.Add(BuildPublicUri(publicBaseUri, "console/callback"));
            descriptor.PostLogoutRedirectUris.Add(BuildPublicUri(publicBaseUri, "console"));
            descriptor.PostLogoutRedirectUris.Add(BuildPublicUri(publicBaseUri, "console/"));

            // 直接访问 console 开发服务器（开发环境，端口 3100）
            descriptor.RedirectUris.Add(new Uri("http://localhost:3100/console/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3100/console"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3100/console/"));

            EnsureConsolePermissions(descriptor);

            // 扩展属性：客户端展示信息
            ApplyOfficialMetadata(descriptor, "Radish 官方管理控制台，面向后台运营与系统管理。");

            await _applicationManager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            // 如果客户端已存在，更新配置（确保包含所有访问方式的 URL）
            var descriptor = new OpenIddictApplicationDescriptor();
            await _applicationManager.PopulateAsync(descriptor, existingConsole, cancellationToken);

            descriptor.RedirectUris.Clear();
            // 通过 Gateway 访问（公开入口）
            descriptor.RedirectUris.Add(BuildPublicUri(publicBaseUri, "console/callback"));
            // 直接访问 console 开发服务器（开发环境，端口 3100）
            descriptor.RedirectUris.Add(new Uri("http://localhost:3100/console/callback"));

            descriptor.PostLogoutRedirectUris.Clear();
            // 通过 Gateway 访问（公开入口）
            descriptor.PostLogoutRedirectUris.Add(BuildPublicUri(publicBaseUri, "console"));
            descriptor.PostLogoutRedirectUris.Add(BuildPublicUri(publicBaseUri, "console/"));
            // 直接访问 console 开发服务器（开发环境，端口 3100）
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3100/console"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3100/console/"));

            // 确保扩展属性存在
            ApplyOfficialMetadata(descriptor, "Radish 官方管理控制台，面向后台运营与系统管理。");
            EnsureConsolePermissions(descriptor);

            await _applicationManager.UpdateAsync(existingConsole, descriptor, cancellationToken);
        }

        var existingShop = await _applicationManager.FindByClientIdAsync("radish-shop", cancellationToken);
        if (existingShop is not null)
        {
            await _applicationManager.DeleteAsync(existingShop, cancellationToken);
            Console.WriteLine("[OpenIddictSeed] 已移除遗留的 radish-shop 客户端种子。");
        }
    }

    private Uri GetPublicBaseUri()
    {
        var issuer = _configuration.GetValue<string>("OpenIddict:Server:Issuer");
        if (Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
        {
            return EnsureTrailingSlash(issuerUri);
        }

        return new Uri("https://localhost:5000/");
    }

    private static Uri BuildPublicUri(Uri baseUri, string relativePath)
    {
        var normalizedPath = relativePath.TrimStart('/');
        return new Uri(EnsureTrailingSlash(baseUri), normalizedPath);
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var uriText = uri.ToString();
        return uriText.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri($"{uriText}/");
    }

    private static void EnsureConsolePermissions(OpenIddictApplicationDescriptor descriptor)
    {
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.OpenId);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.Profile);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.OfflineAccess);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.RadishApi);
    }

    private static void ApplyOfficialMetadata(OpenIddictApplicationDescriptor descriptor, string description)
    {
        descriptor.Properties["description"] = JsonSerializer.SerializeToElement(description);
        descriptor.Properties["developerName"] = JsonSerializer.SerializeToElement(OfficialDeveloperName);
        descriptor.Properties["status"] = JsonSerializer.SerializeToElement(ActiveStatus);
        descriptor.Properties["appType"] = JsonSerializer.SerializeToElement(InternalAppType);
        descriptor.Properties["IsDeleted"] = JsonSerializer.SerializeToElement("false");
        descriptor.Properties.Remove("DeletedAt");
        descriptor.Properties.Remove("DeletedBy");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
