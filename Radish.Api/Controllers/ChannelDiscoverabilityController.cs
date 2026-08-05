using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared;

namespace Radish.Api.Controllers;

/// <summary>Console 频道匿名公开摘要资格治理控制器。</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[ApiErrorContract]
[Tags("频道公开摘要治理")]
[Authorize(Policy = AuthorizationPolicies.Client)]
public sealed class ChannelDiscoverabilityController : ControllerBase
{
    private readonly IChannelDiscoverabilityService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ChannelDiscoverabilityController(
        IChannelDiscoverabilityService service,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.ChannelDiscoverabilityView)]
    [ProducesResponseType(typeof(MessageModel<PageModel<ChannelDiscoverabilityVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<PageModel<ChannelDiscoverabilityVo>>> GetPage(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        ChannelDiscoverVisibility? discoverVisibility = null,
        bool? isEnabled = null,
        bool includeDeleted = false)
    {
        var page = await _service.GetPageAsync(
            Current.TenantId,
            pageIndex,
            pageSize,
            keyword,
            discoverVisibility,
            isEnabled,
            includeDeleted);
        return MessageModel<PageModel<ChannelDiscoverabilityVo>>.Success("获取成功", page);
    }

    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.ChannelDiscoverabilityView)]
    [ProducesResponseType(typeof(MessageModel<IReadOnlyList<ChannelDiscoverVisibilityEventVo>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel<IReadOnlyList<ChannelDiscoverVisibilityEventVo>>> GetHistory(
        long channelId,
        int take = 20)
    {
        var history = await _service.GetHistoryAsync(Current.TenantId, channelId, take);
        return MessageModel<IReadOnlyList<ChannelDiscoverVisibilityEventVo>>.Success("获取成功", history);
    }

    [HttpPut("{channelId:long}")]
    [RequireConsolePermission(ConsolePermissions.ChannelDiscoverabilityManage)]
    [ProducesResponseType(typeof(MessageModel<ChannelDiscoverVisibilityMutationVo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status409Conflict)]
    public async Task<MessageModel<ChannelDiscoverVisibilityMutationVo>> UpdateVisibility(
        long channelId,
        [FromBody] UpdateChannelDiscoverVisibilityDto request)
    {
        var result = await _service.UpdateVisibilityAsync(
            Current.TenantId,
            channelId,
            Current.UserId,
            Current.UserName,
            request);
        return MessageModel<ChannelDiscoverVisibilityMutationVo>.Success(
            result.VoChanged ? "公开摘要资格已更新" : "公开摘要资格未变化",
            result);
    }
}
