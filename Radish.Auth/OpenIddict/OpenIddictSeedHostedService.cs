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
        var existingClient = await _applicationManager.FindByClientIdAsync("radish-client", cancellationToken);
        if (existingClient is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "radish-client",
                DisplayName = "Radish Web Client",
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit // SSO: 无需显式授权
            };

            // 开发环境：前端直接访问（Vite dev server）
            descriptor.RedirectUris.Add(new Uri("http://localhost:3000/oidc/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3000"));

            // 生产环境：通过 Gateway 访问
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

            // 扩展属性：客户端展示信息
            descriptor.Properties["description"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish 社区平台前端应用");
            descriptor.Properties["developerName"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish Team");

            //descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

            await _applicationManager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            // 如果客户端已存在，更新 redirect_uri 配置（确保包含开发和生产环境的地址）
            var descriptor = new OpenIddictApplicationDescriptor();
            await _applicationManager.PopulateAsync(descriptor, existingClient, cancellationToken);

            descriptor.RedirectUris.Clear();
            descriptor.RedirectUris.Add(new Uri("http://localhost:3000/oidc/callback"));
            descriptor.RedirectUris.Add(new Uri("https://localhost:5000/oidc/callback"));

            descriptor.PostLogoutRedirectUris.Clear();
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3000"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000"));

            // 确保扩展属性存在
            descriptor.Properties["description"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish 社区平台前端应用");
            descriptor.Properties["developerName"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish Team");

            await _applicationManager.UpdateAsync(existingClient, descriptor, cancellationToken);
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

            // 扩展属性：客户端展示信息
            descriptor.Properties["description"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish API 文档和调试工具");
            descriptor.Properties["developerName"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish Team");

            await _applicationManager.CreateAsync(descriptor, cancellationToken);
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

            // 开发环境：Console 直接访问
            descriptor.RedirectUris.Add(new Uri("http://localhost:3002/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3002"));

            // 生产环境：通过 Gateway 访问
            descriptor.RedirectUris.Add(new Uri("https://localhost:5000/console/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/console"));

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "openid");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "profile");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "radish-api");

            // 扩展属性：客户端展示信息
            descriptor.Properties["description"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish 后台管理控制台");
            descriptor.Properties["developerName"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish Team");

            await _applicationManager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            // 如果客户端已存在，更新 redirect_uri 配置
            var descriptor = new OpenIddictApplicationDescriptor();
            await _applicationManager.PopulateAsync(descriptor, existingConsole, cancellationToken);

            descriptor.RedirectUris.Clear();
            descriptor.RedirectUris.Add(new Uri("http://localhost:3002/callback"));
            descriptor.RedirectUris.Add(new Uri("https://localhost:5000/console/callback"));

            descriptor.PostLogoutRedirectUris.Clear();
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3002"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/console"));

            // 确保扩展属性存在
            descriptor.Properties["description"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish 后台管理控制台");
            descriptor.Properties["developerName"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish Team");

            await _applicationManager.UpdateAsync(existingConsole, descriptor, cancellationToken);
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

            // 扩展属性：客户端展示信息
            descriptor.Properties["description"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish 后台服务和 Rust 原生扩展");
            descriptor.Properties["developerName"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish Team");

            await _applicationManager.CreateAsync(descriptor, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
