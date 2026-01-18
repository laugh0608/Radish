namespace Radish.Model.ViewModels;

/// <summary>App 测试结果 ViewModel</summary>
/// <remarks>用于替代 AppTest() 方法的匿名对象</remarks>
public class AppTestResultVo
{
    /// <summary>第三个服务的角色列表</summary>
    public List<RoleVo> VoRoleList3 { get; set; } = new();
}