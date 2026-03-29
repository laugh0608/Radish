namespace Radish.Model;

/// <summary>
/// 系统配置持久化记录
/// </summary>
public class SystemConfigRecord
{
    public long Id { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Type { get; set; } = "string";

    public bool IsEnabled { get; set; } = true;

    public DateTime CreateTime { get; set; }

    public DateTime? ModifyTime { get; set; }
}
