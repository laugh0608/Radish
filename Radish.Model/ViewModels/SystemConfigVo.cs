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

    /// <summary>影响范围摘要</summary>
    public string? VoImpactSummary { get; set; }

    /// <summary>配置类型（string/number/boolean/json）</summary>
    public string VoType { get; set; } = "string";

    /// <summary>数字最小值</summary>
    public decimal? VoMinNumberValue { get; set; }

    /// <summary>数字最大值</summary>
    public decimal? VoMaxNumberValue { get; set; }

    /// <summary>数字设置是否必须为整数</summary>
    public bool VoRequiresInteger { get; set; }

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

    /// <summary>写入版本号，默认态为 0</summary>
    public int VoVersion { get; set; }

    /// <summary>创建时间</summary>
    public DateTime? VoCreateTime { get; set; }

    /// <summary>修改时间</summary>
    public DateTime? VoModifyTime { get; set; }
}

/// <summary>
/// 系统设置变更审计 VO
/// </summary>
public class SystemConfigChangeLogVo
{
    /// <summary>审计记录 ID</summary>
    public long VoId { get; set; }

    /// <summary>设置分类</summary>
    public string VoCategory { get; set; } = string.Empty;

    /// <summary>设置键</summary>
    public string VoKey { get; set; } = string.Empty;

    /// <summary>设置名称</summary>
    public string VoName { get; set; } = string.Empty;

    /// <summary>动作类型</summary>
    public string VoActionType { get; set; } = string.Empty;

    /// <summary>旧值</summary>
    public string? VoOldValue { get; set; }

    /// <summary>新值</summary>
    public string? VoNewValue { get; set; }

    /// <summary>默认值</summary>
    public string VoDefaultValue { get; set; } = string.Empty;

    /// <summary>修改原因</summary>
    public string VoReason { get; set; } = string.Empty;

    /// <summary>风险等级</summary>
    public string VoRiskLevel { get; set; } = "Low";

    /// <summary>生效方式</summary>
    public string VoEffectiveMode { get; set; } = "Immediate";

    /// <summary>确认风险等级</summary>
    public string? VoConfirmRiskLevel { get; set; }

    /// <summary>确认设置键</summary>
    public string? VoConfirmKey { get; set; }

    /// <summary>操作者用户 ID</summary>
    public long? VoOperatorUserId { get; set; }

    /// <summary>操作者用户名</summary>
    public string? VoOperatorUserName { get; set; }

    /// <summary>请求来源 IP</summary>
    public string? VoRequestIp { get; set; }

    /// <summary>User-Agent</summary>
    public string? VoUserAgent { get; set; }

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }
}
