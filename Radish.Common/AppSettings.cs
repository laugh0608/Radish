using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Radish.Common;

/// <summary>appsettings.json 操作类</summary>
public class AppSettings
{
    // 需要引用 Microsoft.Extensions.Configuration.Binder 和 Microsoft.Extensions.Configuration.Json 包
    public static IConfiguration Configuration { get; set; }
    
    // static string ContentPath { get; set; }

    public AppSettings(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public AppSettings(string contentPath)
    {
        const string path = "appsettings.json";

        // 如果把配置文件根据环境变量来分开了，可以这样写
        // Path = $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json";

        Configuration = new ConfigurationBuilder()
            .SetBasePath(contentPath)
            .Add(new JsonConfigurationSource
            {
                Path = path, Optional = false, ReloadOnChange = true
            }) // 这样的话，可以直接读目录里的 json 文件，而不是 bin 文件夹下的，所以不用修改复制属性
            .Build();
    }

    /// <summary>封装要操作的字符，数组类型</summary>
    /// <param name="sections">节点配置</param>
    /// <returns></returns>
    public static string RadishApp(params string[] sections)
    {
        try
        {
            if (sections.Any())
            {
                return Configuration[string.Join(":", sections)];
            }
        }
        catch (Exception)
        {
            throw new Exception("Invalid configuration Failed");
        }

        return "";
    }

    /// <summary>递归获取配置信息数组</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sections"></param>
    /// <returns></returns>
    public static List<T> RadishApp<T>(params string[] sections)
    {
        List<T> list = new List<T>();
        Configuration.Bind(string.Join(":", sections), list);
        return list;
    }


    /// <summary>根据路径</summary>
    /// <remarks>configuration["Redis:ConnectionString"]</remarks>
    /// <param name="sectionsPath">configuration["Redis:ConnectionString"]</param>
    /// <returns></returns>
    public static string GetValue(string sectionsPath)
    {
        try
        {
            return Configuration[sectionsPath];
        }
        catch (Exception)
        {
            // throw new Exception("Invalid configuration Failed");
        }

        return "";
    }
}