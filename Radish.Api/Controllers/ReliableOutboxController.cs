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
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Client)]
[Tags("可靠任务")]
public sealed class ReliableOutboxController : ControllerBase
{
    private readonly IReliableOutboxService _outboxService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ReliableOutboxController(
        IReliableOutboxService outboxService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _outboxService = outboxService;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.HangfireView)]
    [ProducesResponseType(typeof(MessageModel<List<ReliableOutboxVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetDeadLetters(string sourceDatabase = ReliableOutboxSources.Main, int take = 50)
    {
        var normalizedSource = string.Equals(sourceDatabase, ReliableOutboxSources.Chat, StringComparison.OrdinalIgnoreCase)
            ? ReliableOutboxSources.Chat
            : ReliableOutboxSources.Main;
        var messages = await _outboxService.QueryDeadLettersAsync(normalizedSource, take);
        var result = messages.Select(message => new ReliableOutboxVo
        {
            VoSourceDatabase = message.SourceDatabase,
            VoId = message.Id,
            VoTenantId = message.TenantId,
            VoTaskType = message.TaskType,
            VoAggregateType = message.AggregateType,
            VoAggregateId = message.AggregateId,
            VoStatus = message.Status,
            VoAttemptCount = message.AttemptCount,
            VoMaxAttempts = message.MaxAttempts,
            VoAvailableAtUtc = message.AvailableAtUtc,
            VoLastErrorCode = message.LastErrorCode,
            VoLastErrorSummary = message.LastErrorSummary
        }).ToList();

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = result
        };
    }

    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.HangfireReplay)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> Replay([FromBody] ReliableOutboxReplayDto request)
    {
        if (request.OutboxId <= 0 || string.IsNullOrWhiteSpace(request.Reason))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "任务 ID 和重放原因不能为空"
            };
        }

        var sourceDatabase = string.Equals(
            request.SourceDatabase,
            ReliableOutboxSources.Chat,
            StringComparison.OrdinalIgnoreCase)
            ? ReliableOutboxSources.Chat
            : string.Equals(
                request.SourceDatabase,
                ReliableOutboxSources.Main,
                StringComparison.OrdinalIgnoreCase)
                ? ReliableOutboxSources.Main
                : null;
        if (sourceDatabase == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "来源数据库只允许 Main 或 Chat"
            };
        }

        var current = _currentUserAccessor.Current;
        var operatorName = string.IsNullOrWhiteSpace(current.UserName)
            ? $"User-{current.UserId}"
            : current.UserName;
        var replayed = await _outboxService.ReplayAsync(
            sourceDatabase,
            request.OutboxId,
            operatorName,
            request.Reason,
            DateTime.UtcNow);

        return new MessageModel
        {
            IsSuccess = replayed,
            StatusCode = replayed
                ? (int)HttpStatusCodeEnum.Success
                : (int)HttpStatusCodeEnum.BadRequest,
            MessageInfo = replayed ? "可靠任务已进入待执行队列" : "只有 DeadLetter 任务可以重放"
        };
    }
}
