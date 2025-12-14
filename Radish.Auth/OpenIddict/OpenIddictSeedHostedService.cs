using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;

namespace Radish.Auth.OpenIddict;

/// <summary>
/// 初始化 OpenIddict 所需的基础数据：
/// - radish-client：前端 Web 客户端（授权码 + PKCE + refresh_token）
/// - radish-console：后台管理控制台（授权码 + refresh_token）
/// - radish-scalar：API 文档和调试工具（授权码）
/// - radish-shop：商城应用（授权码 + refresh_token，占位）
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
        Console.WriteLine("[OpenIddictSeed] StartAsync 开始执行");

        // 初始化 radish-api Scope
        if (await _scopeManager.FindByNameAsync("radish-api", cancellationToken) is null)
        {
            Console.WriteLine("[OpenIddictSeed] 创建 radish-api scope");
            var descriptor = new OpenIddictScopeDescriptor
            {
                Name = "radish-api",
                DisplayName = "Radish API"
            };

            descriptor.Resources.Add("radish-api");

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
                DisplayName = "Radish Web Client",
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit // SSO: 无需显式授权
            };

            // 通过 Gateway 访问（生产环境）
            descriptor.RedirectUris.Add(new Uri("https://localhost:5000/oidc/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/"));

            // 直接访问前端开发服务器（开发环境）
            descriptor.RedirectUris.Add(new Uri("http://localhost:3000/oidc/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3000"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3000/"));

            Console.WriteLine($"[OpenIddictSeed] PostLogoutRedirectUris 数量: {descriptor.PostLogoutRedirectUris.Count}");

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
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
            Console.WriteLine("[OpenIddictSeed] radish-client 客户端创建完成");
        }
        else
        {
            Console.WriteLine("[OpenIddictSeed] radish-client 客户端已存在，更新配置");
            // 如果客户端已存在，更新配置（确保包含所有访问方式的 URL）
            var descriptor = new OpenIddictApplicationDescriptor();
            await _applicationManager.PopulateAsync(descriptor, existingClient, cancellationToken);

            descriptor.RedirectUris.Clear();
            // 通过 Gateway 访问（生产环境）
            descriptor.RedirectUris.Add(new Uri("https://localhost:5000/oidc/callback"));
            // 直接访问前端开发服务器（开发环境）
            descriptor.RedirectUris.Add(new Uri("http://localhost:3000/oidc/callback"));

            descriptor.PostLogoutRedirectUris.Clear();
            // 通过 Gateway 访问（生产环境）
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/"));
            // 直接访问前端开发服务器（开发环境）
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3000"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3000/"));

            Console.WriteLine($"[OpenIddictSeed] 更新后 PostLogoutRedirectUris 数量: {descriptor.PostLogoutRedirectUris.Count}");

            // 确保扩展属性存在
            descriptor.Properties["description"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish 社区平台前端应用");
            descriptor.Properties["developerName"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish Team");

            await _applicationManager.UpdateAsync(existingClient, descriptor, cancellationToken);
            Console.WriteLine("[OpenIddictSeed] radish-client 客户端更新完成");
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
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
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

            // 通过 Gateway 访问（生产环境）
            descriptor.RedirectUris.Add(new Uri("https://localhost:5000/console/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/console"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/console/"));

            // 直接访问 console 开发服务器（开发环境）
            descriptor.RedirectUris.Add(new Uri("http://localhost:3002/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3002"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3002/"));

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
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
            // 如果客户端已存在，更新配置（确保包含所有访问方式的 URL）
            var descriptor = new OpenIddictApplicationDescriptor();
            await _applicationManager.PopulateAsync(descriptor, existingConsole, cancellationToken);

            descriptor.RedirectUris.Clear();
            // 通过 Gateway 访问（生产环境）
            descriptor.RedirectUris.Add(new Uri("https://localhost:5000/console/callback"));
            // 直接访问 console 开发服务器（开发环境）
            descriptor.RedirectUris.Add(new Uri("http://localhost:3002/callback"));

            descriptor.PostLogoutRedirectUris.Clear();
            // 通过 Gateway 访问（生产环境）
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/console"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/console/"));
            // 直接访问 console 开发服务器（开发环境）
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3002"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3002/"));

            // 确保扩展属性存在
            descriptor.Properties["description"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish 后台管理控制台");
            descriptor.Properties["developerName"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish Team");

            await _applicationManager.UpdateAsync(existingConsole, descriptor, cancellationToken);
        }

        // 初始化商城客户端：radish-shop（占位，未来实现）
        var existingShop = await _applicationManager.FindByClientIdAsync("radish-shop", cancellationToken);
        if (existingShop is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "radish-shop",
                DisplayName = "Radish Shop",
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit // SSO: 无需显式授权
            };

            // 通过 Gateway 访问（生产环境）
            descriptor.RedirectUris.Add(new Uri("https://localhost:5000/shop/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/shop"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/shop/"));

            // 直接访问 shop 开发服务器（开发环境，预留端口 3003）
            descriptor.RedirectUris.Add(new Uri("http://localhost:3003/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3003"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3003/"));

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "openid");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "profile");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "radish-api");

            // 扩展属性：客户端展示信息
            descriptor.Properties["description"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish 商城应用（占位，未来实现）");
            descriptor.Properties["developerName"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish Team");

            await _applicationManager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            // 如果客户端已存在，更新配置（确保包含所有访问方式的 URL）
            var descriptor = new OpenIddictApplicationDescriptor();
            await _applicationManager.PopulateAsync(descriptor, existingShop, cancellationToken);

            descriptor.RedirectUris.Clear();
            // 通过 Gateway 访问（生产环境）
            descriptor.RedirectUris.Add(new Uri("https://localhost:5000/shop/callback"));
            // 直接访问 shop 开发服务器（开发环境，预留端口 3003）
            descriptor.RedirectUris.Add(new Uri("http://localhost:3003/callback"));

            descriptor.PostLogoutRedirectUris.Clear();
            // 通过 Gateway 访问（生产环境）
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/shop"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5000/shop/"));
            // 直接访问 shop 开发服务器（开发环境，预留端口 3003）
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3003"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("http://localhost:3003/"));

            // 确保扩展属性存在
            descriptor.Properties["description"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish 商城应用（占位，未来实现）");
            descriptor.Properties["developerName"] = System.Text.Json.JsonSerializer.SerializeToElement("Radish Team");

            await _applicationManager.UpdateAsync(existingShop, descriptor, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
