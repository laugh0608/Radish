using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>萝卜币系统实体映射配置</summary>
public class CoinProfile : Profile
{
    public CoinProfile()
    {
        ConfigureUserBalanceMapping();
        ConfigureCoinTransactionMapping();
    }

    /// <summary>配置用户余额映射</summary>
    private void ConfigureUserBalanceMapping()
    {
        // UserBalance -> UserBalanceVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<UserBalance, UserBalanceVo>()
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.BalanceDisplay, opt => opt.MapFrom(src => FormatToRadish(src.Balance)))
            .ForMember(dest => dest.FrozenBalanceDisplay, opt => opt.MapFrom(src => FormatToRadish(src.FrozenBalance)));

        // UserBalanceVo -> UserBalance
        RecognizePrefixes("Vo");
        CreateMap<UserBalanceVo, UserBalance>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.UserId));
    }

    /// <summary>配置交易记录映射</summary>
    private void ConfigureCoinTransactionMapping()
    {
        // CoinTransaction -> CoinTransactionVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<CoinTransaction, CoinTransactionVo>()
            .ForMember(dest => dest.AmountDisplay, opt => opt.MapFrom(src => FormatToRadish(src.Amount)))
            .ForMember(dest => dest.FeeDisplay, opt => opt.MapFrom(src => FormatToRadish(src.Fee)))
            .ForMember(dest => dest.TransactionTypeDisplay, opt => opt.MapFrom(src => GetTransactionTypeName(src.TransactionType)))
            .ForMember(dest => dest.StatusDisplay, opt => opt.MapFrom(src => GetStatusName(src.Status)))
            .ForMember(dest => dest.FromUserName, opt => opt.Ignore()) // 需要在 Service 层单独设置
            .ForMember(dest => dest.ToUserName, opt => opt.Ignore());   // 需要在 Service 层单独设置

        // CoinTransactionVo -> CoinTransaction
        RecognizePrefixes("Vo");
        CreateMap<CoinTransactionVo, CoinTransaction>();
    }

    /// <summary>
    /// 格式化为白萝卜显示
    /// </summary>
    /// <param name="carrot">胡萝卜数量</param>
    /// <returns>白萝卜格式化字符串（保留3位小数）</returns>
    private static string FormatToRadish(long carrot)
    {
        decimal radish = carrot / 1000m;
        return radish.ToString("F3"); // 保留3位小数
    }

    /// <summary>
    /// 获取交易类型中文名称
    /// </summary>
    /// <param name="transactionType">交易类型代码</param>
    /// <returns>交易类型中文名称</returns>
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

    /// <summary>
    /// 获取交易状态中文名称
    /// </summary>
    /// <param name="status">交易状态代码</param>
    /// <returns>交易状态中文名称</returns>
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
