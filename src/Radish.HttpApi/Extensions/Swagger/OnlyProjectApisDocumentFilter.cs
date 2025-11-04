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

    // 明确允许显示的前缀（优先级最高），用于覆盖 DropPrefixes 中的某些 ABP 端点
    private static readonly string[] AllowPrefixes =
    [
        "/api/abp/application-configuration", // ABP 应用配置（包含 OIDC 配置）
        "/api/abp/application-localization",  // ABP 语言/本地化配置
        "/connect",                            // OIDC 相关端点（如 connect/token 等），一般不会进 Swagger，但兜底放行
        "/.well-known"                         // OIDC 元数据端点，兜底放行
    ];

    private static readonly string[] DropPrefixes =
    [
        "/api/abp",
        "/api/identity",
        "/api/account",
        "/api/tenant-management",
        "/api/feature-management",
        "/api/permission-management",
        "/api/setting-management"
    ];

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        static bool StartsWith(string path, string prefix)
            => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        bool IsAllowed(string path)
            => AllowPrefixes.Any(p => StartsWith(path, p))
               || KeepPrefixes.Any(p => StartsWith(path, p));

        bool ShouldRemove(string path)
        {
            // 1) 显式允许优先：命中则保留
            if (AllowPrefixes.Any(p => StartsWith(path, p))) return false;

            // 2) 命中黑名单：移除（除非已在 1) 放行）
            if (DropPrefixes.Any(p => StartsWith(path, p))) return true;

            // 3) 命中白名单：保留
            if (KeepPrefixes.Any(p => StartsWith(path, p))) return false;

            // 4) 其他未识别路径：移除，保持文档干净
            return true;
        }

        var toRemove = swaggerDoc.Paths
            .Where(kv => ShouldRemove(kv.Key))
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

        // 注意：不再移除 ABP 的 schemas，以避免引用缺失（如 RemoteServiceErrorInfo）。
        // 如果未来需要精简，可在确保“引用完整收集”的前提下再开启。
    }
}
