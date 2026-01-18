using System.Text.Json;
using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>经验值系统实体映射配置</summary>
public class ExperienceProfile : Profile
{
    public ExperienceProfile()
    {
        ConfigureUserExperienceMapping();
        ConfigureExpTransactionMapping();
        ConfigureLevelConfigMapping();
        ConfigureUserExpDailyStatsMapping();
    }

    /// <summary>配置用户经验值映射</summary>
    private void ConfigureUserExperienceMapping()
    {
        // UserExperience -> UserExperienceVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<UserExperience, UserExperienceVo>()
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id)) // Id 就是 UserId
            .ForMember(dest => dest.UserName, opt => opt.Ignore()) // 需要在 Service 层单独设置
            .ForMember(dest => dest.AvatarUrl, opt => opt.Ignore()) // 需要在 Service 层单独设置
            .ForMember(dest => dest.CurrentLevelName, opt => opt.Ignore()) // 需要在 Service 层根据 LevelConfig 设置
            .ForMember(dest => dest.ExpToNextLevel, opt => opt.Ignore()) // 需要在 Service 层计算
            .ForMember(dest => dest.NextLevel, opt => opt.Ignore()) // 需要在 Service 层设置
            .ForMember(dest => dest.NextLevelName, opt => opt.Ignore()) // 需要在 Service 层设置
            .ForMember(dest => dest.LevelProgress, opt => opt.Ignore()) // 需要在 Service 层计算
            .ForMember(dest => dest.ThemeColor, opt => opt.Ignore()) // 需要在 Service 层根据 LevelConfig 设置
            .ForMember(dest => dest.IconUrl, opt => opt.Ignore()) // 需要在 Service 层根据 LevelConfig 设置
            .ForMember(dest => dest.BadgeUrl, opt => opt.Ignore()) // 需要在 Service 层根据 LevelConfig 设置
            .ForMember(dest => dest.Rank, opt => opt.Ignore()); // 需要在 Service 层单独查询

        // UserExperienceVo -> UserExperience
        RecognizePrefixes("Vo");
        CreateMap<UserExperienceVo, UserExperience>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.UserId));
    }

    /// <summary>配置经验值交易记录映射</summary>
    private void ConfigureExpTransactionMapping()
    {
        // ExpTransaction -> ExpTransactionVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<ExpTransaction, ExpTransactionVo>()
            .ForMember(dest => dest.ExpTypeDisplay, opt => opt.MapFrom(src => GetExpTypeName(src.ExpType)))
            .ForMember(dest => dest.UserName, opt => opt.Ignore()); // 需要在 Service 层单独设置

        // ExpTransactionVo -> ExpTransaction
        RecognizePrefixes("Vo");
        CreateMap<ExpTransactionVo, ExpTransaction>();
    }

    /// <summary>配置等级配置映射</summary>
    private void ConfigureLevelConfigMapping()
    {
        // LevelConfig -> LevelConfigVo (使用前缀识别 + 手动配置特殊字段)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<LevelConfig, LevelConfigVo>()
            .ForMember(dest => dest.VoPrivileges, opt => opt.MapFrom(src => ParsePrivileges(src.Privileges)));

        // LevelConfigVo -> LevelConfig (使用前缀识别 + 手动配置特殊字段)
        RecognizePrefixes("Vo");
        CreateMap<LevelConfigVo, LevelConfig>()
            .ForMember(dest => dest.Privileges, opt => opt.MapFrom(src => SerializePrivileges(src.VoPrivileges)));
    }

    /// <summary>配置每日统计映射</summary>
    private void ConfigureUserExpDailyStatsMapping()
    {
        // UserExpDailyStats -> UserExpDailyStatsVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<UserExpDailyStats, UserExpDailyStatsVo>();

        // UserExpDailyStatsVo -> UserExpDailyStats
        RecognizePrefixes("Vo");
        CreateMap<UserExpDailyStatsVo, UserExpDailyStats>();
    }

    /// <summary>
    /// 获取经验值类型中文名称
    /// </summary>
    /// <param name="expType">经验值类型代码</param>
    /// <returns>经验值类型中文名称</returns>
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

    /// <summary>
    /// 解析 JSON 格式的特权列表
    /// </summary>
    /// <param name="privilegesJson">特权列表 JSON 字符串</param>
    /// <returns>特权列表</returns>
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

    /// <summary>
    /// 序列化特权列表为 JSON 格式
    /// </summary>
    /// <param name="privileges">特权列表</param>
    /// <returns>特权列表 JSON 字符串</returns>
    private static string? SerializePrivileges(List<string>? privileges)
    {
        if (privileges == null || privileges.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(privileges);
    }
}
