using AutoMapper;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using SqlSugar;

namespace Radish.Service;

/// <summary>用户浏览记录服务</summary>
public class UserBrowseHistoryService : BaseService<UserBrowseHistory, UserBrowseHistoryVo>, IUserBrowseHistoryService
{
    private readonly IBaseRepository<UserBrowseHistory> _browseHistoryRepository;

    public UserBrowseHistoryService(
        IMapper mapper,
        IBaseRepository<UserBrowseHistory> baseRepository)
        : base(mapper, baseRepository)
    {
        _browseHistoryRepository = baseRepository;
    }

    /// <summary>记录浏览行为</summary>
    public async Task RecordAsync(RecordBrowseHistoryDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.UserId <= 0 || request.TargetId <= 0 || string.IsNullOrWhiteSpace(request.TargetType))
        {
            return;
        }

        var normalizedTargetType = request.TargetType.Trim();
        var normalizedTitle = NormalizeRequired(
            string.IsNullOrWhiteSpace(request.Title) ? $"{normalizedTargetType}-{request.TargetId}" : request.Title,
            200,
            $"{normalizedTargetType}-{request.TargetId}");
        var normalizedOperatorName = NormalizeRequired(request.OperatorName, 50, "System");
        var normalizedTargetSlug = NormalizeOptional(request.TargetSlug, 200);
        var normalizedSummary = NormalizeOptional(request.Summary, 500);
        var normalizedCoverImage = NormalizeOptional(request.CoverImage, 500);
        var normalizedRoutePath = NormalizeOptional(request.RoutePath, 500);
        var nowUtc = DateTime.UtcNow;
        var existing = await _browseHistoryRepository.QueryFirstAsync(history =>
            history.UserId == request.UserId &&
            history.TargetType == normalizedTargetType &&
            history.TargetId == request.TargetId &&
            !history.IsDeleted);

        if (existing == null)
        {
            await _browseHistoryRepository.AddAsync(new UserBrowseHistory
            {
                UserId = request.UserId,
                TargetType = normalizedTargetType,
                TargetId = request.TargetId,
                TargetSlug = normalizedTargetSlug,
                Title = normalizedTitle,
                Summary = normalizedSummary,
                CoverImage = normalizedCoverImage,
                RoutePath = normalizedRoutePath,
                ViewCount = 1,
                LastViewTime = nowUtc,
                TenantId = request.TenantId,
                CreateTime = DateTime.Now,
                CreateBy = normalizedOperatorName,
                CreateId = request.UserId
            });
            return;
        }

        existing.TargetSlug = normalizedTargetSlug ?? existing.TargetSlug;
        existing.Title = normalizedTitle;
        existing.Summary = normalizedSummary;
        existing.CoverImage = normalizedCoverImage;
        existing.RoutePath = normalizedRoutePath ?? existing.RoutePath;
        existing.ViewCount = Math.Max(0, existing.ViewCount) + 1;
        existing.LastViewTime = nowUtc;
        existing.ModifyTime = DateTime.Now;
        existing.ModifyBy = normalizedOperatorName;
        existing.ModifyId = request.UserId;

        await _browseHistoryRepository.UpdateAsync(existing);
    }

    /// <summary>获取当前用户浏览记录分页</summary>
    public async Task<(List<UserBrowseHistoryVo> items, int total)> GetMyPageAsync(long userId, int pageIndex, int pageSize)
    {
        var safePageIndex = pageIndex < 1 ? 1 : pageIndex;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var (items, total) = await _browseHistoryRepository.QueryPageAsync(
            history => history.UserId == userId && !history.IsDeleted,
            safePageIndex,
            safePageSize,
            history => history.LastViewTime,
            OrderByType.Desc,
            history => history.Id,
            OrderByType.Desc);

        return (Mapper.Map<List<UserBrowseHistoryVo>>(items), total);
    }

    private static string NormalizeRequired(string? value, int maxLength, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
