using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace Radish.Gateway.PublicHead;

/// <summary>公开页面 HTML head 快照注入中间件。</summary>
public sealed class PublicHeadSnapshotMiddleware
{
    private static readonly TimeSpan InjectedHtmlCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly Regex ForumPostPathPattern = new("^/forum/post/([^/?#]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DocsPathPattern = new("^/docs/([^/?#]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ShopProductPathPattern = new("^/shop/product/([1-9][0-9]*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly IReadOnlyDictionary<string, string> StaticRouteSnapshotApiPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/discover"] = "/api/public-head/static/discover",
            ["/forum"] = "/api/public-head/static/forum",
            ["/docs"] = "/api/public-head/static/docs",
            ["/leaderboard"] = "/api/public-head/static/leaderboard",
            ["/shop"] = "/api/public-head/static/shop"
        };

    private readonly RequestDelegate _next;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<PublicHeadSnapshotMiddleware> _logger;

    public PublicHeadSnapshotMiddleware(
        RequestDelegate next,
        IMemoryCache memoryCache,
        ILogger<PublicHeadSnapshotMiddleware> logger)
    {
        _next = next;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, PublicHeadSnapshotClient snapshotClient)
    {
        if (!ShouldHandle(context, out var snapshotRequest))
        {
            await _next(context);
            return;
        }

        var cacheKey = BuildCacheKey(context, snapshotRequest.ApiPath);
        if (_memoryCache.TryGetValue<string>(cacheKey, out var cachedHtml) && !string.IsNullOrWhiteSpace(cachedHtml))
        {
            await WriteHtmlAsync(context, cachedHtml);
            return;
        }

        var snapshot = await snapshotClient.GetSnapshotAsync(snapshotRequest.ApiPath, context.RequestAborted);
        if (snapshot is null)
        {
            await _next(context);
            return;
        }

        var indexHtml = await snapshotClient.GetFrontendIndexHtmlAsync(context.RequestAborted);
        if (string.IsNullOrWhiteSpace(indexHtml))
        {
            await _next(context);
            return;
        }

        try
        {
            var injectedHtml = PublicHeadHtmlInjector.Inject(indexHtml, snapshot);
            _memoryCache.Set(cacheKey, injectedHtml, InjectedHtmlCacheTtl);
            await WriteHtmlAsync(context, injectedHtml);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "公开详情页 HTML head 注入失败，Path={Path}", context.Request.Path);
            await _next(context);
        }
    }

    private static bool ShouldHandle(HttpContext context, out SnapshotRequest snapshotRequest)
    {
        snapshotRequest = default;

        if (context.WebSockets.IsWebSocketRequest ||
            context.Request.Method is not ("GET" or "HEAD") ||
            !AcceptsHtml(context.Request))
        {
            return false;
        }

        var path = context.Request.Path.Value?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (StaticRouteSnapshotApiPaths.TryGetValue(path, out var staticApiPath))
        {
            snapshotRequest = new SnapshotRequest(staticApiPath);
            return true;
        }

        var forumMatch = ForumPostPathPattern.Match(path);
        if (forumMatch.Success)
        {
            snapshotRequest = new SnapshotRequest($"/api/public-head/forum/post/{Uri.EscapeDataString(Uri.UnescapeDataString(forumMatch.Groups[1].Value))}");
            return true;
        }

        var docsMatch = DocsPathPattern.Match(path);
        if (docsMatch.Success)
        {
            var slug = Uri.UnescapeDataString(docsMatch.Groups[1].Value);
            if (string.Equals(slug, "search", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            snapshotRequest = new SnapshotRequest($"/api/public-head/docs/{Uri.EscapeDataString(slug)}");
            return true;
        }

        var shopMatch = ShopProductPathPattern.Match(path);
        if (shopMatch.Success)
        {
            snapshotRequest = new SnapshotRequest($"/api/public-head/shop/product/{shopMatch.Groups[1].Value}");
            return true;
        }

        return false;
    }

    private static bool AcceptsHtml(HttpRequest request)
    {
        if (!request.Headers.Accept.Any())
        {
            return true;
        }

        return request.Headers.Accept.Any(value =>
            value is not null &&
            (value.Contains("text/html", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("*/*", StringComparison.OrdinalIgnoreCase)));
    }

    private static string BuildCacheKey(HttpContext context, string apiPath)
    {
        return $"public-head:html:{context.Request.Host}:{apiPath}";
    }

    private static async Task WriteHtmlAsync(HttpContext context, string html)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";

        if (context.Request.Method == "HEAD")
        {
            return;
        }

        await context.Response.WriteAsync(html, Encoding.UTF8);
    }

    private readonly record struct SnapshotRequest(string ApiPath);
}
