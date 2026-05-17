using System.Text;
using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;

namespace Radish.Api.Controllers;

/// <summary>公开 sitemap 输出控制器。</summary>
[ApiController]
[ApiVersionNeutral]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class PublicSitemapController : ControllerBase
{
    private static readonly Regex SectionFileNamePattern = new(
        "^(forum|docs|shop)-([1-9][0-9]*)\\.xml$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IPublicSitemapService _publicSitemapService;
    private readonly IConfiguration _configuration;

    public PublicSitemapController(IPublicSitemapService publicSitemapService, IConfiguration configuration)
    {
        _publicSitemapService = publicSitemapService;
        _configuration = configuration;
    }

    [HttpGet("/sitemap.xml")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetIndex()
    {
        var xml = await _publicSitemapService.GetIndexXmlAsync(ResolvePublicBaseUrl());
        return Content(xml, "application/xml; charset=utf-8", Encoding.UTF8);
    }

    [HttpGet("/sitemaps/{fileName}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSection(string fileName)
    {
        var normalizedFileName = fileName.Trim();
        if (string.Equals(normalizedFileName, "static.xml", StringComparison.OrdinalIgnoreCase))
        {
            var staticXml = await _publicSitemapService.GetSectionXmlAsync("static", 1, ResolvePublicBaseUrl());
            return Content(staticXml, "application/xml; charset=utf-8", Encoding.UTF8);
        }

        var match = SectionFileNamePattern.Match(normalizedFileName);
        if (!match.Success || !int.TryParse(match.Groups[2].Value, out var pageIndex))
        {
            return NotFound();
        }

        var xml = await _publicSitemapService.GetSectionXmlAsync(match.Groups[1].Value, pageIndex, ResolvePublicBaseUrl());
        return Content(xml, "application/xml; charset=utf-8", Encoding.UTF8);
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

        var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedProto) &&
            !string.IsNullOrWhiteSpace(forwardedHost) &&
            IsSafeForwardedHost(forwardedHost))
        {
            return $"{forwardedProto.Trim()}://{forwardedHost.Trim()}".TrimEnd('/');
        }

        return $"{Request.Scheme}://{Request.Host}".TrimEnd('/');
    }

    private static bool IsSafeForwardedHost(string forwardedHost)
    {
        return !forwardedHost.Contains('/', StringComparison.Ordinal) &&
            !forwardedHost.Contains('\\', StringComparison.Ordinal) &&
            !forwardedHost.Contains(' ', StringComparison.Ordinal);
    }
}
