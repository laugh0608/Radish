using Radish.Model;

namespace Radish.IRepository;

/// <summary>持续权益选择与失效的原子持久化边界。</summary>
public interface IUserBenefitRepository
{
    Task<List<UserActiveBenefit>> GetActiveSelectionsAsync(
        IReadOnlyCollection<long> userIds,
        IReadOnlyCollection<Radish.Shared.CustomEnum.BenefitType> benefitTypes);

    Task<List<UserActiveBenefit>> GetActiveSelectionsAsync(long userId);

    Task<UserActiveBenefit?> GetActiveSelectionAsync(long userId, Radish.Shared.CustomEnum.BenefitType benefitType);

    Task<List<long>> GetDueBenefitIdsAsync(DateTime nowUtc, int take);

    Task<UserBenefitPersistenceResult> ActivateAsync(
        long userId,
        long benefitId,
        long operatorId,
        string operatorName,
        DateTime nowUtc);

    Task<UserBenefitPersistenceResult> DeactivateAsync(
        long userId,
        long benefitId,
        long operatorId,
        string operatorName,
        DateTime nowUtc);

    Task<UserBenefitPersistenceResult> RevokeAsync(
        long benefitId,
        string reason,
        long operatorId,
        string operatorName,
        DateTime nowUtc);

    Task<UserBenefitPersistenceResult?> ExpireAsync(long benefitId, DateTime nowUtc);
}

/// <summary>权益状态持久化结果。</summary>
public sealed record UserBenefitPersistenceResult(
    UserBenefit Benefit,
    bool Changed,
    long? CurrentBenefitId,
    long? PreviousBenefitId);
