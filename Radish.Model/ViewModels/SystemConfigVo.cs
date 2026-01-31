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

    /// <summary>配置描述</summary>
    public string? VoDescription { get; set; }

    /// <summary>配置类型（string/number/boolean/json）</summary>
    public string VoType { get; set; } = "string";

    /// <summary>是否启用</summary>
    public bool VoIsEnabled { get; set; } = true;

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }

    /// <summary>修改时间</summary>
    public DateTime? VoModifyTime { get; set; }
}
