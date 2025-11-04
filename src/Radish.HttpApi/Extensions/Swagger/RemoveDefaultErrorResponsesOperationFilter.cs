using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Radish.Extensions.Swagger;

/// <summary>
/// 全局移除 Swagger 中默认的错误响应（如 ABP 自动添加的 400/401/403/404/500/501）。
/// </summary>
public class RemoveDefaultErrorResponsesOperationFilter : IOperationFilter
{
    private static readonly string[] CodesToRemove =
    {
        "400", "401", "403", "404", "500", "501", "default"
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var code in CodesToRemove)
        {
            if (operation.Responses.ContainsKey(code))
            {
                operation.Responses.Remove(code);
            }
        }
    }
}

