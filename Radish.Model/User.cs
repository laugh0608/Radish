namespace Radish.Model;

/// <summary>用户实体</summary>
/// <remarks>对应仓储层生成的内存数据</remarks>
public class User
{
    /// <summary>用户唯一 ID，主键</summary>
    public long Id { get; set; }

    /// <summary>用户名称</summary>
    public string UserName { get; set; } = string.Empty;
}