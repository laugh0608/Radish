namespace Radish.Model;

/// <summary>
/// 系统设置风险等级
/// </summary>
public static class SystemConfigRiskLevel
{
    public const string Low = "Low";

    public const string Medium = "Medium";

    public const string High = "High";

    public const string Critical = "Critical";
}

/// <summary>
/// 系统设置生效方式
/// </summary>
public static class SystemConfigEffectiveMode
{
    public const string Immediate = "Immediate";

    public const string RestartRequired = "RestartRequired";
}

/// <summary>
/// 系统设置定义
/// </summary>
public sealed class SystemConfigDefinition
{
    public long Id { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string ValueType { get; init; } = "string";

    public string DefaultValue { get; init; } = string.Empty;

    public decimal? MinNumberValue { get; init; }

    public decimal? MaxNumberValue { get; init; }

    public bool RequiresInteger { get; init; }

    public string RiskLevel { get; init; } = SystemConfigRiskLevel.Low;

    public string EffectiveMode { get; init; } = SystemConfigEffectiveMode.Immediate;

    public bool IsEditable { get; init; } = true;

    public bool IsSensitive { get; init; }
}

/// <summary>
/// 系统配置默认值常量
/// </summary>
public static class SystemConfigDefaults
{
    private static readonly SystemConfigDefinition[] DefinitionItems =
    [
        new()
        {
            Id = 1,
            Category = SiteBrandingCategory,
            Key = SiteFaviconKey,
            Name = SiteFaviconName,
            Description = "浏览器标签页显示的网站图标，默认使用 DataBases/Uploads/DefaultIco/bailuobo.ico。",
            ValueType = "string",
            DefaultValue = DefaultSiteFaviconPath,
            RiskLevel = SystemConfigRiskLevel.Low,
            EffectiveMode = SystemConfigEffectiveMode.Immediate,
            IsEditable = true,
            IsSensitive = false
        },
        new()
        {
            Id = 2,
            Category = ContentPublishingCategory,
            Key = PostTitleMinLengthKey,
            Name = "帖子标题最小长度",
            Description = "发帖和编辑帖子时标题至少需要达到的字符数。",
            ValueType = "number",
            DefaultValue = DefaultPostTitleMinLength,
            MinNumberValue = 1,
            MaxNumberValue = 200,
            RequiresInteger = true,
            RiskLevel = SystemConfigRiskLevel.Medium,
            EffectiveMode = SystemConfigEffectiveMode.Immediate,
            IsEditable = true,
            IsSensitive = false
        },
        new()
        {
            Id = 3,
            Category = ContentPublishingCategory,
            Key = PostBodyMinLengthKey,
            Name = "帖子正文最小长度",
            Description = "发帖和编辑帖子时正文至少需要达到的字符数。",
            ValueType = "number",
            DefaultValue = DefaultPostBodyMinLength,
            MinNumberValue = 1,
            MaxNumberValue = 50000,
            RequiresInteger = true,
            RiskLevel = SystemConfigRiskLevel.Medium,
            EffectiveMode = SystemConfigEffectiveMode.Immediate,
            IsEditable = true,
            IsSensitive = false
        },
        new()
        {
            Id = 4,
            Category = CommentInteractionCategory,
            Key = CommentBodyMinLengthKey,
            Name = "评论内容最小长度",
            Description = "发表评论和编辑评论时内容至少需要达到的字符数。",
            ValueType = "number",
            DefaultValue = DefaultCommentBodyMinLength,
            MinNumberValue = 1,
            MaxNumberValue = 2000,
            RequiresInteger = true,
            RiskLevel = SystemConfigRiskLevel.Low,
            EffectiveMode = SystemConfigEffectiveMode.Immediate,
            IsEditable = true,
            IsSensitive = false
        }
    ];

    private static readonly IReadOnlyDictionary<string, SystemConfigDefinition> DefinitionsByKey =
        DefinitionItems.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<long, SystemConfigDefinition> DefinitionsById =
        DefinitionItems.ToDictionary(item => item.Id);

    /// <summary>站点品牌分类</summary>
    public const string SiteBrandingCategory = "站点品牌";

    /// <summary>站点 favicon 配置键</summary>
    public const string SiteFaviconKey = "Site.Branding.FaviconUrl";

    /// <summary>站点 favicon 配置名称</summary>
    public const string SiteFaviconName = "标签页图标";

    /// <summary>默认 favicon 相对地址</summary>
    public const string DefaultSiteFaviconPath = "/uploads/DefaultIco/bailuobo.ico";

    /// <summary>内容发布分类</summary>
    public const string ContentPublishingCategory = "内容发布";

    /// <summary>评论互动分类</summary>
    public const string CommentInteractionCategory = "评论互动";

    /// <summary>帖子标题最小长度配置键</summary>
    public const string PostTitleMinLengthKey = "Content.PostTitle.MinLength";

    /// <summary>帖子正文最小长度配置键</summary>
    public const string PostBodyMinLengthKey = "Content.PostBody.MinLength";

    /// <summary>评论内容最小长度配置键</summary>
    public const string CommentBodyMinLengthKey = "Comment.Body.MinLength";

    /// <summary>默认帖子标题最小长度</summary>
    public const string DefaultPostTitleMinLength = "3";

    /// <summary>默认帖子正文最小长度</summary>
    public const string DefaultPostBodyMinLength = "10";

    /// <summary>默认评论内容最小长度</summary>
    public const string DefaultCommentBodyMinLength = "1";

    /// <summary>已注册系统设置定义</summary>
    public static IReadOnlyList<SystemConfigDefinition> Definitions => DefinitionItems;

    public static SystemConfigDefinition? GetDefinitionByKey(string key)
    {
        return string.IsNullOrWhiteSpace(key)
            ? null
            : DefinitionsByKey.GetValueOrDefault(key.Trim());
    }

    public static SystemConfigDefinition? GetDefinitionById(long id)
    {
        return DefinitionsById.GetValueOrDefault(id);
    }
}
