using System.Globalization;
using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>Console 频道匿名公开摘要资格治理服务。</summary>
public sealed class ChannelDiscoverabilityService : IChannelDiscoverabilityService
{
    private readonly IChannelDiscoverabilityRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ChannelDiscoverabilityService(
        IChannelDiscoverabilityRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<PageModel<ChannelDiscoverabilityVo>> GetPageAsync(
        long tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        ChannelDiscoverVisibility? discoverVisibility,
        bool? isEnabled,
        bool includeDeleted)
    {
        if (tenantId < 0)
        {
            throw InvalidArgument();
        }

        EnsureVisibilityValue(discoverVisibility);
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var normalizedKeyword = NormalizeText(keyword, 100);
        var (items, total) = await _repository.QueryPageAsync(new ChannelDiscoverabilityPageQuery(
            tenantId,
            safePageIndex,
            safePageSize,
            normalizedKeyword,
            discoverVisibility,
            isEnabled,
            includeDeleted));

        return new PageModel<ChannelDiscoverabilityVo>
        {
            Page = safePageIndex,
            PageSize = safePageSize,
            DataCount = total,
            PageCount = (int)Math.Ceiling(total / (double)safePageSize),
            Data = items.Select(MapChannel).ToList()
        };
    }

    public async Task<ChannelDiscoverabilityVo> GetByIdAsync(long tenantId, long channelId)
    {
        if (tenantId < 0 || channelId <= 0)
        {
            throw InvalidArgument();
        }

        var channel = await _repository.QueryByIdAsync(tenantId, channelId);
        return channel == null ? throw TargetUnavailable() : MapChannel(channel);
    }

    public async Task<PageModel<ChannelDiscoverVisibilityEventVo>> GetHistoryAsync(
        long tenantId,
        long channelId,
        int pageIndex,
        int pageSize)
    {
        if (tenantId < 0 || channelId <= 0)
        {
            throw InvalidArgument();
        }

        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        try
        {
            var (items, total) = await _repository.QueryHistoryAsync(new ChannelDiscoverVisibilityHistoryQuery(
                tenantId,
                channelId,
                safePageIndex,
                safePageSize));
            return new PageModel<ChannelDiscoverVisibilityEventVo>
            {
                Page = safePageIndex,
                PageSize = safePageSize,
                DataCount = total,
                PageCount = (int)Math.Ceiling(total / (double)safePageSize),
                Data = items.Select(MapEvent).ToList()
            };
        }
        catch (ChannelDiscoverabilityTargetUnavailableException)
        {
            throw TargetUnavailable();
        }
    }

    public async Task<ChannelDiscoverVisibilityMutationVo> UpdateVisibilityAsync(
        long tenantId,
        long channelId,
        long actorUserId,
        string actorName,
        UpdateChannelDiscoverVisibilityDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (tenantId < 0 || channelId <= 0 || actorUserId <= 0 || request.ExpectedVersion < 0)
        {
            throw InvalidArgument();
        }

        EnsureVisibilityValue(request.DiscoverVisibility);
        var normalizedReason = request.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            throw new BusinessException(
                "修改频道公开摘要资格必须填写原因",
                StatusCodes.Status400BadRequest,
                "ChannelDiscoverability.ReasonRequired",
                "error.channel_discoverability.reason_required");
        }

        if (normalizedReason.Length > 500)
        {
            throw InvalidArgument();
        }

        try
        {
            var result = await _repository.SetVisibilityAsync(new ChannelDiscoverVisibilityChangeCommand(
                tenantId,
                channelId,
                request.DiscoverVisibility,
                request.ExpectedVersion,
                normalizedReason,
                actorUserId,
                NormalizeActorName(actorName),
                _timeProvider.GetUtcNow().UtcDateTime));
            return new ChannelDiscoverVisibilityMutationVo
            {
                VoChannel = MapChannel(result.Channel),
                VoChanged = result.Changed
            };
        }
        catch (ChannelDiscoverabilityTargetUnavailableException)
        {
            throw TargetUnavailable();
        }
        catch (ChannelDiscoverabilityStateConflictException)
        {
            throw new BusinessException(
                "频道公开摘要状态已变化，请刷新后重试",
                StatusCodes.Status409Conflict,
                "ChannelDiscoverability.VersionConflict",
                "error.channel_discoverability.version_conflict");
        }
        catch (ChannelDiscoverabilityIneligibleException exception)
        {
            throw new BusinessException(
                $"频道当前不满足匿名公开摘要资格：{string.Join(",", exception.Issues)}",
                StatusCodes.Status409Conflict,
                "ChannelDiscoverability.NotEligible",
                "error.channel_discoverability.not_eligible",
                exception.Issues.Cast<object>().ToArray());
        }
    }

    private static ChannelDiscoverabilityVo MapChannel(Channel channel)
    {
        var issues = ChannelDiscoverabilityPolicy.GetSummaryEligibilityIssues(channel);
        return new ChannelDiscoverabilityVo
        {
            VoChannelId = channel.Id.ToString(CultureInfo.InvariantCulture),
            VoName = channel.Name,
            VoSlug = channel.Slug,
            VoDescription = channel.Description,
            VoIconEmoji = channel.IconEmoji,
            VoType = channel.Type,
            VoIsEnabled = channel.IsEnabled,
            VoIsDeleted = channel.IsDeleted,
            VoDiscoverVisibility = channel.DiscoverVisibility,
            VoDiscoverVisibilityVersion = channel.DiscoverVisibilityVersion,
            VoCanEnableSummary = issues.Count == 0,
            VoEligibilityIssues = issues,
            VoLastMessageTime = channel.LastMessageTime,
            VoModifyTime = channel.ModifyTime,
            VoModifyBy = channel.ModifyBy
        };
    }

    private static ChannelDiscoverVisibilityEventVo MapEvent(ChannelDiscoverVisibilityEvent change)
    {
        return new ChannelDiscoverVisibilityEventVo
        {
            VoId = change.Id.ToString(CultureInfo.InvariantCulture),
            VoChannelId = change.ChannelId.ToString(CultureInfo.InvariantCulture),
            VoFromVisibility = change.FromVisibility,
            VoToVisibility = change.ToVisibility,
            VoExpectedVersion = change.ExpectedVersion,
            VoResultVersion = change.ResultVersion,
            VoReason = change.Reason,
            VoActorUserId = change.ActorUserId.ToString(CultureInfo.InvariantCulture),
            VoActorName = change.ActorName,
            VoCreateTime = change.CreateTime
        };
    }

    private static void EnsureVisibilityValue(ChannelDiscoverVisibility? visibility)
    {
        if (visibility.HasValue && visibility.Value is not (
                ChannelDiscoverVisibility.Hidden or ChannelDiscoverVisibility.Summary))
        {
            throw InvalidArgument();
        }
    }

    private static string NormalizeText(string? value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string NormalizeActorName(string? actorName)
    {
        var normalized = NormalizeText(actorName, 50);
        return string.IsNullOrWhiteSpace(normalized) ? "System" : normalized;
    }

    private static BusinessException InvalidArgument() => new(
        "频道公开摘要请求无效",
        StatusCodes.Status400BadRequest,
        "ChannelDiscoverability.InvalidArgument",
        "error.channel_discoverability.invalid_argument");

    private static BusinessException TargetUnavailable() => new(
        "频道不存在或不在当前租户的公开摘要治理范围内",
        StatusCodes.Status404NotFound,
        "ChannelDiscoverability.TargetUnavailable",
        "error.channel_discoverability.target_unavailable");
}
