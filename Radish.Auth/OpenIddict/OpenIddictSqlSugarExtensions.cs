using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Radish.Auth.OpenIddict.Stores;
using Radish.Model.OpenIddict;

namespace Radish.Auth.OpenIddict;

/// <summary>
/// OpenIddict 集成 SqlSugar 的扩展。
/// 这里只做最小注册，具体 Store 内部逻辑后续逐步完善。
/// </summary>
public static class OpenIddictSqlSugarExtensions
{
    public static void UseRadishSqlSugarStores(this OpenIddictCoreBuilder builder)
    {
        // 应用（客户端）
        builder.Services.AddScoped<IOpenIddictApplicationStore<RadishApplication>, RadishApplicationStore>();

        // 授权记录
        builder.Services.AddScoped<IOpenIddictAuthorizationStore<RadishAuthorization>, RadishAuthorizationStore>();

        // 作用域
        builder.Services.AddScoped<IOpenIddictScopeStore<RadishScope>, RadishScopeStore>();

        // Token
        builder.Services.AddScoped<IOpenIddictTokenStore<RadishToken>, RadishTokenStore>();
    }
}
