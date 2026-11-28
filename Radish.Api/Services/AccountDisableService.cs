using System.Globalization;
using Radish.Auth.OpenIddict;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Api.Services;

public sealed class AccountDisableService(IUserRepository users, IReliableOutboxService outbox,
    WebAccountSessionRevocation sessions) : IAccountDisableService
{
    public async Task<AccountDisableVo> DisableAsync(long tenantId, long userId, long operatorId, string reason)
    {
        if (tenantId < 0 || operatorId <= 0 || userId <= 0 || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new BusinessException("停用请求无效", 400, "User.DisableInvalid", "error.user.disable_invalid");

        var (task, created) = await users.DisableAccountAsync(tenantId, userId, operatorId, reason.Trim(), DateTime.UtcNow);
        if (created)
        {
            // Finish the durable operation even when the initiating browser has disconnected.
            try
            {
                await RevokeWebSessionsAsync(tenantId, userId, CancellationToken.None);
                await outbox.MarkSucceededAsync(ReliableOutboxSources.Main, task.Id, DateTime.UtcNow);
            }
            catch (Exception error)
            {
                await outbox.MarkFailedAsync(ReliableOutboxSources.Main, task.Id, error, DateTime.UtcNow);
            }
        }

        var current = await outbox.QueryByIdAsync(ReliableOutboxSources.Main, task.Id)
            ?? throw new InvalidOperationException("账号停用任务不存在");
        return new AccountDisableVo { VoWebSessionRevocation = AccountDisableVo.FromTaskStatus(current.Status) };
    }

    public Task RevokeWebSessionsAsync(long tenantId, long userId, CancellationToken cancellationToken) =>
        sessions.RevokeAsync(tenantId.ToString(CultureInfo.InvariantCulture), userId.ToString(CultureInfo.InvariantCulture),
            DateTime.UtcNow, cancellationToken);
}
