using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Radish.Extension.AuditLogExtension;

/// <summary>
/// 审计日志扩展方法
/// </summary>
public static class AuditLogSetup
{
    /// <summary>
    /// 注册审计日志服务
    /// </summary>
    public static IServiceCollection AddAuditLogSetup(this IServiceCollection services, AuditLogOptions? options = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        // 注册配置选项
        if (options != null)
        {
            services.AddSingleton(options);
        }
        else
        {
            // 从配置文件读取
            services.AddSingleton(sp =>
            {
                var configuration = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
                var auditLogOptions = new AuditLogOptions();
                configuration.GetSection("AuditLog").Bind(auditLogOptions);
                return auditLogOptions;
            });
        }

        return services;
    }

    /// <summary>
    /// 使用审计日志中间件
    /// </summary>
    /// <remarks>
    /// 应该在认证授权中间件之后、路由中间件之前调用
    /// </remarks>
    public static IApplicationBuilder UseAuditLogSetup(this IApplicationBuilder app)
    {
        if (app == null) throw new ArgumentNullException(nameof(app));

        var options = app.ApplicationServices.GetService<AuditLogOptions>();
        if (options == null || !options.Enable)
        {
            return app;
        }

        app.UseMiddleware<AuditLogMiddleware>(options);

        return app;
    }
}
