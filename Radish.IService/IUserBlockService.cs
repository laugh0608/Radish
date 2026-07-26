using Radish.Model.ViewModels;

namespace Radish.IService;

public sealed record UserBlockServiceMutationResult(
    UserBlockMutationVo Result,
    long TargetUserId);

public interface IUserBlockService
{
    Task<UserBlockServiceMutationResult> BlockAsync(
        long tenantId,
        long currentUserId,
        string targetUserPublicId,
        string operationKey,
        string operatorName);

    Task<UserBlockServiceMutationResult> UnblockAsync(
        long tenantId,
        long currentUserId,
        string targetUserPublicId,
        string operationKey,
        string operatorName);

    Task<UserBlockServiceMutationResult> BlockByUserIdAsync(
        long tenantId,
        long currentUserId,
        long targetUserId,
        string operationKey,
        string operatorName);

    Task<UserBlockServiceMutationResult> UnblockByUserIdAsync(
        long tenantId,
        long currentUserId,
        long targetUserId,
        string operationKey,
        string operatorName);

    Task<UserBlockPageVo> GetMineAsync(
        long tenantId,
        long currentUserId,
        int pageIndex,
        int pageSize);
}
