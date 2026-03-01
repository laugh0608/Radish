using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>回应服务接口</summary>
public interface IReactionService
{
    /// <summary>获取单目标回应汇总</summary>
    Task<List<ReactionSummaryVo>> GetSummaryAsync(string targetType, long targetId, long currentUserId = 0);

    /// <summary>批量获取多目标回应汇总</summary>
    Task<Dictionary<string, List<ReactionSummaryVo>>> BatchGetSummaryAsync(string targetType, List<long> targetIds, long currentUserId = 0);

    /// <summary>切换回应（添加/取消）</summary>
    Task<List<ReactionSummaryVo>> ToggleAsync(ToggleReactionDto request, long userId, string userName, long tenantId);
}
