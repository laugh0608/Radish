using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>电子宠物控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Client)]
[Tags("电子宠物")]
public class PetController : ControllerBase
{
    private readonly IPetService _petService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public PetController(IPetService petService, ICurrentUserAccessor currentUserAccessor)
    {
        _petService = petService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>读取当前用户宠物状态</summary>
    [HttpGet]
    public async Task<MessageModel<PetProfileVo?>> GetMy()
    {
        return await ExecuteAsync(async () =>
            MessageModel<PetProfileVo?>.Success("查询成功", await _petService.GetMyPetAsync(Current.UserId)));
    }

    /// <summary>领取默认宠物</summary>
    [HttpPost]
    public async Task<MessageModel<PetProfileVo>> Claim([FromBody] PetClaimDto? request)
    {
        return await ExecuteAsync(async () =>
            MessageModel<PetProfileVo>.Success(
                "领取成功",
                await _petService.ClaimAsync(Current.UserId, Current.UserName, Current.TenantId, request ?? new PetClaimDto())));
    }

    /// <summary>更新宠物资料</summary>
    [HttpPut]
    public async Task<MessageModel<PetProfileVo>> UpdateProfile([FromBody] UpdatePetProfileDto request)
    {
        if (!ModelState.IsValid)
        {
            return MessageModel<PetProfileVo>.Message(false, "请求参数验证失败", default!);
        }

        return await ExecuteAsync(async () =>
            MessageModel<PetProfileVo>.Success(
                "更新成功",
                await _petService.UpdateProfileAsync(Current.UserId, Current.UserName, request)));
    }

    /// <summary>照顾宠物</summary>
    [HttpPost]
    public async Task<MessageModel<PetCareResultVo>> Care([FromBody] PetCareDto request)
    {
        if (!ModelState.IsValid)
        {
            return MessageModel<PetCareResultVo>.Message(false, "请求参数验证失败", default!);
        }

        return await ExecuteAsync(async () =>
            MessageModel<PetCareResultVo>.Success(
                "操作成功",
                await _petService.CareAsync(Current.UserId, Current.UserName, request)));
    }

    /// <summary>查询当前用户宠物状态流水</summary>
    [HttpGet]
    public async Task<MessageModel<VoPagedResult<PetStatLogVo>>> GetLogs(int pageIndex = 1, int pageSize = 20)
    {
        return await ExecuteAsync(async () =>
            MessageModel<VoPagedResult<PetStatLogVo>>.Success(
                "查询成功",
                await _petService.GetMyLogsAsync(Current.UserId, pageIndex, pageSize)));
    }

    private static async Task<MessageModel<T>> ExecuteAsync<T>(Func<Task<MessageModel<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (BusinessException ex)
        {
            return new MessageModel<T>
            {
                IsSuccess = false,
                StatusCode = ex.StatusCode,
                MessageInfo = ex.Message,
                Code = ex.ErrorCode
            };
        }
    }
}
