using Radish.Model.ViewModels;

namespace Radish.IService;

public interface IAccountDisableService
{
    Task<AccountDisableVo> DisableAsync(long tenantId, long userId, long operatorId, string reason);
    Task RevokeWebSessionsAsync(long tenantId, long userId, CancellationToken cancellationToken);
}
