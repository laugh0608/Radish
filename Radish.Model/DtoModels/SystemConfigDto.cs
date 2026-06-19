namespace Radish.Model.DtoModels;

/// <summary>
/// 创建系统配置请求
/// </summary>
public class CreateSystemConfigDto
{
    public string Category { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Type { get; set; } = "string";

    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 更新系统配置请求
/// </summary>
public class UpdateSystemConfigDto
{
    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? Reason { get; set; }

    public string? ConfirmRiskLevel { get; set; }

    public string? ConfirmKey { get; set; }
}

/// <summary>
/// 恢复系统设置默认值请求
/// </summary>
public class RestoreSystemConfigDefaultDto
{
    public string? Reason { get; set; }

    public string? ConfirmRiskLevel { get; set; }

    public string? ConfirmKey { get; set; }
}

/// <summary>
/// 系统设置变更请求上下文
/// </summary>
public class SystemConfigChangeContext
{
    public long? OperatorUserId { get; set; }

    public string? OperatorUserName { get; set; }

    public string? RequestIp { get; set; }

    public string? UserAgent { get; set; }
}
