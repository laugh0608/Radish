using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

namespace Radish.Extension.OpenApiExtension;

/// <summary>Scalar 和 OpenAPI 启动配置服务</summary>
public static class ScalarSetup
{
    /// <summary>
    /// 添加增强的 OpenAPI 配置
    /// </summary>
    /// <param name="services">服务集合</param>
    public static void AddScalarSetup(this IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        // 配置 OpenAPI v1 文档
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = "Radish API Documentation";
                document.Info.Version = "v1.0";
                document.Info.Description = @"
## Radish 社区平台 API 文档

### 认证方式
使用 JWT Bearer Token 认证，在请求头中添加：
```
Authorization: Bearer {your_token}
```

### 获取 Token
调用 `GET /api/Login/GetJwtToken` 接口，传入用户名和密码获取 Token。

### 常见状态码
- `200`: 请求成功
- `401`: 未授权，Token 无效或过期
- `403`: 禁止访问，权限不足
- `500`: 服务器内部错误

### 统一响应格式
所有接口返回统一的 MessageModel 格式：
```json
{
  ""statusCode"": 200,
  ""isSuccess"": true,
  ""messageInfo"": ""操作成功"",
  ""messageInfoDev"": ""开发者调试信息"",
  ""responseData"": {}
}
```
";

                // 添加服务器列表
                document.Servers.Add(new()
                {
                    Url = "https://localhost:7110",
                    Description = "本地开发环境 (HTTPS)"
                });
                document.Servers.Add(new()
                {
                    Url = "http://localhost:5165",
                    Description = "本地开发环境 (HTTP)"
                });

                return Task.CompletedTask;
            });
        });

        // 配置 OpenAPI v2 文档（预留）
        services.AddOpenApi("v2", options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = "Radish API Documentation (v2 - Preview)";
                document.Info.Version = "v2.0-preview";
                document.Info.Description = "第二版 API 文档，用于新功能预览和向后兼容";
                return Task.CompletedTask;
            });
        });
    }

    /// <summary>
    /// 映射 Scalar UI 到指定路径
    /// </summary>
    /// <param name="app">应用构建器</param>
    /// <param name="routePrefix">路由前缀，默认 /api/docs</param>
    public static void UseScalarUI(this WebApplication app, string routePrefix = "/api/docs")
    {
        // 映射 OpenAPI 文档端点
        app.MapOpenApi();

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
                .WithDefaultHttpClient(ScalarTarget.Node, ScalarClient.Axios);

            // 配置多版本文档
            options
                .AddDocument("v1", "v1 (当前版本)", "/openapi/v1.json", isDefault: true)
                .AddDocument("v2", "v2 (预览版)", "/openapi/v2.json");
        });
    }
}
