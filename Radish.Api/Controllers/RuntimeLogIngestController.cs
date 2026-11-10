using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Logging;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Api.Controllers;

[ApiController]
[ApiVersionNeutral]
[AllowAnonymous] // 监听与 collector 凭据由内网管道校验，绝不使用用户 JWT 放行。
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class RuntimeLogIngestController(IRuntimeLogIngestService service) : ControllerBase
{
    [HttpPost(RuntimeLogIngestSetup.IngestPath)]
    public async Task<ActionResult<MessageModel<RuntimeLogIngestVo>>> Ingest()
    {
        var result = await service.IngestAsync(Request.Body, HttpContext.RequestAborted);
        return StatusCode(result.StatusCode, new MessageModel<RuntimeLogIngestVo>
        {
            StatusCode = result.StatusCode, IsSuccess = result.StatusCode == 200,
            MessageInfo = result.StatusCode == 200 ? "Runtime log batch committed." : "Runtime log batch not fully committed.",
            ResponseData = result.Summary
        });
    }
}
