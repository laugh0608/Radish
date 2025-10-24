using System;
using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Radish.Extensions.Swagger;

public class OnlyProjectApisDocumentFilter : IDocumentFilter
{
    private static readonly string[] KeepPrefixes =
    [
        "/api/v",      // 版本化自定义控制器，如 /api/v1/...
        "/api/app",    // ABP 动态应用服务 (本项目)
        "/api/radish"  // 如有自定义前缀
    ];

    private static readonly string[] DropPrefixes =
    [
        "/api/abp",
        "/api/identity",
        "/api/account",
        "/api/tenant-management",
        "/api/feature-management",
        "/api/permission-management",
        "/api/setting-management",
        "/connect" // OpenIddict/OIDC 端点
    ];

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var toRemove = swaggerDoc.Paths
            .Where(kv =>
                DropPrefixes.Any(p => kv.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase)) ||
               !KeepPrefixes.Any(p => kv.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var path in toRemove)
        {
            swaggerDoc.Paths.Remove(path);
        }

        // 可选：清理未使用的标签
        if (swaggerDoc.Tags is { Count: > 0 })
        {
            var used = swaggerDoc.Paths
                .SelectMany(p => p.Value.Operations.Values)
                .SelectMany(op => op.Tags?.Select(t => t.Name) ?? Enumerable.Empty<string>())
                .Distinct()
                .ToHashSet(StringComparer.Ordinal);

            var tagsToRemove = swaggerDoc.Tags
                .Where(t => !used.Contains(t.Name))
                .ToList();

            foreach (var tag in tagsToRemove)
            {
                swaggerDoc.Tags.Remove(tag);
            }
        }
    }
}

