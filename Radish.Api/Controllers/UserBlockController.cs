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

/// <summary>当前用户的私人屏蔽关系。</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("用户屏蔽")]
[Authorize(Policy = AuthorizationPolicies.Client)]
[ApiErrorContract]
public sealed class UserBlockController : ControllerBase
{
    private readonly IUserBlockService _userBlockService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public UserBlockController(
        IUserBlockService userBlockService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _userBlockService = userBlockService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    [HttpPost]
    [ProducesResponseType(typeof(MessageModel<UserBlockMutationVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> Block([FromBody] UserBlockMutationDto request)
    {
        var result = await _userBlockService.BlockAsync(
            Current.TenantId,
            Current.UserId,
            request.TargetUserPublicId,
            request.OperationKey,
            Current.UserName);
        return Success(result.Result.VoChanged ? "屏蔽成功" : "已屏蔽该用户", result.Result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MessageModel<UserBlockMutationVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> Unblock([FromBody] UserBlockMutationDto request)
    {
        var result = await _userBlockService.UnblockAsync(
            Current.TenantId,
            Current.UserId,
            request.TargetUserPublicId,
            request.OperationKey,
            Current.UserName);
        return Success("已解除屏蔽", result.Result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<UserBlockPageVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMine(int pageIndex = 1, int pageSize = 20)
    {
        var result = await _userBlockService.GetMineAsync(
            Current.TenantId,
            Current.UserId,
            pageIndex,
            pageSize);
        return Success("获取屏蔽列表成功", result);
    }

    private static MessageModel Success(string message, object data) => new()
    {
        IsSuccess = true,
        StatusCode = (int)HttpStatusCodeEnum.Success,
        MessageInfo = message,
        ResponseData = data
    };
}
