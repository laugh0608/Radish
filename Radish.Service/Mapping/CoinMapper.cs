using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class CoinMapper
{
    // CoinTransaction → CoinTransactionVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static CoinTransactionVo? ToCoinTransactionVo(CoinTransaction? source)
    {
        if (source is null) return null;
        var target = MapCoinTransactionToCoinTransactionVo(source);
        target.VoAmountDisplay = FormatToRadish(source.Amount);
        target.VoFeeDisplay = FormatToRadish(source.Fee);
        target.VoTransactionTypeDisplay = GetTransactionTypeName(source.TransactionType);
        target.VoStatusDisplay = GetStatusName(source.Status);
        return target;
    }

    [MapProperty(nameof(CoinTransaction.Id), nameof(CoinTransactionVo.VoId))]
    [MapProperty(nameof(CoinTransaction.TransactionNo), nameof(CoinTransactionVo.VoTransactionNo))]
    [MapProperty(nameof(CoinTransaction.FromUserId), nameof(CoinTransactionVo.VoFromUserId))]
    [MapperIgnoreTarget(nameof(CoinTransactionVo.VoFromUserName))]
    [MapProperty(nameof(CoinTransaction.ToUserId), nameof(CoinTransactionVo.VoToUserId))]
    [MapperIgnoreTarget(nameof(CoinTransactionVo.VoToUserName))]
    [MapProperty(nameof(CoinTransaction.Amount), nameof(CoinTransactionVo.VoAmount))]
    [MapperIgnoreTarget(nameof(CoinTransactionVo.VoAmountDisplay))]
    [MapProperty(nameof(CoinTransaction.Fee), nameof(CoinTransactionVo.VoFee))]
    [MapperIgnoreTarget(nameof(CoinTransactionVo.VoFeeDisplay))]
    [MapProperty(nameof(CoinTransaction.TheoreticalAmount), nameof(CoinTransactionVo.VoTheoreticalAmount))]
    [MapProperty(nameof(CoinTransaction.RoundingDiff), nameof(CoinTransactionVo.VoRoundingDiff))]
    [MapProperty(nameof(CoinTransaction.TransactionType), nameof(CoinTransactionVo.VoTransactionType))]
    [MapperIgnoreTarget(nameof(CoinTransactionVo.VoTransactionTypeDisplay))]
    [MapProperty(nameof(CoinTransaction.Status), nameof(CoinTransactionVo.VoStatus))]
    [MapperIgnoreTarget(nameof(CoinTransactionVo.VoStatusDisplay))]
    [MapProperty(nameof(CoinTransaction.BusinessType), nameof(CoinTransactionVo.VoBusinessType))]
    [MapProperty(nameof(CoinTransaction.BusinessId), nameof(CoinTransactionVo.VoBusinessId))]
    [MapProperty(nameof(CoinTransaction.Remark), nameof(CoinTransactionVo.VoRemark))]
    [MapProperty(nameof(CoinTransaction.CreateBy), nameof(CoinTransactionVo.VoCreateBy))]
    [MapProperty(nameof(CoinTransaction.CreateId), nameof(CoinTransactionVo.VoCreateId))]
    [MapProperty(nameof(CoinTransaction.CreateTime), nameof(CoinTransactionVo.VoCreateTime))]
    [UserMapping(Default = false)]
    private static partial CoinTransactionVo MapCoinTransactionToCoinTransactionVo(CoinTransaction source);

    public static partial List<CoinTransactionVo> ToCoinTransactionVoList(IEnumerable<CoinTransaction>? source);

    // UserBalance → UserBalanceVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static UserBalanceVo? ToUserBalanceVo(UserBalance? source)
    {
        if (source is null) return null;
        var target = MapUserBalanceToUserBalanceVo(source);
        target.VoBalanceDisplay = FormatToRadish(source.Balance);
        target.VoFrozenBalanceDisplay = FormatToRadish(source.FrozenBalance);
        return target;
    }

    [MapProperty(nameof(UserBalance.UserId), nameof(UserBalanceVo.VoUserId))]
    [MapperIgnoreTarget(nameof(UserBalanceVo.VoUserName))]
    [MapProperty(nameof(UserBalance.Balance), nameof(UserBalanceVo.VoBalance))]
    [MapperIgnoreTarget(nameof(UserBalanceVo.VoBalanceDisplay))]
    [MapProperty(nameof(UserBalance.FrozenBalance), nameof(UserBalanceVo.VoFrozenBalance))]
    [MapperIgnoreTarget(nameof(UserBalanceVo.VoFrozenBalanceDisplay))]
    [MapProperty(nameof(UserBalance.TotalEarned), nameof(UserBalanceVo.VoTotalEarned))]
    [MapProperty(nameof(UserBalance.TotalSpent), nameof(UserBalanceVo.VoTotalSpent))]
    [MapProperty(nameof(UserBalance.TotalTransferredIn), nameof(UserBalanceVo.VoTotalTransferredIn))]
    [MapProperty(nameof(UserBalance.TotalTransferredOut), nameof(UserBalanceVo.VoTotalTransferredOut))]
    [MapProperty(nameof(UserBalance.Version), nameof(UserBalanceVo.VoVersion))]
    [MapProperty(nameof(UserBalance.CreateTime), nameof(UserBalanceVo.VoCreateTime))]
    [MapProperty(nameof(UserBalance.ModifyTime), nameof(UserBalanceVo.VoModifyTime))]
    [UserMapping(Default = false)]
    private static partial UserBalanceVo MapUserBalanceToUserBalanceVo(UserBalance source);

    public static partial List<UserBalanceVo> ToUserBalanceVoList(IEnumerable<UserBalance>? source);

    // CoinTransactionVo → CoinTransaction
    [MapProperty(nameof(CoinTransactionVo.VoTransactionNo), nameof(CoinTransaction.TransactionNo))]
    [MapProperty(nameof(CoinTransactionVo.VoFromUserId), nameof(CoinTransaction.FromUserId))]
    [MapProperty(nameof(CoinTransactionVo.VoToUserId), nameof(CoinTransaction.ToUserId))]
    [MapProperty(nameof(CoinTransactionVo.VoAmount), nameof(CoinTransaction.Amount))]
    [MapProperty(nameof(CoinTransactionVo.VoFee), nameof(CoinTransaction.Fee))]
    [MapProperty(nameof(CoinTransactionVo.VoTheoreticalAmount), nameof(CoinTransaction.TheoreticalAmount))]
    [MapProperty(nameof(CoinTransactionVo.VoRoundingDiff), nameof(CoinTransaction.RoundingDiff))]
    [MapProperty(nameof(CoinTransactionVo.VoTransactionType), nameof(CoinTransaction.TransactionType))]
    [MapProperty(nameof(CoinTransactionVo.VoStatus), nameof(CoinTransaction.Status))]
    [MapProperty(nameof(CoinTransactionVo.VoBusinessType), nameof(CoinTransaction.BusinessType))]
    [MapProperty(nameof(CoinTransactionVo.VoBusinessId), nameof(CoinTransaction.BusinessId))]
    [MapperIgnoreTarget(nameof(CoinTransaction.RewardBusinessKey))]
    [MapProperty(nameof(CoinTransactionVo.VoRemark), nameof(CoinTransaction.Remark))]
    [MapperIgnoreTarget(nameof(CoinTransaction.TenantId))]
    [MapProperty(nameof(CoinTransactionVo.VoCreateTime), nameof(CoinTransaction.CreateTime))]
    [MapProperty(nameof(CoinTransactionVo.VoCreateBy), nameof(CoinTransaction.CreateBy))]
    [MapProperty(nameof(CoinTransactionVo.VoCreateId), nameof(CoinTransaction.CreateId))]
    [MapperIgnoreTarget(nameof(CoinTransaction.ModifyTime))]
    [MapperIgnoreTarget(nameof(CoinTransaction.ModifyBy))]
    [MapperIgnoreTarget(nameof(CoinTransaction.ModifyId))]
    [MapProperty(nameof(CoinTransactionVo.VoId), nameof(CoinTransaction.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial CoinTransaction? ToCoinTransaction(CoinTransactionVo? source);

    public static partial List<CoinTransaction> ToCoinTransactionList(IEnumerable<CoinTransactionVo>? source);

    // UserBalanceVo → UserBalance
    [MapProperty(nameof(UserBalanceVo.VoUserId), nameof(UserBalance.UserId))]
    [MapProperty(nameof(UserBalanceVo.VoBalance), nameof(UserBalance.Balance))]
    [MapProperty(nameof(UserBalanceVo.VoFrozenBalance), nameof(UserBalance.FrozenBalance))]
    [MapProperty(nameof(UserBalanceVo.VoTotalEarned), nameof(UserBalance.TotalEarned))]
    [MapProperty(nameof(UserBalanceVo.VoTotalSpent), nameof(UserBalance.TotalSpent))]
    [MapProperty(nameof(UserBalanceVo.VoTotalTransferredIn), nameof(UserBalance.TotalTransferredIn))]
    [MapProperty(nameof(UserBalanceVo.VoTotalTransferredOut), nameof(UserBalance.TotalTransferredOut))]
    [MapProperty(nameof(UserBalanceVo.VoVersion), nameof(UserBalance.Version))]
    [MapperIgnoreTarget(nameof(UserBalance.TenantId))]
    [MapProperty(nameof(UserBalanceVo.VoCreateTime), nameof(UserBalance.CreateTime))]
    [MapperIgnoreTarget(nameof(UserBalance.CreateBy))]
    [MapperIgnoreTarget(nameof(UserBalance.CreateId))]
    [MapProperty(nameof(UserBalanceVo.VoModifyTime), nameof(UserBalance.ModifyTime))]
    [MapperIgnoreTarget(nameof(UserBalance.ModifyBy))]
    [MapperIgnoreTarget(nameof(UserBalance.ModifyId))]
    [MapperIgnoreTarget(nameof(UserBalance.IsDeleted))]
    [MapperIgnoreTarget(nameof(UserBalance.DeletedAt))]
    [MapperIgnoreTarget(nameof(UserBalance.DeletedBy))]
    [MapperIgnoreTarget(nameof(UserBalance.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserBalance? ToUserBalance(UserBalanceVo? source);

    public static partial List<UserBalance> ToUserBalanceList(IEnumerable<UserBalanceVo>? source);

    private static string FormatToRadish(long carrot)
    {
        decimal radish = carrot / 1000m;
        return radish.ToString("F3");
    }
    private static string GetTransactionTypeName(string transactionType)
    {
        return transactionType switch
        {
            "SYSTEM_GRANT" => "系统赠送",
            "LIKE_REWARD" => "点赞奖励",
            "COMMENT_REWARD" => "评论奖励",
            "TRANSFER" => "用户转账",
            "TIP" => "打赏",
            "CONSUME" => "消费",
            "REFUND" => "退款",
            "PENALTY" => "惩罚扣除",
            "ADMIN_ADJUST" => "管理员调整",
            _ => transactionType
        };
    }
    private static string GetStatusName(string status)
    {
        return status switch
        {
            "PENDING" => "待处理",
            "SUCCESS" => "成功",
            "FAILED" => "失败",
            _ => status
        };
    }

}
