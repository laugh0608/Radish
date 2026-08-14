namespace Radish.Model.ViewModels;

/// <summary>Console 用户角色与派生权限的只读权威快照。</summary>
public sealed class ConsoleUserAuthorizationVo
{
    public long VoUserId { get; set; }

    public List<string> VoRoleNames { get; set; } = [];

    public List<string> VoPermissionKeys { get; set; } = [];
}
