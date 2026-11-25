using System.Data.Common;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.HttpContextTool;
using Radish.Common.LogTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Api.Controllers.v1;

[ApiController, ApiVersion(1), Route("api/v{version:apiVersion}/[controller]/[action]")]
[Authorize(Policy = AuthorizationPolicies.Client)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RuntimeLogConsoleController(IRuntimeLogConsoleService console) : ControllerBase
{
    [HttpPost, RequireConsolePermission(ConsolePermissions.LogsView, PlatformOnly = true)]
    public Task<ActionResult<MessageModel<RuntimeLogPageVo>>> Query(RuntimeLogQueryDto query, CancellationToken token) =>
        Execute(() => console.QueryAsync(query, token));

    [HttpGet, RequireConsolePermission(ConsolePermissions.LogsView, PlatformOnly = true)]
    public Task<ActionResult<MessageModel<RuntimeLogDetailVo>>> Detail(string eventId, CancellationToken token) =>
        Execute(async () => await console.GetAsync(eventId, token) ?? throw new KeyNotFoundException());

    [HttpGet, RequireConsolePermission(ConsolePermissions.LogsView, ConsolePermissions.AlertsView, PlatformOnly = true)]
    public Task<ActionResult<MessageModel<RuntimeLogPipelineVo>>> Pipeline(CancellationToken token) =>
        Execute(() => console.GetPipelineAsync(token));

    [HttpGet, RequireConsolePermission(ConsolePermissions.LogsView, PlatformOnly = true)]
    public Task<ActionResult<MessageModel<IReadOnlyList<RuntimeLogHistoryVo>>>> History(DateTimeOffset? from, DateTimeOffset? to,
        CancellationToken token) => Execute(() => console.GetHistoryAsync(from, to, false, token));

    [HttpGet, RequireConsolePermission(ConsolePermissions.LogsAudit, PlatformOnly = true)]
    public Task<ActionResult<MessageModel<IReadOnlyList<RuntimeLogHistoryVo>>>> Audit(DateTimeOffset? from, DateTimeOffset? to,
        CancellationToken token) => Execute(() => console.GetHistoryAsync(from, to, true, token));

    [HttpPost, RequireConsolePermission(ConsolePermissions.LogsView, PlatformOnly = true)]
    [RequireConsolePermission(ConsolePermissions.LogsExport, PlatformOnly = true)]
    public Task<ActionResult<MessageModel<RuntimeLogCsvVo>>> Export(RuntimeLogQueryDto query, CancellationToken token) =>
        Execute(() => console.ExportAsync(query, token));

    [HttpGet, RequireConsolePermission(ConsolePermissions.AlertsView, PlatformOnly = true)]
    public Task<ActionResult<MessageModel<RuntimeLogAlertsVo>>> Alerts(CancellationToken token) =>
        Execute(() => console.GetAlertsAsync(token));

    [HttpPost, RequireConsolePermission(ConsolePermissions.AlertsView, PlatformOnly = true)]
    [RequireConsolePermission(ConsolePermissions.AlertsManage, PlatformOnly = true)]
    public async Task<ActionResult<MessageModel<bool>>> Transition(string id, RuntimeLogAlertActionDto action, CancellationToken token)
    {
        var result = await Execute(() => console.TransitionAsync(id, action, token));
        return result.Value?.ResponseData == false && result.Result == null ? Failed<bool>(409, "告警状态已变化或当前操作不可用") : result;
    }

    private async Task<ActionResult<MessageModel<T>>> Execute<T>(Func<Task<T>> action)
    {
        using var scope = new RuntimeLogIngestScope();
        if (!console.Enabled) return Failed<T>(404, "运行日志 Console 未启用");
        try { return MessageModel<T>.Success("成功", await action()); }
        catch (ArgumentException) { return Failed<T>(400, "日志筛选、游标或操作参数无效"); }
        catch (KeyNotFoundException) { return Failed<T>(404, "事件不存在或已过期"); }
        catch (Exception ex) when (ex is DbException or TimeoutException)
        {
            return Failed<T>(503, "日志存储暂不可用");
        }
    }

    private ObjectResult Failed<T>(int status, string message) => StatusCode(status,
        new MessageModel<T> { StatusCode = status, IsSuccess = false, MessageInfo = message });
}
