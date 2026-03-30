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
}
