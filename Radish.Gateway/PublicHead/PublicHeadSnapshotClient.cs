using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Radish.Gateway.PublicHead;

/// <summary>公开详情页 head 快照与前端入口 HTML 获取客户端。</summary>
public sealed class PublicHeadSnapshotClient
{
    private static readonly TimeSpan IndexHtmlCacheTtl = TimeSpan.FromMinutes(5);
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<PublicHeadSnapshotClient> _logger;

    public PublicHeadSnapshotClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        ILogger<PublicHeadSnapshotClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<PublicHeadSnapshot?> GetSnapshotAsync(string apiPath, CancellationToken cancellationToken)
    {
        var apiBaseUrl = _configuration["DownstreamServices:ApiService:BaseUrl"];
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return null;
        }

        var requestUrl = CombineUrl(apiBaseUrl, apiPath);
        try
        {
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("公开 head 快照请求失败，Url={Url}, StatusCode={StatusCode}", requestUrl, response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<PublicHeadSnapshot>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "公开 head 快照请求异常，Url={Url}", requestUrl);
            return null;
        }
    }

    public async Task<string?> GetFrontendIndexHtmlAsync(CancellationToken cancellationToken)
    {
        var frontendBaseUrl = _configuration["FrontendService:BaseUrl"];
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
        {
            return null;
        }

        var cacheKey = $"public-head:index-html:{frontendBaseUrl.Trim().TrimEnd('/')}";
        if (_memoryCache.TryGetValue<string>(cacheKey, out var cachedHtml) && !string.IsNullOrWhiteSpace(cachedHtml))
        {
            return cachedHtml;
        }

        var requestUrl = CombineUrl(frontendBaseUrl, "/");
        try
        {
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("前端入口 HTML 请求失败，Url={Url}, StatusCode={StatusCode}", requestUrl, response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            _memoryCache.Set(cacheKey, html, IndexHtmlCacheTtl);
            return html;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "前端入口 HTML 请求异常，Url={Url}", requestUrl);
            return null;
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        return $"{baseUrl.Trim().TrimEnd('/')}{(path.StartsWith('/') ? path : $"/{path}")}";
    }
}
