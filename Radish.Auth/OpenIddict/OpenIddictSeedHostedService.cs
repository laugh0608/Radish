using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;

namespace Radish.Auth.OpenIddict;

/// <summary>
/// 初始化 OpenIddict 所需的基础数据：
/// - radish-client：前端 Web 客户端（授权码 + PKCE + refresh_token）
/// - radish-api Scope：资源服务器标识
/// </summary>
public class OpenIddictSeedHostedService : IHostedService
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;

    public OpenIddictSeedHostedService(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager)
    {
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 初始化 radish-api Scope
        if (await _scopeManager.FindByNameAsync("radish-api", cancellationToken) is null)
        {
            var descriptor = new OpenIddictScopeDescriptor
            {
                Name = "radish-api",
                DisplayName = "Radish API"
            };

            descriptor.Resources.Add("radish-api");

            await _scopeManager.CreateAsync(descriptor, cancellationToken);
        }

        // 初始化前端 Web 客户端：radish-client
        if (await _applicationManager.FindByClientIdAsync("radish-client", cancellationToken) is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "radish-client",
                DisplayName = "Radish Web Client",
                ConsentType = OpenIddictConstants.ConsentTypes.Explicit
            };

            // 通过 Gateway 暴露的回调地址
            descriptor.RedirectUris.Add(new Uri("https://localhost:5000/oidc/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000"));

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            // 允许 openid/profile/offline_access + radish-api 这几种 scope
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "openid");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "profile");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "radish-api");

            //descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

            await _applicationManager.CreateAsync(descriptor, cancellationToken);
        }

        // 初始化 Scalar 文档客户端：radish-scalar（用于 /scalar OAuth 调试）
        if (await _applicationManager.FindByClientIdAsync("radish-scalar", cancellationToken) is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "radish-scalar",
                DisplayName = "Radish API Documentation",
                ConsentType = OpenIddictConstants.ConsentTypes.Explicit // 需要用户显式授权
            };

            descriptor.RedirectUris.Add(new Uri("https://localhost:5000/scalar/oauth2-callback"));

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            // 当前 OpenIddict 版本未提供 Scopes.OpenId/Profile 常量，这里直接使用 Scope 前缀 + 字面值
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "openid");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "profile");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "radish-api");

            await _applicationManager.CreateAsync(descriptor, cancellationToken);
        }

        // 初始化后台/服务端客户端：radish-rust-ext（示例：Rust 扩展或后台任务）
        if (await _applicationManager.FindByClientIdAsync("radish-rust-ext", cancellationToken) is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "radish-rust-ext",
                ClientSecret = "radish-rust-ext-dev-secret", // 本地开发示例，生产请使用强随机密钥并通过配置管理
                DisplayName = "Radish Rust Extension",
            };

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "radish-api");

            await _applicationManager.CreateAsync(descriptor, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
