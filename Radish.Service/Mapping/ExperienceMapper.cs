using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using System.Text.Json;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class ExperienceMapper
{
    // ExpTransaction → ExpTransactionVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static ExpTransactionVo? ToExpTransactionVo(ExpTransaction? source)
    {
        if (source is null) return null;
        var target = MapExpTransactionToExpTransactionVo(source);
        target.VoExpTypeDisplay = GetExpTypeName(source.ExpType);
        return target;
    }

    [MapProperty(nameof(ExpTransaction.Id), nameof(ExpTransactionVo.VoId))]
    [MapProperty(nameof(ExpTransaction.UserId), nameof(ExpTransactionVo.VoUserId))]
    [MapperIgnoreTarget(nameof(ExpTransactionVo.VoUserName))]
    [MapProperty(nameof(ExpTransaction.CreateId), nameof(ExpTransactionVo.VoOperatorId))]
    [MapProperty(nameof(ExpTransaction.CreateBy), nameof(ExpTransactionVo.VoOperatorName))]
    [MapProperty(nameof(ExpTransaction.ExpType), nameof(ExpTransactionVo.VoExpType))]
    [MapperIgnoreTarget(nameof(ExpTransactionVo.VoExpTypeDisplay))]
    [MapProperty(nameof(ExpTransaction.ExpAmount), nameof(ExpTransactionVo.VoExpAmount))]
    [MapProperty(nameof(ExpTransaction.BusinessType), nameof(ExpTransactionVo.VoBusinessType))]
    [MapProperty(nameof(ExpTransaction.BusinessId), nameof(ExpTransactionVo.VoBusinessId))]
    [MapProperty(nameof(ExpTransaction.Remark), nameof(ExpTransactionVo.VoRemark))]
    [MapProperty(nameof(ExpTransaction.ExpBefore), nameof(ExpTransactionVo.VoExpBefore))]
    [MapProperty(nameof(ExpTransaction.ExpAfter), nameof(ExpTransactionVo.VoExpAfter))]
    [MapProperty(nameof(ExpTransaction.LevelBefore), nameof(ExpTransactionVo.VoLevelBefore))]
    [MapProperty(nameof(ExpTransaction.LevelAfter), nameof(ExpTransactionVo.VoLevelAfter))]
    [MapProperty(nameof(ExpTransaction.CreateTime), nameof(ExpTransactionVo.VoCreateTime))]
    [UserMapping(Default = false)]
    private static partial ExpTransactionVo MapExpTransactionToExpTransactionVo(ExpTransaction source);

    public static partial List<ExpTransactionVo> ToExpTransactionVoList(IEnumerable<ExpTransaction>? source);

    // LevelConfig → LevelConfigVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static LevelConfigVo? ToLevelConfigVo(LevelConfig? source)
    {
        if (source is null) return null;
        var target = MapLevelConfigToLevelConfigVo(source);
        target.VoPrivileges = ParsePrivileges(source.Privileges) ?? [];
        return target;
    }

    [MapProperty(nameof(LevelConfig.Level), nameof(LevelConfigVo.VoLevel))]
    [MapProperty(nameof(LevelConfig.LevelName), nameof(LevelConfigVo.VoLevelName))]
    [MapProperty(nameof(LevelConfig.ExpRequired), nameof(LevelConfigVo.VoExpRequired))]
    [MapProperty(nameof(LevelConfig.ExpCumulative), nameof(LevelConfigVo.VoExpCumulative))]
    [MapProperty(nameof(LevelConfig.ThemeColor), nameof(LevelConfigVo.VoThemeColor))]
    [MapProperty(nameof(LevelConfig.IconAttachmentId), nameof(LevelConfigVo.VoIconAttachmentId))]
    [MapperIgnoreTarget(nameof(LevelConfigVo.VoIconUrl))]
    [MapProperty(nameof(LevelConfig.BadgeAttachmentId), nameof(LevelConfigVo.VoBadgeAttachmentId))]
    [MapperIgnoreTarget(nameof(LevelConfigVo.VoBadgeUrl))]
    [MapProperty(nameof(LevelConfig.Description), nameof(LevelConfigVo.VoDescription))]
    [MapperIgnoreTarget(nameof(LevelConfigVo.VoPrivileges))]
    [MapProperty(nameof(LevelConfig.IsEnabled), nameof(LevelConfigVo.VoIsEnabled))]
    [MapProperty(nameof(LevelConfig.SortOrder), nameof(LevelConfigVo.VoSortOrder))]
    [UserMapping(Default = false)]
    private static partial LevelConfigVo MapLevelConfigToLevelConfigVo(LevelConfig source);

    public static partial List<LevelConfigVo> ToLevelConfigVoList(IEnumerable<LevelConfig>? source);

    // UserExpDailyStats → UserExpDailyStatsVo
    [MapProperty(nameof(UserExpDailyStats.Id), nameof(UserExpDailyStatsVo.VoId))]
    [MapProperty(nameof(UserExpDailyStats.UserId), nameof(UserExpDailyStatsVo.VoUserId))]
    [MapperIgnoreTarget(nameof(UserExpDailyStatsVo.VoStatDate))]
    [MapProperty(nameof(UserExpDailyStats.ExpEarned), nameof(UserExpDailyStatsVo.VoExpEarned))]
    [MapProperty(nameof(UserExpDailyStats.ExpFromPost), nameof(UserExpDailyStatsVo.VoExpFromPost))]
    [MapProperty(nameof(UserExpDailyStats.ExpFromComment), nameof(UserExpDailyStatsVo.VoExpFromComment))]
    [MapProperty(nameof(UserExpDailyStats.ExpFromLike), nameof(UserExpDailyStatsVo.VoExpFromLike))]
    [MapProperty(nameof(UserExpDailyStats.ExpFromHighlight), nameof(UserExpDailyStatsVo.VoExpFromHighlight))]
    [MapProperty(nameof(UserExpDailyStats.ExpFromLogin), nameof(UserExpDailyStatsVo.VoExpFromLogin))]
    [MapProperty(nameof(UserExpDailyStats.PostCount), nameof(UserExpDailyStatsVo.VoPostCount))]
    [MapProperty(nameof(UserExpDailyStats.CommentCount), nameof(UserExpDailyStatsVo.VoCommentCount))]
    [MapProperty(nameof(UserExpDailyStats.LikeGivenCount), nameof(UserExpDailyStatsVo.VoLikeGivenCount))]
    [MapProperty(nameof(UserExpDailyStats.LikeReceivedCount), nameof(UserExpDailyStatsVo.VoLikeReceivedCount))]
    [MapperIgnoreTarget(nameof(UserExpDailyStatsVo.VoObservations))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserExpDailyStatsVo? ToUserExpDailyStatsVo(UserExpDailyStats? source);

    public static partial List<UserExpDailyStatsVo> ToUserExpDailyStatsVoList(IEnumerable<UserExpDailyStats>? source);

    // UserExperience → UserExperienceVo
    [MapProperty(nameof(UserExperience.UserId), nameof(UserExperienceVo.VoUserId))]
    [MapperIgnoreTarget(nameof(UserExperienceVo.VoUserName))]
    [MapperIgnoreTarget(nameof(UserExperienceVo.VoAvatarUrl))]
    [MapProperty(nameof(UserExperience.CurrentLevel), nameof(UserExperienceVo.VoCurrentLevel))]
    [MapperIgnoreTarget(nameof(UserExperienceVo.VoCurrentLevelName))]
    [MapProperty(nameof(UserExperience.CurrentExp), nameof(UserExperienceVo.VoCurrentExp))]
    [MapProperty(nameof(UserExperience.TotalExp), nameof(UserExperienceVo.VoTotalExp))]
    [MapperIgnoreTarget(nameof(UserExperienceVo.VoExpToNextLevel))]
    [MapperIgnoreTarget(nameof(UserExperienceVo.VoNextLevel))]
    [MapperIgnoreTarget(nameof(UserExperienceVo.VoNextLevelName))]
    [MapperIgnoreTarget(nameof(UserExperienceVo.VoLevelProgress))]
    [MapperIgnoreTarget(nameof(UserExperienceVo.VoThemeColor))]
    [MapperIgnoreTarget(nameof(UserExperienceVo.VoIconUrl))]
    [MapperIgnoreTarget(nameof(UserExperienceVo.VoBadgeUrl))]
    [MapProperty(nameof(UserExperience.LevelUpAt), nameof(UserExperienceVo.VoLevelUpAt))]
    [MapperIgnoreTarget(nameof(UserExperienceVo.VoRank))]
    [MapProperty(nameof(UserExperience.ExpFrozen), nameof(UserExperienceVo.VoExpFrozen))]
    [MapProperty(nameof(UserExperience.FrozenUntil), nameof(UserExperienceVo.VoFrozenUntil))]
    [MapProperty(nameof(UserExperience.FrozenReason), nameof(UserExperienceVo.VoFrozenReason))]
    [MapProperty(nameof(UserExperience.Version), nameof(UserExperienceVo.VoVersion))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserExperienceVo? ToUserExperienceVo(UserExperience? source);

    public static partial List<UserExperienceVo> ToUserExperienceVoList(IEnumerable<UserExperience>? source);

    // ExpTransactionVo → ExpTransaction
    [MapProperty(nameof(ExpTransactionVo.VoUserId), nameof(ExpTransaction.UserId))]
    [MapProperty(nameof(ExpTransactionVo.VoExpType), nameof(ExpTransaction.ExpType))]
    [MapProperty(nameof(ExpTransactionVo.VoExpAmount), nameof(ExpTransaction.ExpAmount))]
    [MapProperty(nameof(ExpTransactionVo.VoBusinessType), nameof(ExpTransaction.BusinessType))]
    [MapProperty(nameof(ExpTransactionVo.VoBusinessId), nameof(ExpTransaction.BusinessId))]
    [MapperIgnoreTarget(nameof(ExpTransaction.RewardBusinessKey))]
    [MapProperty(nameof(ExpTransactionVo.VoRemark), nameof(ExpTransaction.Remark))]
    [MapProperty(nameof(ExpTransactionVo.VoExpBefore), nameof(ExpTransaction.ExpBefore))]
    [MapProperty(nameof(ExpTransactionVo.VoExpAfter), nameof(ExpTransaction.ExpAfter))]
    [MapProperty(nameof(ExpTransactionVo.VoLevelBefore), nameof(ExpTransaction.LevelBefore))]
    [MapProperty(nameof(ExpTransactionVo.VoLevelAfter), nameof(ExpTransaction.LevelAfter))]
    [MapperIgnoreTarget(nameof(ExpTransaction.CreatedDate))]
    [MapperIgnoreTarget(nameof(ExpTransaction.TenantId))]
    [MapProperty(nameof(ExpTransactionVo.VoCreateTime), nameof(ExpTransaction.CreateTime))]
    [MapperIgnoreTarget(nameof(ExpTransaction.CreateBy))]
    [MapperIgnoreTarget(nameof(ExpTransaction.CreateId))]
    [MapperIgnoreTarget(nameof(ExpTransaction.ModifyTime))]
    [MapperIgnoreTarget(nameof(ExpTransaction.ModifyBy))]
    [MapperIgnoreTarget(nameof(ExpTransaction.ModifyId))]
    [MapProperty(nameof(ExpTransactionVo.VoId), nameof(ExpTransaction.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial ExpTransaction? ToExpTransaction(ExpTransactionVo? source);

    public static partial List<ExpTransaction> ToExpTransactionList(IEnumerable<ExpTransactionVo>? source);

    // LevelConfigVo → LevelConfig
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static LevelConfig? ToLevelConfig(LevelConfigVo? source)
    {
        if (source is null) return null;
        var target = MapLevelConfigVoToLevelConfig(source);
        target.Privileges = SerializePrivileges(source.VoPrivileges);
        return target;
    }

    [MapProperty(nameof(LevelConfigVo.VoLevel), nameof(LevelConfig.Level))]
    [MapProperty(nameof(LevelConfigVo.VoLevelName), nameof(LevelConfig.LevelName))]
    [MapProperty(nameof(LevelConfigVo.VoExpRequired), nameof(LevelConfig.ExpRequired))]
    [MapProperty(nameof(LevelConfigVo.VoExpCumulative), nameof(LevelConfig.ExpCumulative))]
    [MapProperty(nameof(LevelConfigVo.VoThemeColor), nameof(LevelConfig.ThemeColor))]
    [MapProperty(nameof(LevelConfigVo.VoIconAttachmentId), nameof(LevelConfig.IconAttachmentId))]
    [MapProperty(nameof(LevelConfigVo.VoBadgeAttachmentId), nameof(LevelConfig.BadgeAttachmentId))]
    [MapProperty(nameof(LevelConfigVo.VoDescription), nameof(LevelConfig.Description))]
    [MapperIgnoreTarget(nameof(LevelConfig.Privileges))]
    [MapProperty(nameof(LevelConfigVo.VoIsEnabled), nameof(LevelConfig.IsEnabled))]
    [MapProperty(nameof(LevelConfigVo.VoSortOrder), nameof(LevelConfig.SortOrder))]
    [MapperIgnoreTarget(nameof(LevelConfig.CreateTime))]
    [MapperIgnoreTarget(nameof(LevelConfig.CreateBy))]
    [MapperIgnoreTarget(nameof(LevelConfig.CreateId))]
    [MapperIgnoreTarget(nameof(LevelConfig.ModifyTime))]
    [MapperIgnoreTarget(nameof(LevelConfig.ModifyBy))]
    [MapperIgnoreTarget(nameof(LevelConfig.ModifyId))]
    [MapperIgnoreTarget(nameof(LevelConfig.Id))]
    [UserMapping(Default = false)]
    private static partial LevelConfig MapLevelConfigVoToLevelConfig(LevelConfigVo source);

    public static partial List<LevelConfig> ToLevelConfigList(IEnumerable<LevelConfigVo>? source);

    // UserExpDailyStatsVo → UserExpDailyStats
    [MapProperty(nameof(UserExpDailyStatsVo.VoUserId), nameof(UserExpDailyStats.UserId))]
    [MapperIgnoreTarget(nameof(UserExpDailyStats.StatDate))]
    [MapProperty(nameof(UserExpDailyStatsVo.VoExpEarned), nameof(UserExpDailyStats.ExpEarned))]
    [MapProperty(nameof(UserExpDailyStatsVo.VoExpFromPost), nameof(UserExpDailyStats.ExpFromPost))]
    [MapProperty(nameof(UserExpDailyStatsVo.VoExpFromComment), nameof(UserExpDailyStats.ExpFromComment))]
    [MapProperty(nameof(UserExpDailyStatsVo.VoExpFromLike), nameof(UserExpDailyStats.ExpFromLike))]
    [MapProperty(nameof(UserExpDailyStatsVo.VoExpFromHighlight), nameof(UserExpDailyStats.ExpFromHighlight))]
    [MapProperty(nameof(UserExpDailyStatsVo.VoExpFromLogin), nameof(UserExpDailyStats.ExpFromLogin))]
    [MapProperty(nameof(UserExpDailyStatsVo.VoPostCount), nameof(UserExpDailyStats.PostCount))]
    [MapProperty(nameof(UserExpDailyStatsVo.VoCommentCount), nameof(UserExpDailyStats.CommentCount))]
    [MapProperty(nameof(UserExpDailyStatsVo.VoLikeGivenCount), nameof(UserExpDailyStats.LikeGivenCount))]
    [MapProperty(nameof(UserExpDailyStatsVo.VoLikeReceivedCount), nameof(UserExpDailyStats.LikeReceivedCount))]
    [MapperIgnoreTarget(nameof(UserExpDailyStats.TenantId))]
    [MapperIgnoreTarget(nameof(UserExpDailyStats.CreateTime))]
    [MapperIgnoreTarget(nameof(UserExpDailyStats.CreateBy))]
    [MapperIgnoreTarget(nameof(UserExpDailyStats.CreateId))]
    [MapperIgnoreTarget(nameof(UserExpDailyStats.ModifyTime))]
    [MapperIgnoreTarget(nameof(UserExpDailyStats.ModifyBy))]
    [MapperIgnoreTarget(nameof(UserExpDailyStats.ModifyId))]
    [MapProperty(nameof(UserExpDailyStatsVo.VoId), nameof(UserExpDailyStats.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserExpDailyStats? ToUserExpDailyStats(UserExpDailyStatsVo? source);

    public static partial List<UserExpDailyStats> ToUserExpDailyStatsList(IEnumerable<UserExpDailyStatsVo>? source);

    // UserExperienceVo → UserExperience
    [MapProperty(nameof(UserExperienceVo.VoUserId), nameof(UserExperience.UserId))]
    [MapProperty(nameof(UserExperienceVo.VoCurrentLevel), nameof(UserExperience.CurrentLevel))]
    [MapProperty(nameof(UserExperienceVo.VoCurrentExp), nameof(UserExperience.CurrentExp))]
    [MapProperty(nameof(UserExperienceVo.VoTotalExp), nameof(UserExperience.TotalExp))]
    [MapProperty(nameof(UserExperienceVo.VoLevelUpAt), nameof(UserExperience.LevelUpAt))]
    [MapProperty(nameof(UserExperienceVo.VoExpFrozen), nameof(UserExperience.ExpFrozen))]
    [MapProperty(nameof(UserExperienceVo.VoFrozenUntil), nameof(UserExperience.FrozenUntil))]
    [MapProperty(nameof(UserExperienceVo.VoFrozenReason), nameof(UserExperience.FrozenReason))]
    [MapProperty(nameof(UserExperienceVo.VoVersion), nameof(UserExperience.Version))]
    [MapperIgnoreTarget(nameof(UserExperience.TenantId))]
    [MapperIgnoreTarget(nameof(UserExperience.CreateTime))]
    [MapperIgnoreTarget(nameof(UserExperience.CreateBy))]
    [MapperIgnoreTarget(nameof(UserExperience.CreateId))]
    [MapperIgnoreTarget(nameof(UserExperience.ModifyTime))]
    [MapperIgnoreTarget(nameof(UserExperience.ModifyBy))]
    [MapperIgnoreTarget(nameof(UserExperience.ModifyId))]
    [MapperIgnoreTarget(nameof(UserExperience.IsDeleted))]
    [MapperIgnoreTarget(nameof(UserExperience.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserExperience? ToUserExperience(UserExperienceVo? source);

    public static partial List<UserExperience> ToUserExperienceList(IEnumerable<UserExperienceVo>? source);

    private static string GetExpTypeName(string expType)
    {
        return expType switch
        {
            "POST_CREATE" => "发布帖子",
            "POST_LIKED" => "帖子被点赞",
            "COMMENT_CREATE" => "发布评论",
            "COMMENT_LIKED" => "评论被点赞",
            "COMMENT_REPLIED" => "评论被回复",
            "LIKE_OTHERS" => "点赞他人",
            "GOD_COMMENT" => "成为神评",
            "SOFA_COMMENT" => "成为沙发",
            "DAILY_LOGIN" => "每日登录",
            "WEEKLY_LOGIN" => "连续登录",
            "PROFILE_COMPLETE" => "完善资料",
            "FIRST_POST" => "首次发帖",
            "FIRST_COMMENT" => "首次评论",
            "ADMIN_ADJUST" => "管理员调整",
            "PENALTY" => "惩罚扣除",
            _ => expType
        };
    }
    private static List<string>? ParsePrivileges(string? privilegesJson)
    {
        if (string.IsNullOrWhiteSpace(privilegesJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(privilegesJson);
        }
        catch
        {
            return null;
        }
    }
    private static string? SerializePrivileges(List<string>? privileges)
    {
        if (privileges == null || privileges.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(privileges);
    }

}
