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
