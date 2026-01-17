namespace Radish.Model.ViewModels;

/// <summary>GetById 测试结果 ViewModel</summary>
/// <remarks>用于替代 GetById() 方法的匿名对象</remarks>
public class GetByIdResultVo
{
    /// <summary>ID</summary>
    public int Id { get; set; }

    /// <summary>名称</summary>
    public string Name { get; set; } = string.Empty;
}