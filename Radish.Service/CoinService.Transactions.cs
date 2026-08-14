using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;
using SqlSugar;

namespace Radish.Service;

public partial class CoinService
{
    /// <summary>按用户和稳定筛选分页读取资产流水。</summary>
    public async Task<PageModel<CoinTransactionVo>> GetTransactionsAsync(
        long userId,
        int pageIndex,
        int pageSize,
        string? transactionType = null,
        string? status = null,
        string? businessType = null,
        long? businessId = null)
    {
        try
        {
            if (pageIndex <= 0 || pageSize <= 0 || pageSize > 100)
            {
                throw new ArgumentException("分页参数无效");
            }

            await EnsureUserExistsAsync(userId);
            var whereExpression = Expressionable.Create<CoinTransaction>()
                .And(transaction => transaction.FromUserId == userId || transaction.ToUserId == userId);

            if (!string.IsNullOrWhiteSpace(transactionType))
            {
                whereExpression.And(transaction => transaction.TransactionType == transactionType);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                whereExpression.And(transaction => transaction.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(businessType))
            {
                whereExpression.And(transaction => transaction.BusinessType == businessType);
            }

            if (businessId.HasValue)
            {
                whereExpression.And(transaction => transaction.BusinessId == businessId.Value);
            }

            var (transactions, totalCount) = await _coinTransactionRepository.QueryPageAsync(
                whereExpression.ToExpression(),
                pageIndex,
                pageSize,
                transaction => transaction.CreateTime,
                OrderByType.Desc,
                transaction => transaction.Id,
                OrderByType.Desc);
            var transactionVos = Mapper.Map<List<CoinTransactionVo>>(transactions);
            await FillCoinTransactionUserNamesAsync(transactionVos);

            return new PageModel<CoinTransactionVo>
            {
                Page = pageIndex,
                PageSize = pageSize,
                DataCount = totalCount,
                PageCount = (int)Math.Ceiling(totalCount / (double)pageSize),
                Data = transactionVos
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 交易记录失败", userId);
            throw;
        }
    }

    /// <summary>按流水号读取交易详情。</summary>
    public async Task<CoinTransactionVo?> GetTransactionByNoAsync(string transactionNo)
    {
        try
        {
            var transaction = await _coinTransactionRepository.QueryFirstAsync(
                item => item.TransactionNo == transactionNo);
            if (transaction == null)
            {
                return null;
            }

            var transactionVo = Mapper.Map<CoinTransactionVo>(transaction);
            await FillCoinTransactionUserNamesAsync([transactionVo]);
            return transactionVo;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "根据交易流水号 {TransactionNo} 获取交易详情失败", transactionNo);
            throw;
        }
    }

    private async Task FillCoinTransactionUserNamesAsync(IReadOnlyCollection<CoinTransactionVo> transactionVos)
    {
        if (transactionVos.Count == 0)
        {
            return;
        }

        var userIds = transactionVos
            .SelectMany(transaction => new[] { transaction.VoFromUserId, transaction.VoToUserId })
            .Where(userId => userId is > 0)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToList();
        if (userIds.Count == 0)
        {
            foreach (var transaction in transactionVos)
            {
                transaction.VoFromUserName = "系统";
                transaction.VoToUserName = "系统";
            }
            return;
        }

        var users = await _userRepository.QueryAsync(user => userIds.Contains(user.Id) && !user.IsDeleted)
            ?? [];
        var displayNames = users.ToDictionary(
            user => user.Id,
            user => User.BuildDisplayHandle(user.UserName, user.PublicIndex, user.Id)
                ?? User.NormalizeDisplayName(user.UserName, user.Id));
        foreach (var transaction in transactionVos)
        {
            transaction.VoFromUserName = ResolveCoinTransactionUserName(transaction.VoFromUserId, displayNames);
            transaction.VoToUserName = ResolveCoinTransactionUserName(transaction.VoToUserId, displayNames);
        }
    }

    private static string ResolveCoinTransactionUserName(
        long? userId,
        IReadOnlyDictionary<long, string> displayNames)
    {
        if (!userId.HasValue)
        {
            return "系统";
        }

        return displayNames.TryGetValue(userId.Value, out var userName)
            ? userName
            : $"用户{userId.Value}";
    }
}
