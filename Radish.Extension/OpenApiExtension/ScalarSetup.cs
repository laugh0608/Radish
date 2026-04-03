using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Radish.Common.HttpContextTool;
using Scalar.AspNetCore;

namespace Radish.Extension.OpenApiExtension;

/// <summary>Scalar 和 OpenAPI 启动配置服务</summary>
public static class ScalarSetup
{
    /// <summary>
    /// 添加增强的 OpenAPI 配置，支持 API 版本控制
    /// </summary>
    /// <param name="services">服务集合</param>
    public static void AddScalarSetup(this IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        // 获取 API 版本描述提供者（需要在 AddApiVersioning 之后调用）
        var serviceProvider = services.BuildServiceProvider();
        var versionProvider = serviceProvider.GetRequiredService<IApiVersionDescriptionProvider>();

        // 为每个 API 版本动态创建 OpenAPI 文档
        foreach (var description in versionProvider.ApiVersionDescriptions)
        {
            var versionName = description.GroupName; // 例如：v1, v2

            services.AddOpenApi(versionName, options =>
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    var version = description.ApiVersion.ToString(); // 例如：1.0, 2.0
                    var isDeprecated = description.IsDeprecated;

                    // 设置文档标题和版本
                    document.Info.Title = $"Radish API Documentation - {versionName.ToUpper()}";
                    document.Info.Version = version;

                    // 根据版本设置不同的描述
                    if (versionName == "v1")
                    {
                        document.Info.Description = BuildV1Description(isDeprecated);
                    }
                    else if (versionName == "v2")
                    {
                        document.Info.Description = BuildV2Description();
                    }
                    else
                    {
                        document.Info.Description = $"Radish API 文档 - 版本 {version}";
                    }

                    // 如果版本已弃用，添加警告标记
                    if (isDeprecated)
                    {
                        document.Info.Description = $"⚠️ **此版本已弃用**\n\n{document.Info.Description}";
                    }

                    // 清空默认服务器列表，添加自定义服务器
                    document.Servers.Clear();
                    document.Servers.Add(new()
                    {
                        Url = "https://localhost:5000",
                        Description = "本地开发环境 (Gateway HTTPS)"
                    });
                    document.Servers.Add(new()
                    {
                        Url = "http://localhost:5001",
                        Description = "本地开发环境 (Gateway HTTP)"
                    });
                    document.Servers.Add(new()
                    {
                        Url = "http://localhost:5100",
                        Description = "本地开发环境 (API 直连)"
                    });

                    // 添加 OAuth2 Security Scheme（用于 Scalar OIDC 登录）
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes["oauth2"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.OAuth2,
                        Description = "通过 OIDC 认证服务器获取 Access Token",
                        Flows = new OpenApiOAuthFlows
                        {
                            AuthorizationCode = new OpenApiOAuthFlow
                            {
                                // 通过 Gateway 代理的 Auth 端点
                                AuthorizationUrl = new Uri("https://localhost:5000/connect/authorize"),
                                TokenUrl = new Uri("https://localhost:5000/connect/token"),
                                Scopes = new Dictionary<string, string>
                                {
                                    [UserScopes.OpenId] = "OpenID Connect 身份认证",
                                    [UserScopes.Profile] = "用户基本信息",
                                    [UserScopes.RadishApi] = "Radish API 访问权限"
                                }
                            }
                        }
                    };

                    return Task.CompletedTask;
                });
            });
        }
    }

    /// <summary>
    /// 映射 Scalar UI 到指定路径
    /// </summary>
    /// <param name="app">应用构建器</param>
    /// <param name="routePrefix">路由前缀，默认 /scalar</param>
    public static void UseScalarUI(this WebApplication app, string routePrefix = "/scalar")
    {
        // 映射 OpenAPI 文档端点
        app.MapOpenApi();

        // 获取 API 版本描述提供者
        var versionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        var versions = versionProvider.ApiVersionDescriptions
            .OrderBy(v => v.ApiVersion)
            .ToList();

        // 配置 Scalar UI
        app.MapScalarApiReference(routePrefix, options =>
        {
            options.WithTitle("Radish API Documentation")
                // 统一主题/外观
                .WithTheme(ScalarTheme.BluePlanet)
                .ForceDarkMode()
                .HideDarkModeToggle()
                .HideClientButton()
                // 显示操作 ID，便于调试
                .ShowOperationId()
                // 展开所有标签，方便查看
                .ExpandAllTags()
                // 按字母顺序排序标签
                .SortTagsAlphabetically()
                // 保留 Schema 属性顺序
                .PreserveSchemaPropertyOrder()
                // 设置默认 HTTP 客户端为 Axios
                .WithDefaultHttpClient(ScalarTarget.Node, ScalarClient.Axios)
                // 配置 OAuth2/OIDC 认证（启用 Authorize 按钮）
                .AddPreferredSecuritySchemes("oauth2")
                .AddOAuth2Flows("oauth2", flows =>
                {
                    flows.AuthorizationCode = new AuthorizationCodeFlow
                    {
                        ClientId = "radish-scalar",
                        RedirectUri = "https://localhost:5000/scalar/oauth2-callback"
                    };
                })
                .AddDefaultScopes("oauth2", [UserScopes.OpenId, UserScopes.Profile, UserScopes.RadishApi]);

            // 动态配置多版本文档
            for (var i = 0; i < versions.Count; i++)
            {
                var description = versions[i];
                var versionName = description.GroupName;
                var displayName = description.IsDeprecated
                    ? $"{versionName.ToUpper()} (已弃用)"
                    : versionName.ToUpper();

                // 第一个版本设为默认
                var isDefault = i == 0;

                options.AddDocument(
                    versionName,
                    displayName,
                    $"/openapi/{versionName}.json",
                    isDefault
                );
            }
        });
    }

    /// <summary>
    /// 构建 v1 版本的文档描述
    /// </summary>
    private static string BuildV1Description(bool isDeprecated)
    {
        var statusText = isDeprecated ? "**已弃用** - 请迁移到更高版本" : "**当前稳定版本**";

        return $@"
## Radish 社区平台 API 文档 - V1

{statusText}

### 📋 包含的接口
- **认证管理**: 用户登录、Token 获取
- **用户管理**: 用户信息查询、修改

### 🔐 认证方式
使用 JWT Bearer Token 认证，在请求头中添加：
```
Authorization: Bearer {{your_token}}
```

### 🔑 获取 Token

**方式一：OIDC 认证（推荐）**
1. 点击右上角 **Authenticate** 按钮
2. 选择 **oauth2** 认证方式
3. 点击 **Authorize** 跳转到登录页面
4. 使用测试账号登录：
   - 用户名：`test`
   - 密码：`test123456`
5. 授权后自动返回，所有请求将自动携带 Token

**方式二：传统 API 登录**
调用 `GET /api/v1/Login/GetJwtToken` 接口，传入用户名和密码获取 Token，然后手动添加到请求头。

### 📊 常见状态码
- `200`: 请求成功
- `401`: 未授权，Token 无效或过期
- `403`: 禁止访问，权限不足
- `404`: 资源不存在
- `500`: 服务器内部错误

### 📦 统一响应格式
所有接口返回统一的 MessageModel 格式：
```json
{{
  ""statusCode"": 200,
  ""isSuccess"": true,
  ""messageInfo"": ""操作成功"",
  ""messageInfoDev"": ""开发者调试信息"",
  ""responseData"": {{}}
}}
```
";
    }

    /// <summary>
    /// 构建 v2 版本的文档描述
    /// </summary>
    private static string BuildV2Description()
    {
        return @"
## Radish 社区平台 API 文档 - V2

**新功能预览版本**

### 📋 包含的接口
- **系统管理**: 应用配置查询、系统信息获取
- **性能测试**: C# 与 Rust 原生库性能对比

### 🆕 新特性
- **应用配置管理**: 提供多种配置读取方式演示
- **Rust 互操作**: 跨语言性能测试接口
- **版本控制**: URL 路径版本控制示例

### 🔐 认证方式
与 v1 版本相同，使用 JWT Bearer Token 认证。

### 🚀 URL 格式
所有 v2 接口 URL 格式为：`/api/v2/{Controller}/{Action}`

例如：
- `/api/v2/AppSetting/GetRedisConfig`
- `/api/v2/RustTest/TestSum1`

### ⚙️ 版本迁移
从 v1 迁移到 v2 只需修改 URL 路径中的版本号即可。
";
    }
}
