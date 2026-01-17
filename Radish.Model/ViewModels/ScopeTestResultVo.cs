namespace Radish.Model.ViewModels;

/// <summary>作用域测试结果 ViewModel</summary>
/// <remarks>用于替代 ScopeTest() 方法的匿名对象</remarks>
public class ScopeTestResultVo
{
    /// <summary>第一个服务的角色列表</summary>
    public List<RoleVo> RoleList1 { get; set; } = new();

    /// <summary>第二个服务的角色列表</summary>
    public List<RoleVo> RoleList2 { get; set; } = new();
}