namespace Radish.Model;

/// <summary>
/// 系统设置变更动作类型
/// </summary>
public static class SystemConfigChangeAction
{
    public const string UpdateOverride = "UpdateOverride";

    public const string RestoreDefault = "RestoreDefault";
}

/// <summary>
/// 系统设置变更审计记录
/// </summary>
public class SystemConfigChangeLogRecord
{
    public long Id { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ActionType { get; set; } = SystemConfigChangeAction.UpdateOverride;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string DefaultValue { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = SystemConfigRiskLevel.Low;

    public string EffectiveMode { get; set; } = SystemConfigEffectiveMode.Immediate;

    public string? ConfirmRiskLevel { get; set; }

    public string? ConfirmKey { get; set; }

    public long? OperatorUserId { get; set; }

    public string? OperatorUserName { get; set; }

    public string? RequestIp { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreateTime { get; set; }
}
