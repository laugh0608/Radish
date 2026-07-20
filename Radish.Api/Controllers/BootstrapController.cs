using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Radish.Api.Filters;
using Radish.Api.Resources;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

[ApiController]
[ApiErrorContract]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("系统初始化")]
public class BootstrapController : ControllerBase
{
    private readonly IBootstrapService _bootstrapService;
    private readonly IStringLocalizer<Errors> _errorsLocalizer;

    public BootstrapController(
        IBootstrapService bootstrapService,
        IStringLocalizer<Errors> errorsLocalizer)
    {
        _bootstrapService = bootstrapService;
        _errorsLocalizer = errorsLocalizer;
    }

    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<BootstrapStatusVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel<BootstrapStatusVo>> Status()
    {
        var status = await _bootstrapService.GetStatusAsync();
        return MessageModel<BootstrapStatusVo>.Success("获取成功", status);
    }

    [HttpPost]
    [EnableRateLimiting("sensitive")]
    [ProducesResponseType(typeof(MessageModel<BootstrapAdminCreatedVo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel<BootstrapAdminCreatedVo>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel<BootstrapAdminCreatedVo>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MessageModel<BootstrapAdminCreatedVo>), StatusCodes.Status500InternalServerError)]
    public async Task<MessageModel<BootstrapAdminCreatedVo>> CreateFirstAdministrator(
        [FromBody] BootstrapCreateAdminDto dto)
    {
        var result = await _bootstrapService.CreateFirstAdministratorAsync(dto);
        if (result.Status != BootstrapAdminCreationStatus.Created)
        {
            return BuildFailure(result);
        }

        return MessageModel<BootstrapAdminCreatedVo>.Success(
            "初始化完成",
            new BootstrapAdminCreatedVo
            {
                VoUserId = result.UserId,
                VoDisplayName = result.DisplayName,
                VoEmail = result.Email
            });
    }

    private MessageModel<BootstrapAdminCreatedVo> BuildFailure(BootstrapAdminCreationResult result)
    {
        var localizedMessage = string.IsNullOrWhiteSpace(result.MessageKey)
            ? null
            : _errorsLocalizer[result.MessageKey, result.MessageArguments];

        return new MessageModel<BootstrapAdminCreatedVo>
        {
            IsSuccess = false,
            StatusCode = ResolveFailureStatusCode(result.Status),
            MessageInfo = localizedMessage is null || localizedMessage.ResourceNotFound
                ? result.Message
                : localizedMessage.Value,
            Code = result.Code,
            MessageKey = result.MessageKey
        };
    }

    private static int ResolveFailureStatusCode(BootstrapAdminCreationStatus status)
    {
        return status switch
        {
            BootstrapAdminCreationStatus.InvalidInput => (int)HttpStatusCodeEnum.BadRequest,
            BootstrapAdminCreationStatus.AlreadyInitialized or
                BootstrapAdminCreationStatus.EmailTaken or
                BootstrapAdminCreationStatus.ConcurrentInitialization => (int)HttpStatusCodeEnum.Conflict,
            _ => (int)HttpStatusCodeEnum.InternalServerError
        };
    }
}
