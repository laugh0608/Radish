namespace Radish.Model.ViewModels;

/// <summary>
/// 系统配置 VO
/// </summary>
public class SystemConfigVo
{
    /// <summary>配置 ID</summary>
    public long VoId { get; set; }

    /// <summary>配置分类</summary>
    public string VoCategory { get; set; } = string.Empty;

    /// <summary>配置键</summary>
    public string VoKey { get; set; } = string.Empty;

    /// <summary>配置名称</summary>
    public string VoName { get; set; } = string.Empty;

    /// <summary>配置值</summary>
    public string VoValue { get; set; } = string.Empty;

    /// <summary>默认值</summary>
    public string VoDefaultValue { get; set; } = string.Empty;

    /// <summary>当前生效值</summary>
    public string VoEffectiveValue { get; set; } = string.Empty;

    /// <summary>配置描述</summary>
    public string? VoDescription { get; set; }

    /// <summary>配置类型（string/number/boolean/json）</summary>
    public string VoType { get; set; } = "string";

    /// <summary>覆盖值是否启用</summary>
    public bool VoIsEnabled { get; set; } = true;

    /// <summary>是否存在覆盖值</summary>
    public bool VoIsOverridden { get; set; }

    /// <summary>风险等级</summary>
    public string VoRiskLevel { get; set; } = "Low";

    /// <summary>生效方式</summary>
    public string VoEffectiveMode { get; set; } = "Immediate";

    /// <summary>是否允许在 Console 编辑</summary>
    public bool VoIsEditable { get; set; } = true;

    /// <summary>是否敏感设置</summary>
    public bool VoIsSensitive { get; set; }

    /// <summary>创建时间</summary>
    public DateTime? VoCreateTime { get; set; }

    /// <summary>修改时间</summary>
    public DateTime? VoModifyTime { get; set; }
}
