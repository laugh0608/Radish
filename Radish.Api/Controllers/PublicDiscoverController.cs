using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared;

namespace Radish.Api.Controllers;

/// <summary>匿名公开社区发现读模型。</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[ApiErrorContract]
[Tags("公开社区发现")]
public sealed class PublicDiscoverController : ControllerBase
{
    private readonly IPublicDiscoverService _service;

    public PublicDiscoverController(IPublicDiscoverService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(MessageModel<PublicDiscoverFeedVo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status503ServiceUnavailable)]
    public async Task<MessageModel<PublicDiscoverFeedVo>> GetFeed(
        string? cursor = null,
        int pageSize = 20)
    {
        Response.Headers.CacheControl = "no-store";
        var feed = await _service.GetFeedAsync(cursor, pageSize);
        return MessageModel<PublicDiscoverFeedVo>.Success("获取成功", feed);
    }
}
