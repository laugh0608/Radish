using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("系统初始化")]
public class BootstrapController : ControllerBase
{
    private readonly IBootstrapService _bootstrapService;

    public BootstrapController(IBootstrapService bootstrapService)
    {
        _bootstrapService = bootstrapService;
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
    public async Task<MessageModel<BootstrapAdminCreatedVo>> CreateFirstAdministrator(
        [FromBody] BootstrapCreateAdminDto dto)
    {
        var result = await _bootstrapService.CreateFirstAdministratorAsync(dto);
        if (result.Status != BootstrapAdminCreationStatus.Created)
        {
            return MessageModel<BootstrapAdminCreatedVo>.Failed(result.Message);
        }

        return MessageModel<BootstrapAdminCreatedVo>.Success(
            "初始化完成",
            new BootstrapAdminCreatedVo
            {
                VoUserId = result.UserId,
                VoLoginName = result.LoginName
            });
    }
}
