using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;

namespace Radish.Api.Controllers;

/// <summary>公开详情页 HTML head 快照控制器。</summary>
[ApiController]
[ApiVersionNeutral]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class PublicHeadSnapshotController : ControllerBase
{
    private readonly IPublicHeadSnapshotService _publicHeadSnapshotService;
    private readonly IConfiguration _configuration;

    public PublicHeadSnapshotController(IPublicHeadSnapshotService publicHeadSnapshotService, IConfiguration configuration)
    {
        _publicHeadSnapshotService = publicHeadSnapshotService;
        _configuration = configuration;
    }

    [HttpGet("/api/public-head/forum/post/{postKey}")]
    public async Task<IActionResult> GetForumPost(string postKey)
    {
        var snapshot = await _publicHeadSnapshotService.GetForumPostSnapshotAsync(postKey, ResolvePublicBaseUrl());
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    [HttpGet("/api/public-head/docs/{slug}")]
    public async Task<IActionResult> GetDocs(string slug)
    {
        var snapshot = await _publicHeadSnapshotService.GetDocsSnapshotAsync(slug, ResolvePublicBaseUrl());
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    [HttpGet("/api/public-head/shop/product/{productId:long}")]
    public async Task<IActionResult> GetShopProduct(long productId)
    {
        var snapshot = await _publicHeadSnapshotService.GetShopProductSnapshotAsync(productId, ResolvePublicBaseUrl());
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    private string ResolvePublicBaseUrl()
    {
        var configuredUrl = _configuration["GatewayService:PublicUrl"];
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            configuredUrl = _configuration["RADISH_PUBLIC_URL"];
        }

        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            return configuredUrl.Trim().TrimEnd('/');
        }

        return $"{Request.Scheme}://{Request.Host}".TrimEnd('/');
    }
}
