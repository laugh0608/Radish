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
        "/api/setting-management"
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

        // 隐藏未被引用的 ABP Schemas（保守策略：仅移除未被任何操作引用的 ABP 类型）
        if (swaggerDoc.Components?.Schemas is { Count: > 0 })
        {
            var referenced = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

            void Collect(OpenApiSchema? schema)
            {
                if (schema is null) return;
                if (schema.Reference?.Id is { } id)
                {
                    // 仅在 components/schemas 下的引用才计入（更安全）
                    if (schema.Reference.Type == ReferenceType.Schema)
                    {
                        if (referenced.Add(id))
                        {
                            // 递归展开：AllOf/AnyOf/OneOf/Items/AdditionalProperties/Properties
                            foreach (var s in schema.AllOf) Collect(s);
                            foreach (var s in schema.AnyOf) Collect(s);
                            foreach (var s in schema.OneOf) Collect(s);
                            if (schema.Items != null) Collect(schema.Items);
                            if (schema.AdditionalProperties != null) Collect(schema.AdditionalProperties);
                            foreach (var p in schema.Properties.Values) Collect(p);
                        }
                    }
                    return;
                }

                foreach (var s in schema.AllOf) Collect(s);
                foreach (var s in schema.AnyOf) Collect(s);
                foreach (var s in schema.OneOf) Collect(s);
                if (schema.Items != null) Collect(schema.Items);
                if (schema.AdditionalProperties != null) Collect(schema.AdditionalProperties);
                foreach (var p in schema.Properties.Values) Collect(p);
            }

            foreach (var path in swaggerDoc.Paths.Values)
            {
                foreach (var op in path.Operations.Values)
                {
                    // 请求体
                    var rb = op.RequestBody;
                    if (rb?.Content != null)
                    {
                        foreach (var c in rb.Content.Values)
                        {
                            Collect(c.Schema);
                        }
                    }

                    // 响应体
                    foreach (var resp in op.Responses.Values)
                    {
                        if (resp.Content == null) continue;
                        foreach (var c in resp.Content.Values)
                        {
                            Collect(c.Schema);
                        }
                    }

                    // 参数（尽量完整，但通常为原生类型）
                    if (op.Parameters != null)
                    {
                        foreach (var p in op.Parameters)
                        {
                            Collect(p.Schema);
                        }
                    }
                }
            }

            static bool IsAbpSchema(string name)
                => name.StartsWith("Volo.Abp", StringComparison.Ordinal);

            var removeSchemas = swaggerDoc.Components.Schemas
                .Where(kv => IsAbpSchema(kv.Key) && !referenced.Contains(kv.Key))
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in removeSchemas)
            {
                swaggerDoc.Components.Schemas.Remove(key);
            }
        }
    }
}
