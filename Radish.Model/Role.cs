namespace Radish.Model;

/// <summary>角色实体</summary>
/// <remarks>对应仓储层生成的内存数据</remarks>
public class Role
{
    /// <summary>角色唯一 ID，主键</summary>
    public long Id { get; set; }

    /// <summary>角色名称</summary>
    public string RoleName { get; set; } = string.Empty;
}