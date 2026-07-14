using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

/// <summary>公开身份装饰只读服务。</summary>
public sealed class UserAdornmentService : IUserAdornmentService
{
    private static readonly BenefitType[] SupportedAdornmentTypes =
    [
        BenefitType.Badge,
        BenefitType.Title
    ];

    private readonly IUserBenefitRepository _userBenefitRepository;
    private readonly IBaseRepository<UserBenefit> _benefitRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly TimeProvider _timeProvider;

    public UserAdornmentService(
        IUserBenefitRepository userBenefitRepository,
        IBaseRepository<UserBenefit> benefitRepository,
        IBaseRepository<Attachment> attachmentRepository,
        IAttachmentUrlResolver attachmentUrlResolver,
        TimeProvider timeProvider)
    {
        _userBenefitRepository = userBenefitRepository;
        _benefitRepository = benefitRepository;
        _attachmentRepository = attachmentRepository;
        _attachmentUrlResolver = attachmentUrlResolver;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyDictionary<long, UserAdornmentVo>> GetUserAdornmentsAsync(
        IReadOnlyCollection<long> userIds)
    {
        var normalizedUserIds = userIds.Where(userId => userId > 0).Distinct().ToList();
        if (normalizedUserIds.Count == 0)
        {
            return new Dictionary<long, UserAdornmentVo>();
        }

        var selections = await _userBenefitRepository.GetActiveSelectionsAsync(
            normalizedUserIds,
            SupportedAdornmentTypes);
        if (selections.Count == 0)
        {
            return new Dictionary<long, UserAdornmentVo>();
        }

        var selectedBenefitIds = selections
            .Select(selection => selection.BenefitId)
            .Where(benefitId => benefitId > 0)
            .Distinct()
            .ToList();
        var benefits = await _benefitRepository.QueryAsync(benefit =>
            selectedBenefitIds.Contains(benefit.Id) &&
            normalizedUserIds.Contains(benefit.UserId) &&
            !benefit.IsDeleted);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var validBenefits = benefits
            .Where(benefit =>
                SupportedAdornmentTypes.Contains(benefit.BenefitType) &&
                benefit.EffectiveAt <= nowUtc &&
                !benefit.RevokedAt.HasValue &&
                (!benefit.ExpiresAt.HasValue || benefit.ExpiresAt.Value > nowUtc))
            .GroupBy(benefit => benefit.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var attachmentIds = validBenefits.Values
            .Select(benefit => benefit.BenefitIconAttachmentId)
            .Where(attachmentId => attachmentId is > 0)
            .Select(attachmentId => attachmentId!.Value)
            .Distinct()
            .ToList();
        var publicAttachmentIds = attachmentIds.Count == 0
            ? new HashSet<long>()
            : (await _attachmentRepository.QueryAsync(attachment =>
                    attachmentIds.Contains(attachment.Id) &&
                    attachment.IsPublic &&
                    attachment.IsEnabled &&
                    !attachment.IsDeleted &&
                    (attachment.AuditStatus == null || attachment.AuditStatus != "Reject")))
                .Select(attachment => attachment.Id)
                .ToHashSet();

        Dictionary<long, UserAdornmentVo> result = new();
        foreach (var selection in selections)
        {
            if (!validBenefits.TryGetValue(selection.BenefitId, out var benefit) ||
                benefit.UserId != selection.UserId ||
                benefit.BenefitType != selection.BenefitType)
            {
                continue;
            }

            var item = BuildAdornmentItem(benefit, publicAttachmentIds);
            if (item == null)
            {
                continue;
            }

            if (!result.TryGetValue(selection.UserId, out var adornment))
            {
                adornment = new UserAdornmentVo();
                result[selection.UserId] = adornment;
            }

            if (benefit.BenefitType == BenefitType.Badge)
            {
                adornment.VoBadge = item;
            }
            else if (benefit.BenefitType == BenefitType.Title)
            {
                adornment.VoTitle = item;
            }
        }

        return result;
    }

    public async Task<UserAdornmentVo?> GetUserAdornmentAsync(long userId)
    {
        if (userId <= 0)
        {
            return null;
        }

        var adornments = await GetUserAdornmentsAsync([userId]);
        return adornments.GetValueOrDefault(userId);
    }

    private UserAdornmentItemVo? BuildAdornmentItem(
        UserBenefit benefit,
        IReadOnlySet<long> publicAttachmentIds)
    {
        var resourceKey = benefit.BenefitValue.Trim();
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return null;
        }

        string? imageUrl = null;
        if (benefit.BenefitIconAttachmentId is > 0 &&
            publicAttachmentIds.Contains(benefit.BenefitIconAttachmentId.Value))
        {
            imageUrl = _attachmentUrlResolver.ResolveAttachmentUrl(benefit.BenefitIconAttachmentId.Value);
        }

        if (benefit.BenefitType == BenefitType.Badge && string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        return new UserAdornmentItemVo
        {
            VoResourceKey = resourceKey,
            VoName = benefit.BenefitType == BenefitType.Title
                ? resourceKey
                : string.IsNullOrWhiteSpace(benefit.BenefitName)
                    ? resourceKey
                    : benefit.BenefitName.Trim(),
            VoImageUrl = imageUrl
        };
    }
}
