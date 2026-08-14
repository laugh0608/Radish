using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>论坛帖子与评论的固定金额内容赞赏。</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("论坛内容赞赏")]
[ApiErrorContract]
public sealed class ContentRewardController : ControllerBase
{
    private readonly IContentRewardService _contentRewardService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ContentRewardController(
        IContentRewardService contentRewardService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _contentRewardService = contentRewardService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [ProducesResponseType(typeof(MessageModel<ContentRewardMutationVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> Create([FromBody] CreateContentRewardDto request)
    {
        var result = await _contentRewardService.CreateAsync(
            request,
            Current.UserId,
            Current.UserName,
            Current.TenantId);
        return Success("已送出 1 胡萝卜", result);
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel<ContentRewardTargetPageVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetTargetRewards(
        [FromQuery] string targetType,
        [FromQuery] long targetId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _contentRewardService.GetTargetRewardsAsync(
            targetType,
            targetId,
            Current.UserId,
            Current.TenantId,
            pageIndex,
            pageSize);
        return Success("获取内容赞赏成功", result);
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel<IReadOnlyList<ContentRewardTargetStateVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetTargetStates([FromBody] GetContentRewardTargetStatesDto request)
    {
        var result = await _contentRewardService.GetTargetStatesAsync(
            request,
            Current.UserId,
            Current.TenantId);
        return Success("获取内容赞赏状态成功", result);
    }

    private static MessageModel Success(string message, object data) => new()
    {
        IsSuccess = true,
        StatusCode = (int)HttpStatusCodeEnum.Success,
        MessageInfo = message,
        ResponseData = data
    };
}
