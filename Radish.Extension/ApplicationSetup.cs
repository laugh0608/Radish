using Microsoft.AspNetCore.Builder;
using Radish.Common.CoreTool;
using Serilog;

namespace Radish.Extension;

/// <summary>启动配置的扩展 App</summary>
public static class ApplicationSetup
{
    public static void UseApplicationSetup(this WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            App.IsRun = true;
        });

        app.Lifetime.ApplicationStopped.Register(() =>
        {
            App.IsRun = false;

            // 清除日志
            Log.CloseAndFlush();
        });
    }
}