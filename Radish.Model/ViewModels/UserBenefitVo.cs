using Radish.Shared.CustomEnum;

namespace Radish.Model.ViewModels;

/// <summary>用户权益视图模型</summary>
public class UserBenefitVo
{
    /// <summary>权益 ID</summary>
    public long VoId { get; set; }

    /// <summary>用户 ID</summary>
    public long VoUserId { get; set; }

    /// <summary>权益类型</summary>
    public BenefitType VoBenefitType { get; set; }

    /// <summary>权益类型显示名称</summary>
    public string VoBenefitTypeDisplay => VoBenefitType switch
    {
        BenefitType.Badge => "徽章",
        BenefitType.AvatarFrame => "头像框",
        BenefitType.Title => "称号",
        BenefitType.Theme => "主题",
        BenefitType.Signature => "签名档",
        BenefitType.NameColor => "用户名颜色",
        BenefitType.LikeEffect => "点赞特效",
        _ => "未知"
    };

    /// <summary>权益值</summary>
    public string VoBenefitValue { get; set; } = string.Empty;

    /// <summary>权益名称</summary>
    public string? VoBenefitName { get; set; }

    /// <summary>权益图标</summary>
    public string? VoBenefitIcon { get; set; }

    /// <summary>来源类型</summary>
    public string VoSourceType { get; set; } = string.Empty;

    /// <summary>来源类型显示名称</summary>
    public string VoSourceTypeDisplay => VoSourceType switch
    {
        "Purchase" => "购买",
        "System" => "系统赠送",
        "Activity" => "活动奖励",
        "Gift" => "礼物",
        _ => "未知"
    };

    /// <summary>有效期类型</summary>
    public DurationType VoDurationType { get; set; }

    /// <summary>生效时间</summary>
    public DateTime VoEffectiveAt { get; set; }

    /// <summary>到期时间</summary>
    public DateTime? VoExpiresAt { get; set; }

    /// <summary>是否已过期</summary>
    public bool VoIsExpired { get; set; }

    /// <summary>是否激活使用中</summary>
    public bool VoIsActive { get; set; }

    /// <summary>有效期显示文本</summary>
    public string VoDurationDisplay
    {
        get
        {
            if (VoDurationType == DurationType.Permanent)
                return "永久";
            if (VoExpiresAt == null)
                return "未知";
            if (VoIsExpired)
                return "已过期";
            var remaining = VoExpiresAt.Value - DateTime.Now;
            if (remaining.TotalDays > 1)
                return $"剩余 {(int)remaining.TotalDays} 天";
            if (remaining.TotalHours > 1)
                return $"剩余 {(int)remaining.TotalHours} 小时";
            return "即将过期";
        }
    }

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }
}

/// <summary>用户背包项视图模型</summary>
public class UserInventoryVo
{
    /// <summary>背包项 ID</summary>
    public long Id { get; set; }

    /// <summary>用户 ID</summary>
    public long UserId { get; set; }

    /// <summary>消耗品类型</summary>
    public ConsumableType ConsumableType { get; set; }

    /// <summary>消耗品类型显示名称</summary>
    public string ConsumableTypeDisplay => ConsumableType switch
    {
        ConsumableType.RenameCard => "改名卡",
        ConsumableType.PostPinCard => "置顶卡",
        ConsumableType.PostHighlightCard => "高亮卡",
        ConsumableType.ExpCard => "经验卡",
        ConsumableType.CoinCard => "萝卜币红包",
        ConsumableType.DoubleExpCard => "双倍经验卡",
        ConsumableType.LotteryTicket => "抽奖券",
        _ => "未知"
    };

    /// <summary>道具值</summary>
    public string? ItemValue { get; set; }

    /// <summary>道具名称</summary>
    public string? ItemName { get; set; }

    /// <summary>道具图标</summary>
    public string? ItemIcon { get; set; }

    /// <summary>数量</summary>
    public int Quantity { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>使用道具 DTO</summary>
public class UseItemDto
{
    /// <summary>背包项 ID</summary>
    public long InventoryId { get; set; }

    /// <summary>使用数量</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>目标 ID</summary>
    /// <remarks>
    /// 根据道具类型不同含义不同：
    /// - 置顶卡/高亮卡：帖子 ID
    /// - 其他：可空
    /// </remarks>
    public long? TargetId { get; set; }
}

/// <summary>使用道具结果 DTO</summary>
public class UseItemResultDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>剩余数量</summary>
    public int RemainingQuantity { get; set; }

    /// <summary>效果描述</summary>
    /// <remarks>如"获得 100 经验值"、"帖子已置顶 24 小时"</remarks>
    public string? EffectDescription { get; set; }
}
