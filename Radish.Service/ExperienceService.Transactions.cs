using Radish.Common;
using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;
using SqlSugar;
using System.Linq.Expressions;

namespace Radish.Service;

public partial class ExperienceService
{
    #region 交易记录查询

    /// <summary>
    /// 获取用户经验值交易记录（分页）
    /// </summary>
    public async Task<PageModel<ExpTransactionVo>> GetTransactionsAsync(
        long userId,
        int pageIndex,
        int pageSize,
        string? expType = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            var safePageIndex = NormalizePositivePageIndex(pageIndex);
            var safePageSize = NormalizeTransactionPageSize(pageSize);
            var normalizedExpTypes = NormalizeOptionalFilterValues(expType);

            // 构建动态 Where 条件（使用 And 扩展方法组合多个条件）
            Expression<Func<ExpTransaction, bool>> whereExpression = t => t.UserId == userId;

            // 如果有 expType 筛选条件
            if (normalizedExpTypes.Count > 0)
            {
                whereExpression = whereExpression.And(t => normalizedExpTypes.Contains(t.ExpType));
            }

            // 如果有开始日期筛选条件
            if (startDate.HasValue)
            {
                whereExpression = whereExpression.And(t => t.CreateTime >= startDate.Value);
            }

            // 如果有结束日期筛选条件
            if (endDate.HasValue)
            {
                whereExpression = whereExpression.And(t => t.CreateTime <= endDate.Value);
            }

            // 使用 BaseRepository 的分页查询方法（数据库层面筛选、排序和分页）
            var (pagedData, totalCount) = await _expTransactionRepository.QueryPageAsync(
                whereExpression: whereExpression,
                pageIndex: safePageIndex,
                pageSize: safePageSize,
                orderByExpression: t => t.CreateTime,
                orderByType: OrderByType.Desc
            );

            // 映射为 VO
            var transactions = Mapper.Map<List<ExpTransactionVo>>(pagedData);
            await FillTransactionUserNamesAsync(pagedData, transactions);

            var pageCount = (int)Math.Ceiling(totalCount / (double)safePageSize);

            return new PageModel<ExpTransactionVo>
            {
                Page = safePageIndex,
                PageSize = safePageSize,
                DataCount = totalCount,
                PageCount = pageCount,
                Data = transactions
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 交易记录失败", userId);
            throw;
        }
    }

    private async Task FillTransactionUserNamesAsync(
        IReadOnlyList<ExpTransaction> sourceTransactions,
        IReadOnlyList<ExpTransactionVo> transactionVos)
    {
        if (sourceTransactions.Count == 0 || transactionVos.Count == 0)
        {
            return;
        }

        var userIds = sourceTransactions
            .Select(transaction => transaction.UserId)
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
        {
            return;
        }

        var users = await _userRepository.QueryAsync(user => userIds.Contains(user.Id) && !user.IsDeleted);
        var userNameMap = users.ToDictionary(user => user.Id, user => BuildExperienceUserDisplayName(user));

        for (var index = 0; index < sourceTransactions.Count && index < transactionVos.Count; index++)
        {
            if (userNameMap.TryGetValue(sourceTransactions[index].UserId, out var userName))
            {
                transactionVos[index].VoUserName = userName;
            }
        }
    }

    private static int NormalizePositivePageIndex(int pageIndex)
    {
        return pageIndex > 0 ? pageIndex : 1;
    }

    private static string BuildExperienceUserDisplayName(User user)
    {
        return User.BuildDisplayHandle(user.UserName, user.PublicIndex, user.Id)
            ?? User.NormalizeDisplayName(user.UserName, user.Id);
    }

    private static int NormalizeTransactionPageSize(int pageSize)
    {
        if (pageSize <= 0)
        {
            return 20;
        }

        return Math.Min(pageSize, MaxTransactionPageSize);
    }

    private static List<string> NormalizeOptionalFilterValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    #endregion
}
