using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Api.Services;
using Radish.Common.PermissionTool;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[ApiErrorContract]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class HangfireSessionController(IHangfireDashboardSessionService sessionService) : ControllerBase
{
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.HangfireView)]
    public async Task<ActionResult<MessageModel<HangfireSessionVo>>> Create()
    {
        var session = await sessionService.CreateAsync();
        if (session == null)
        {
            return Unauthorized();
        }

        return MessageModel<HangfireSessionVo>.Success("任务看板会话已建立", session);
    }
}
