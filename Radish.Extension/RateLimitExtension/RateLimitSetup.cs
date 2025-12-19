using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common.CoreTool;
using Radish.Common.OptionTool;
using Serilog;

namespace Radish.Extension.RateLimitExtension;

/// <summary>速率限制扩展</summary>
public static class RateLimitSetup
{
    /// <summary>策略名称常量</summary>
    public static class PolicyNames
    {
        public const string Global = "global";
        public const string Login = "login";
        public const string Sensitive = "sensitive";
        public const string Concurrency = "concurrency";
    }

    /// <summary>添加速率限制服务</summary>
    public static void AddRateLimitSetup(this IServiceCollection services)
    {
        var options = App.GetOptions<RateLimitOptions>();

        if (!options.Enable)
        {
            Log.Information("[RateLimit] 速率限制已禁用");
            return;
        }

        services.AddRateLimiter(limiterOptions =>
        {
            // 配置拒绝响应
            limiterOptions.OnRejected = async (context, cancellationToken) =>
            {
                await HandleRateLimitRejection(context, options, cancellationToken);
            };

            // 全局限流策略（Fixed Window）
            if (options.Global.Enable)
            {
                limiterOptions.AddFixedWindowLimiter(PolicyNames.Global, fixedOptions =>
                {
                    fixedOptions.PermitLimit = options.Global.PermitLimit;
                    fixedOptions.Window = TimeSpan.FromSeconds(options.Global.WindowSeconds);
                    fixedOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    fixedOptions.QueueLimit = 0; // 不排队，直接拒绝
                });

                Log.Information("[RateLimit] 全局限流策略已启用: {PermitLimit} 请求/{WindowSeconds}秒",
                    options.Global.PermitLimit, options.Global.WindowSeconds);
            }

            // 登录限流策略（Sliding Window）
            if (options.Login.Enable)
            {
                limiterOptions.AddSlidingWindowLimiter(PolicyNames.Login, slidingOptions =>
                {
                    slidingOptions.PermitLimit = options.Login.PermitLimit;
                    slidingOptions.Window = TimeSpan.FromSeconds(options.Login.WindowSeconds);
                    slidingOptions.SegmentsPerWindow = options.Login.SegmentsPerWindow;
                    slidingOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    slidingOptions.QueueLimit = 0;
                });

                Log.Information("[RateLimit] 登录限流策略已启用: {PermitLimit} 请求/{WindowSeconds}秒",
                    options.Login.PermitLimit, options.Login.WindowSeconds);
            }

            // 敏感操作限流策略（Token Bucket）
            if (options.Sensitive.Enable)
            {
                limiterOptions.AddTokenBucketLimiter(PolicyNames.Sensitive, tokenOptions =>
                {
                    tokenOptions.TokenLimit = options.Sensitive.TokenLimit;
                    tokenOptions.TokensPerPeriod = options.Sensitive.TokensPerPeriod;
                    tokenOptions.ReplenishmentPeriod = TimeSpan.FromSeconds(options.Sensitive.ReplenishmentPeriodSeconds);
                    tokenOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    tokenOptions.QueueLimit = options.Sensitive.QueueLimit;
                });

                Log.Information("[RateLimit] 敏感操作限流策略已启用: {TokenLimit} 令牌，每 {ReplenishmentPeriod}秒补充 {TokensPerPeriod} 个",
                    options.Sensitive.TokenLimit, options.Sensitive.ReplenishmentPeriodSeconds, options.Sensitive.TokensPerPeriod);
            }

            // 并发限流策略（Concurrency Limiter）
            if (options.Concurrency.Enable)
            {
                limiterOptions.AddConcurrencyLimiter(PolicyNames.Concurrency, concurrencyOptions =>
                {
                    concurrencyOptions.PermitLimit = options.Concurrency.PermitLimit;
                    concurrencyOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    concurrencyOptions.QueueLimit = options.Concurrency.QueueLimit;
                });

                Log.Information("[RateLimit] 并发限流策略已启用: {PermitLimit} 并发请求，队列 {QueueLimit}",
                    options.Concurrency.PermitLimit, options.Concurrency.QueueLimit);
            }

            // 配置分区键解析器（按 IP 地址分区）
            limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(context =>
            {
                var ipAddress = GetClientIpAddress(context);

                // 检查白名单
                if (options.Whitelist.Enable && IsInWhitelist(ipAddress, options.Whitelist))
                {
                    if (options.EnableLogging)
                    {
                        Log.Debug("[RateLimit] IP {IpAddress} 在白名单中，跳过限流", ipAddress);
                    }
                    return RateLimitPartition.GetNoLimiter(ipAddress);
                }

                // 检查黑名单
                if (options.Blacklist.Enable && IsInBlacklist(ipAddress, options.Blacklist))
                {
                    if (options.EnableLogging)
                    {
                        Log.Warning("[RateLimit] IP {IpAddress} 在黑名单中，拒绝请求", ipAddress);
                    }
                    return RateLimitPartition.GetNoLimiter(ipAddress); // 黑名单在 OnRejected 中处理
                }

                // 应用全局限流
                if (options.Global.Enable)
                {
                    return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.Global.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.Global.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
                }

                return RateLimitPartition.GetNoLimiter(ipAddress);
            });
        });

        Log.Information("[RateLimit] 速率限制服务已注册");
    }

    /// <summary>使用速率限制中间件</summary>
    public static void UseRateLimitSetup(this IApplicationBuilder app)
    {
        var options = App.GetOptions<RateLimitOptions>();

        if (!options.Enable)
        {
            return;
        }

        app.UseRateLimiter();
        Log.Information("[RateLimit] 速率限制中间件已启用");
    }

    /// <summary>处理限流拒绝</summary>
    private static async Task HandleRateLimitRejection(
        OnRejectedContext context,
        RateLimitOptions options,
        CancellationToken cancellationToken)
    {
        var ipAddress = GetClientIpAddress(context.HttpContext);
        var endpoint = context.HttpContext.Request.Path;
        var method = context.HttpContext.Request.Method;

        // 记录限流日志
        if (options.EnableLogging)
        {
            Log.Warning("[RateLimit] 请求被限流 - IP: {IpAddress}, 端点: {Method} {Endpoint}, Lease: {Lease}",
                ipAddress, method, endpoint, context.Lease);
        }

        // 检查黑名单
        if (options.Blacklist.Enable && IsInBlacklist(ipAddress, options.Blacklist))
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                status = 403,
                message = "您的 IP 地址已被封禁",
                success = false
            }, cancellationToken: cancellationToken);
            return;
        }

        // 设置响应
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        // 尝试获取 Retry-After 时间
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString("F0");
        }

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            status = 429,
            message = "请求过于频繁，请稍后再试",
            success = false,
            retryAfter = retryAfter.TotalSeconds
        }, cancellationToken: cancellationToken);
    }

    /// <summary>获取客户端 IP 地址</summary>
    private static IPAddress GetClientIpAddress(HttpContext context)
    {
        // 优先从 X-Forwarded-For 获取（反向代理场景）
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0 && IPAddress.TryParse(ips[0].Trim(), out var ip))
            {
                return ip;
            }
        }

        // 从 X-Real-IP 获取
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out var realIpAddress))
        {
            return realIpAddress;
        }

        // 从连接信息获取
        return context.Connection.RemoteIpAddress ?? IPAddress.Loopback;
    }

    /// <summary>检查 IP 是否在白名单中</summary>
    private static bool IsInWhitelist(IPAddress ipAddress, WhitelistConfig whitelist)
    {
        if (!whitelist.Enable || whitelist.IpAddresses.Count == 0)
        {
            return false;
        }

        var ipString = ipAddress.ToString();
        return whitelist.IpAddresses.Any(whiteIp =>
            whiteIp.Equals(ipString, StringComparison.OrdinalIgnoreCase) ||
            IsIpInCidrRange(ipAddress, whiteIp));
    }

    /// <summary>检查 IP 是否在黑名单中</summary>
    private static bool IsInBlacklist(IPAddress ipAddress, BlacklistConfig blacklist)
    {
        if (!blacklist.Enable || blacklist.IpAddresses.Count == 0)
        {
            return false;
        }

        var ipString = ipAddress.ToString();
        return blacklist.IpAddresses.Any(blackIp =>
            blackIp.Equals(ipString, StringComparison.OrdinalIgnoreCase) ||
            IsIpInCidrRange(ipAddress, blackIp));
    }

    /// <summary>检查 IP 是否在 CIDR 范围内</summary>
    private static bool IsIpInCidrRange(IPAddress ipAddress, string cidr)
    {
        if (!cidr.Contains('/'))
        {
            return false;
        }

        try
        {
            var parts = cidr.Split('/');
            var baseAddress = IPAddress.Parse(parts[0]);
            var prefixLength = int.Parse(parts[1]);

            var ipBytes = ipAddress.GetAddressBytes();
            var baseBytes = baseAddress.GetAddressBytes();

            if (ipBytes.Length != baseBytes.Length)
            {
                return false;
            }

            var fullBytes = prefixLength / 8;
            var remainingBits = prefixLength % 8;

            // 检查完整字节
            for (var i = 0; i < fullBytes; i++)
            {
                if (ipBytes[i] != baseBytes[i])
                {
                    return false;
                }
            }

            // 检查剩余位
            if (remainingBits > 0)
            {
                var mask = (byte)(0xFF << (8 - remainingBits));
                if ((ipBytes[fullBytes] & mask) != (baseBytes[fullBytes] & mask))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
