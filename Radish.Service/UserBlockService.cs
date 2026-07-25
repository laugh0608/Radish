using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;

namespace Radish.Service;

/// <summary>用户本人屏蔽、解除和私域列表的应用服务。</summary>
public sealed class UserBlockService : IUserBlockService
{
    private readonly IUserBlockRepository _userBlockRepository;
    private readonly IUserInteractionPolicyService _interactionPolicyService;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly TimeProvider _timeProvider;

    public UserBlockService(
        IUserBlockRepository userBlockRepository,
        IUserInteractionPolicyService interactionPolicyService,
        IBaseRepository<User> userRepository,
        IBaseRepository<Attachment> attachmentRepository,
        IAttachmentUrlResolver attachmentUrlResolver,
        TimeProvider timeProvider)
    {
        _userBlockRepository = userBlockRepository;
        _interactionPolicyService = interactionPolicyService;
        _userRepository = userRepository;
        _attachmentRepository = attachmentRepository;
        _attachmentUrlResolver = attachmentUrlResolver;
        _timeProvider = timeProvider;
    }

    public async Task<UserBlockServiceMutationResult> BlockAsync(
        long tenantId,
        long currentUserId,
        string targetUserPublicId,
        string operationKey,
        string operatorName)
    {
        var target = await RequireTargetByPublicIdAsync(
            tenantId,
            currentUserId,
            targetUserPublicId,
            requireAvailable: true);
        return await MutateAsync(
            tenantId,
            currentUserId,
            target,
            operationKey,
            operatorName,
            UserBlockOperationTypes.Block);
    }

    public async Task<UserBlockServiceMutationResult> UnblockAsync(
        long tenantId,
        long currentUserId,
        string targetUserPublicId,
        string operationKey,
        string operatorName)
    {
        var target = await RequireTargetByPublicIdAsync(
            tenantId,
            currentUserId,
            targetUserPublicId,
            requireAvailable: false);
        return await MutateAsync(
            tenantId,
            currentUserId,
            target,
            operationKey,
            operatorName,
            UserBlockOperationTypes.Unblock);
    }

    public async Task<UserBlockServiceMutationResult> BlockByUserIdAsync(
        long tenantId,
        long currentUserId,
        long targetUserId,
        string operationKey,
        string operatorName)
    {
        var target = await RequireTargetByIdAsync(
            tenantId,
            currentUserId,
            targetUserId,
            requireAvailable: true);
        return await MutateAsync(
            tenantId,
            currentUserId,
            target,
            operationKey,
            operatorName,
            UserBlockOperationTypes.Block);
    }

    public async Task<UserBlockServiceMutationResult> UnblockByUserIdAsync(
        long tenantId,
        long currentUserId,
        long targetUserId,
        string operationKey,
        string operatorName)
    {
        var target = await RequireTargetByIdAsync(
            tenantId,
            currentUserId,
            targetUserId,
            requireAvailable: false);
        return await MutateAsync(
            tenantId,
            currentUserId,
            target,
            operationKey,
            operatorName,
            UserBlockOperationTypes.Unblock);
    }

    public async Task<UserBlockPageVo> GetMineAsync(
        long tenantId,
        long currentUserId,
        int pageIndex,
        int pageSize)
    {
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var (blocks, total) = await _userBlockRepository.QueryMineAsync(
            Math.Max(0, tenantId),
            currentUserId,
            safePageIndex,
            safePageSize);
        if (blocks.Count == 0)
        {
            return new UserBlockPageVo
            {
                VoPageIndex = safePageIndex,
                VoPageSize = safePageSize,
                VoTotal = total
            };
        }

        var targetIds = blocks.Select(item => item.BlockedUserId).Distinct().ToList();
        var users = await _userRepository.QueryAsync(user =>
            user.TenantId == Math.Max(0, tenantId) &&
            targetIds.Contains(user.Id));
        var userMap = users.ToDictionary(user => user.Id);
        var availableUserIds = users
            .Where(user => user.IsEnable && !user.IsDeleted)
            .Select(user => user.Id)
            .ToList();
        var avatarMap = await LoadAvatarMapAsync(availableUserIds);
        return new UserBlockPageVo
        {
            VoItems = blocks.Select(block =>
            {
                userMap.TryGetValue(block.BlockedUserId, out var target);
                return new UserBlockListItemVo
                {
                    VoTargetUserPublicId = target?.PublicId?.Trim().ToLowerInvariant() ?? string.Empty,
                    VoTargetDisplayName = ResolveTargetDisplayName(target),
                    VoTargetAvatarUrl = avatarMap.GetValueOrDefault(block.BlockedUserId),
                    VoBlockedAtUtc = block.ModifyTime ?? block.CreateTime,
                    VoCanUnblock = target != null && User.HasPublicIdFormat(target.PublicId)
                };
            }).ToList(),
            VoTotal = total,
            VoPageIndex = safePageIndex,
            VoPageSize = safePageSize
        };
    }

    private async Task<UserBlockServiceMutationResult> MutateAsync(
        long tenantId,
        long currentUserId,
        User target,
        string operationKey,
        string operatorName,
        string operationType)
    {
        var normalizedOperationKey = NormalizeOperationKey(operationKey);
        UserBlockWriteResult writeResult;
        try
        {
            writeResult = await _userBlockRepository.MutateAsync(new UserBlockMutationCommand(
                Math.Max(0, tenantId),
                currentUserId,
                target.Id,
                operationType,
                normalizedOperationKey,
                NormalizeOperator(operatorName),
                _timeProvider.GetUtcNow().UtcDateTime));
        }
        catch (UserBlockOperationConflictException)
        {
            throw OperationConflict();
        }
        catch (UserBlockStateConflictException)
        {
            throw StateConflict();
        }

        var policy = await _interactionPolicyService.GetSnapshotAsync(tenantId, currentUserId, target.Id);
        var canInteract = target.IsEnable && !target.IsDeleted && policy.CanInteract;
        return new UserBlockServiceMutationResult(
            new UserBlockMutationVo
            {
                VoTargetUserPublicId = target.PublicId!.Trim().ToLowerInvariant(),
                VoRelationshipVersion = writeResult.RelationshipVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                VoChanged = writeResult.Changed,
                VoCapabilities = new UserInteractionCapabilityVo
                {
                    VoCanFollow = canInteract,
                    VoCanDirectMessage = canInteract,
                    VoCanInteract = canInteract,
                    VoInteractionUnavailable = !canInteract,
                    VoIsBlockedByCurrentUser = policy.IsBlockedByCurrentUser
                }
            },
            target.Id);
    }

    private async Task<User> RequireTargetByPublicIdAsync(
        long tenantId,
        long currentUserId,
        string targetUserPublicId,
        bool requireAvailable)
    {
        var normalizedPublicId = targetUserPublicId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!User.HasPublicIdFormat(normalizedPublicId))
        {
            throw TargetUnavailable();
        }

        var target = await _userRepository.QueryFirstAsync(user =>
            user.PublicId == normalizedPublicId &&
            user.TenantId == Math.Max(0, tenantId) &&
            (!requireAvailable || user.IsEnable && !user.IsDeleted));
        return ValidateTarget(currentUserId, target);
    }

    private async Task<User> RequireTargetByIdAsync(
        long tenantId,
        long currentUserId,
        long targetUserId,
        bool requireAvailable)
    {
        if (targetUserId <= 0)
        {
            throw TargetUnavailable();
        }

        var target = await _userRepository.QueryFirstAsync(user =>
            user.Id == targetUserId &&
            user.TenantId == Math.Max(0, tenantId) &&
            (!requireAvailable || user.IsEnable && !user.IsDeleted));
        return ValidateTarget(currentUserId, target);
    }

    private static User ValidateTarget(long currentUserId, User? target)
    {
        if (target == null || !User.HasPublicIdFormat(target.PublicId))
        {
            throw TargetUnavailable();
        }

        if (target.Id == currentUserId)
        {
            throw SelfNotAllowed();
        }

        return target;
    }

    private static string ResolveTargetDisplayName(User? target)
    {
        if (target is not { IsEnable: true, IsDeleted: false })
        {
            return "用户不可用";
        }

        return User.BuildDisplayHandle(target.UserName, target.PublicIndex, target.Id)
               ?? User.NormalizeDisplayName(target.UserName, target.Id);
    }

    private async Task<Dictionary<long, string>> LoadAvatarMapAsync(IReadOnlyCollection<long> userIds)
    {
        var attachments = await _attachmentRepository.QueryAsync(attachment =>
            attachment.BusinessType == AttachmentBusinessTypes.Avatar &&
            attachment.BusinessId.HasValue &&
            userIds.Contains(attachment.BusinessId.Value) &&
            attachment.IsEnabled &&
            !attachment.IsDeleted);
        return attachments
            .Where(attachment => attachment.BusinessId.HasValue)
            .GroupBy(attachment => attachment.BusinessId!.Value)
            .ToDictionary(
                group => group.Key,
                group => _attachmentUrlResolver.ResolveAttachmentUrl(
                    group.OrderByDescending(attachment => attachment.Id).First().Id));
    }

    private static string NormalizeOperationKey(string operationKey)
    {
        var normalized = operationKey?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 100)
        {
            throw new BusinessException(
                "operationKey 无效",
                StatusCodes.Status400BadRequest,
                "UserBlock.OperationKeyInvalid",
                "error.user_block.operation_key_invalid");
        }

        return normalized;
    }

    private static string NormalizeOperator(string operatorName) =>
        string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();

    private static BusinessException SelfNotAllowed() => new(
        "不能屏蔽自己",
        StatusCodes.Status400BadRequest,
        "UserBlock.SelfNotAllowed",
        "error.user_block.self_not_allowed");

    private static BusinessException TargetUnavailable() => new(
        "目标用户不存在或当前不可用",
        StatusCodes.Status404NotFound,
        "UserBlock.TargetUnavailable",
        "error.user_block.target_unavailable");

    private static BusinessException OperationConflict() => new(
        "operationKey 已用于不同的屏蔽操作",
        StatusCodes.Status409Conflict,
        "UserBlock.OperationConflict",
        "error.user_block.operation_conflict");

    private static BusinessException StateConflict() => new(
        "用户屏蔽状态已变化，请刷新后重试",
        StatusCodes.Status409Conflict,
        "UserBlock.StateConflict",
        "error.user_block.state_conflict");
}
