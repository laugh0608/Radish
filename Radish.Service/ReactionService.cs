using AutoMapper;
using Microsoft.Extensions.Logging;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;

namespace Radish.Service;

/// <summary>回应服务实现</summary>
public class ReactionService : BaseService<Reaction, ReactionSummaryVo>, IReactionService
{
    private const int MaxBatchTargetCount = 100;
    private const int MaxUserReactionTypesPerTarget = 10;

    private readonly IBaseRepository<Reaction> _reactionRepository;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IBaseRepository<StickerGroup> _stickerGroupRepository;
    private readonly IBaseRepository<Sticker> _stickerRepository;
    private readonly ILogger<ReactionService> _logger;

    public ReactionService(
        IMapper mapper,
        IBaseRepository<Reaction> baseRepository,
        IBaseRepository<Post> postRepository,
        IBaseRepository<Comment> commentRepository,
        IBaseRepository<StickerGroup> stickerGroupRepository,
        IBaseRepository<Sticker> stickerRepository,
        ILogger<ReactionService> logger)
        : base(mapper, baseRepository)
    {
        _reactionRepository = baseRepository;
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _stickerGroupRepository = stickerGroupRepository;
        _stickerRepository = stickerRepository;
        _logger = logger;
    }

    public async Task<List<ReactionSummaryVo>> GetSummaryAsync(string targetType, long targetId, long currentUserId = 0)
    {
        var normalizedTargetType = NormalizeTargetTypeOrThrow(targetType);
        if (targetId <= 0)
        {
            throw new BusinessException("targetId 必须大于0", 400, "InvalidArgument");
        }

        var reactions = await _reactionRepository.QueryAsync(
            r => r.TargetType == normalizedTargetType
                 && r.TargetId == targetId
                 && !r.IsDeleted);

        return BuildSummary(reactions, currentUserId);
    }

    public async Task<Dictionary<string, List<ReactionSummaryVo>>> BatchGetSummaryAsync(
        string targetType,
        List<long> targetIds,
        long currentUserId = 0)
    {
        var normalizedTargetType = NormalizeTargetTypeOrThrow(targetType);
        if (targetIds == null || targetIds.Count == 0)
        {
            throw new BusinessException("targetIds 不能为空", 400, "InvalidArgument");
        }

        var normalizedTargetIds = targetIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (normalizedTargetIds.Count == 0)
        {
            throw new BusinessException("targetIds 必须包含有效目标", 400, "InvalidArgument");
        }

        if (normalizedTargetIds.Count > MaxBatchTargetCount)
        {
            throw new BusinessException($"单次最多查询 {MaxBatchTargetCount} 个目标", 400, "BatchSizeExceeded");
        }

        var result = normalizedTargetIds.ToDictionary(
            id => id.ToString(),
            _ => new List<ReactionSummaryVo>());

        var reactions = await _reactionRepository.QueryAsync(
            r => r.TargetType == normalizedTargetType
                 && normalizedTargetIds.Contains(r.TargetId)
                 && !r.IsDeleted);

        if (reactions.Count == 0)
        {
            return result;
        }

        var groupedByTarget = reactions
            .GroupBy(r => r.TargetId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var targetId in normalizedTargetIds)
        {
            if (!groupedByTarget.TryGetValue(targetId, out var targetReactions))
            {
                continue;
            }

            result[targetId.ToString()] = BuildSummary(targetReactions, currentUserId);
        }

        return result;
    }

    [UseTran(Propagation = Propagation.Required)]
    public async Task<List<ReactionSummaryVo>> ToggleAsync(ToggleReactionDto request, long userId, string userName, long tenantId)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (userId <= 0)
        {
            throw new BusinessException("未登录", 401, "AuthRequired");
        }

        var normalizedTargetType = NormalizeTargetTypeOrThrow(request.TargetType);
        if (request.TargetId <= 0)
        {
            throw new BusinessException("targetId 必须大于0", 400, "InvalidArgument");
        }

        var normalizedEmojiType = NormalizeEmojiTypeOrThrow(request.EmojiType);
        var normalizedEmojiValue = NormalizeEmojiValueOrThrow(normalizedEmojiType, request.EmojiValue);
        var normalizedUserName = NormalizeUserName(userName);

        await EnsureTargetExistsAsync(normalizedTargetType, request.TargetId);

        string? thumbnailUrl = null;
        if (normalizedEmojiType == "sticker")
        {
            thumbnailUrl = await ResolveStickerThumbnailAsync(tenantId, normalizedEmojiValue);
        }

        try
        {
            await ToggleCoreAsync(
                normalizedTargetType,
                request.TargetId,
                normalizedEmojiType,
                normalizedEmojiValue,
                thumbnailUrl,
                userId,
                normalizedUserName);
        }
        catch (Exception ex) when (IsUniqueConstraintConflict(ex))
        {
            _logger.LogWarning(ex,
                "Reaction 并发冲突，准备重试一次。targetType={TargetType}, targetId={TargetId}, userId={UserId}, emojiValue={EmojiValue}",
                normalizedTargetType, request.TargetId, userId, normalizedEmojiValue);

            try
            {
                await ToggleCoreAsync(
                    normalizedTargetType,
                    request.TargetId,
                    normalizedEmojiType,
                    normalizedEmojiValue,
                    thumbnailUrl,
                    userId,
                    normalizedUserName);
            }
            catch (Exception retryEx) when (IsUniqueConstraintConflict(retryEx))
            {
                throw new BusinessException("并发冲突，请稍后重试", 409, "ConcurrentConflict");
            }
        }

        return await GetSummaryAsync(normalizedTargetType, request.TargetId, userId);
    }

    private async Task ToggleCoreAsync(
        string targetType,
        long targetId,
        string emojiType,
        string emojiValue,
        string? thumbnailUrl,
        long userId,
        string userName)
    {
        var existing = await _reactionRepository.QueryFirstAsync(
            r => r.UserId == userId
                 && r.TargetType == targetType
                 && r.TargetId == targetId
                 && r.EmojiValue == emojiValue
                 && !r.IsDeleted);

        if (existing != null)
        {
            await _reactionRepository.UpdateColumnsAsync(
                r => new Reaction
                {
                    IsDeleted = true,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = userName,
                    ModifyTime = DateTime.UtcNow,
                    ModifyBy = userName,
                    ModifyId = userId
                },
                r => r.Id == existing.Id);
            return;
        }

        var deleted = await _reactionRepository.QueryFirstAsync(
            r => r.UserId == userId
                 && r.TargetType == targetType
                 && r.TargetId == targetId
                 && r.EmojiValue == emojiValue
                 && r.IsDeleted);

        await EnsureUserReactionLimitAsync(userId, targetType, targetId);

        if (deleted != null)
        {
            await _reactionRepository.UpdateColumnsAsync(
                r => new Reaction
                {
                    IsDeleted = false,
                    DeletedAt = null,
                    DeletedBy = null,
                    EmojiType = emojiType,
                    ThumbnailUrl = thumbnailUrl,
                    UserName = userName,
                    ModifyTime = DateTime.UtcNow,
                    ModifyBy = userName,
                    ModifyId = userId
                },
                r => r.Id == deleted.Id);
            return;
        }

        var entity = new Reaction
        {
            UserId = userId,
            UserName = userName,
            TargetType = targetType,
            TargetId = targetId,
            EmojiType = emojiType,
            EmojiValue = emojiValue,
            ThumbnailUrl = thumbnailUrl,
            IsDeleted = false,
            CreateTime = DateTime.UtcNow,
            CreateBy = userName,
            CreateId = userId
        };

        await _reactionRepository.AddAsync(entity);
    }

    private async Task EnsureTargetExistsAsync(string targetType, long targetId)
    {
        switch (targetType)
        {
            case "Post":
            {
                var post = await _postRepository.QueryFirstAsync(
                    p => p.Id == targetId
                         && p.IsEnabled
                         && p.IsPublished
                         && !p.IsDeleted);
                if (post == null)
                {
                    throw new BusinessException("目标内容不存在或不可访问", 404, "TargetNotFound");
                }

                break;
            }
            case "Comment":
            {
                var comment = await _commentRepository.QueryFirstAsync(
                    c => c.Id == targetId
                         && c.IsEnabled
                         && !c.IsDeleted);
                if (comment == null)
                {
                    throw new BusinessException("目标内容不存在或不可访问", 404, "TargetNotFound");
                }

                break;
            }
            default:
                throw new BusinessException("当前仅支持 Post/Comment 回应", 400, "InvalidArgument");
        }
    }

    private async Task EnsureUserReactionLimitAsync(long userId, string targetType, long targetId)
    {
        var activeCount = await _reactionRepository.QueryCountAsync(
            r => r.UserId == userId
                 && r.TargetType == targetType
                 && r.TargetId == targetId
                 && !r.IsDeleted);

        if (activeCount >= MaxUserReactionTypesPerTarget)
        {
            throw new BusinessException(
                $"你已对该内容添加了 {MaxUserReactionTypesPerTarget} 种回应（上限）",
                400,
                "ReactionLimitExceeded");
        }
    }

    private async Task<string?> ResolveStickerThumbnailAsync(long tenantId, string emojiValue)
    {
        if (!TryParseStickerEmojiValue(emojiValue, out var groupCode, out var stickerCode))
        {
            throw new BusinessException("sticker emojiValue 格式无效", 400, "InvalidArgument");
        }

        var normalizedTenantId = NormalizeTenantId(tenantId);
        var group = await _stickerGroupRepository.QueryFirstAsync(
            g => (normalizedTenantId <= 0
                    ? g.TenantId == 0
                    : (g.TenantId == normalizedTenantId || g.TenantId == 0))
                 && g.Code == groupCode
                 && g.IsEnabled
                 && !g.IsDeleted);

        if (group == null)
        {
            throw new BusinessException("表情不存在或已禁用", 404, "StickerNotAvailable");
        }

        var sticker = await _stickerRepository.QueryFirstAsync(
            s => s.GroupId == group.Id
                 && s.Code == stickerCode
                 && s.IsEnabled
                 && !s.IsDeleted);

        if (sticker == null)
        {
            throw new BusinessException("表情不存在或已禁用", 404, "StickerNotAvailable");
        }

        return sticker.ThumbnailUrl;
    }

    private static List<ReactionSummaryVo> BuildSummary(IEnumerable<Reaction> reactions, long currentUserId)
    {
        return reactions
            .Where(r => !r.IsDeleted)
            .GroupBy(r => new { r.EmojiType, r.EmojiValue })
            .Select(g => new ReactionSummaryVo
            {
                VoEmojiType = g.Key.EmojiType,
                VoEmojiValue = g.Key.EmojiValue,
                VoCount = g.Count(),
                VoIsReacted = currentUserId > 0 && g.Any(x => x.UserId == currentUserId),
                VoThumbnailUrl = g
                    .Select(x => x.ThumbnailUrl)
                    .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url))
            })
            .OrderByDescending(x => x.VoCount)
            .ThenBy(x => x.VoEmojiValue)
            .ToList();
    }

    private static string NormalizeTargetTypeOrThrow(string targetType)
    {
        var value = targetType?.Trim().ToLowerInvariant();
        return value switch
        {
            "post" => "Post",
            "comment" => "Comment",
            _ => throw new BusinessException("targetType 仅支持 Post/Comment", 400, "InvalidArgument")
        };
    }

    private static string NormalizeEmojiTypeOrThrow(string emojiType)
    {
        var value = emojiType?.Trim().ToLowerInvariant();
        return value switch
        {
            "unicode" => "unicode",
            "sticker" => "sticker",
            _ => throw new BusinessException("emojiType 仅支持 unicode/sticker", 400, "InvalidArgument")
        };
    }

    private static string NormalizeEmojiValueOrThrow(string emojiType, string emojiValue)
    {
        var normalized = emojiValue?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessException("emojiValue 不能为空", 400, "InvalidArgument");
        }

        if (emojiType == "unicode")
        {
            return normalized;
        }

        if (!TryParseStickerEmojiValue(normalized, out var groupCode, out var stickerCode))
        {
            throw new BusinessException("sticker emojiValue 格式应为 groupCode/stickerCode", 400, "InvalidArgument");
        }

        return $"{groupCode}/{stickerCode}";
    }

    private static bool TryParseStickerEmojiValue(string emojiValue, out string groupCode, out string stickerCode)
    {
        groupCode = string.Empty;
        stickerCode = string.Empty;

        var segments = emojiValue.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        groupCode = segments[0].Trim().ToLowerInvariant();
        stickerCode = segments[1].Trim().ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(groupCode) && !string.IsNullOrWhiteSpace(stickerCode);
    }

    private static long NormalizeTenantId(long tenantId) => tenantId > 0 ? tenantId : 0;

    private static string NormalizeUserName(string userName) =>
        string.IsNullOrWhiteSpace(userName) ? "System" : userName.Trim();

    private static bool IsUniqueConstraintConflict(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
               || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
               || message.Contains("idx_reaction_user_target_emoji", StringComparison.OrdinalIgnoreCase);
    }
}
