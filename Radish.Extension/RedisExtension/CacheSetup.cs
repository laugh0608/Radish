using Microsoft.Extensions.DependencyInjection;
using Radish.Common.CacheTool;
using Radish.Common.CoreTool;
using Radish.Common.OptionTool;
using StackExchange.Redis;

namespace Radish.Extension.RedisExtension;

public static class CacheSetup
{
    /// <summary>统一注册缓存</summary>
    /// <param name="services"></param>
    public static void AddCacheSetup(this IServiceCollection services)
    {
        var cacheOptions = App.GetOptions<RedisOptions>();
        if (cacheOptions.Enable)
        {
            // 配置启动 Redis 服务，虽然可能影响项目启动速度，但是不能在运行的时候报错，所以是合理的
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                // 获取连接字符串
                var configuration = ConfigurationOptions.Parse(cacheOptions.ConnectionString, true);
                configuration.ResolveDns = true;
                return ConnectionMultiplexer.Connect(configuration);
            });
            services.AddSingleton<ConnectionMultiplexer>(p =>
                p.GetService<IConnectionMultiplexer>() as ConnectionMultiplexer);

            //使用 Redis 缓存
            services.AddStackExchangeRedisCache(options =>
            {
                options.ConnectionMultiplexerFactory =
                    () => Task.FromResult(App.GetService<IConnectionMultiplexer>(false));
                if (!string.IsNullOrEmpty(cacheOptions.InstanceName)) options.InstanceName = cacheOptions.InstanceName;
            });

            services.AddTransient<IRedisBasketRepository, RedisBasketRepository>();
        }
        else
        {
            // 使用内存缓存
            services.AddMemoryCache();
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<ICaching, Caching>();
    }
}