namespace Radish.Shared.Constants;

/// <summary>
/// 审计日志操作类型常量
/// </summary>
public static class AuditOperationType
{
    // ==================== 认证相关 ====================
    /// <summary>用户登录</summary>
    public const string Login = "Login";

    /// <summary>用户登出</summary>
    public const string Logout = "Logout";

    /// <summary>登录失败</summary>
    public const string LoginFailed = "LoginFailed";

    /// <summary>Token 刷新</summary>
    public const string RefreshToken = "RefreshToken";

    // ==================== 用户管理 ====================
    /// <summary>创建用户</summary>
    public const string CreateUser = "CreateUser";

    /// <summary>更新用户</summary>
    public const string UpdateUser = "UpdateUser";

    /// <summary>删除用户</summary>
    public const string DeleteUser = "DeleteUser";

    /// <summary>禁用用户</summary>
    public const string DisableUser = "DisableUser";

    /// <summary>启用用户</summary>
    public const string EnableUser = "EnableUser";

    /// <summary>重置密码</summary>
    public const string ResetPassword = "ResetPassword";

    // ==================== 角色管理 ====================
    /// <summary>创建角色</summary>
    public const string CreateRole = "CreateRole";

    /// <summary>更新角色</summary>
    public const string UpdateRole = "UpdateRole";

    /// <summary>删除角色</summary>
    public const string DeleteRole = "DeleteRole";

    /// <summary>分配角色</summary>
    public const string AssignRole = "AssignRole";

    /// <summary>移除角色</summary>
    public const string RemoveRole = "RemoveRole";

    // ==================== 权限管理 ====================
    /// <summary>创建权限</summary>
    public const string CreatePermission = "CreatePermission";

    /// <summary>更新权限</summary>
    public const string UpdatePermission = "UpdatePermission";

    /// <summary>删除权限</summary>
    public const string DeletePermission = "DeletePermission";

    /// <summary>分配权限</summary>
    public const string AssignPermission = "AssignPermission";

    /// <summary>移除权限</summary>
    public const string RemovePermission = "RemovePermission";

    // ==================== 客户端管理 ====================
    /// <summary>创建客户端</summary>
    public const string CreateClient = "CreateClient";

    /// <summary>更新客户端</summary>
    public const string UpdateClient = "UpdateClient";

    /// <summary>删除客户端</summary>
    public const string DeleteClient = "DeleteClient";

    /// <summary>重置客户端密钥</summary>
    public const string ResetClientSecret = "ResetClientSecret";

    /// <summary>启用/禁用客户端</summary>
    public const string ToggleClientStatus = "ToggleClientStatus";

    // ==================== 租户管理 ====================
    /// <summary>创建租户</summary>
    public const string CreateTenant = "CreateTenant";

    /// <summary>更新租户</summary>
    public const string UpdateTenant = "UpdateTenant";

    /// <summary>删除租户</summary>
    public const string DeleteTenant = "DeleteTenant";

    // ==================== 内容管理 ====================
    /// <summary>创建帖子</summary>
    public const string CreatePost = "CreatePost";

    /// <summary>更新帖子</summary>
    public const string UpdatePost = "UpdatePost";

    /// <summary>删除帖子</summary>
    public const string DeletePost = "DeletePost";

    /// <summary>删除评论</summary>
    public const string DeleteComment = "DeleteComment";

    // ==================== 系统配置 ====================
    /// <summary>更新系统配置</summary>
    public const string UpdateSystemConfig = "UpdateSystemConfig";

    /// <summary>清除缓存</summary>
    public const string ClearCache = "ClearCache";

    // ==================== 数据导出 ====================
    /// <summary>导出数据</summary>
    public const string ExportData = "ExportData";

    /// <summary>导出审计日志</summary>
    public const string ExportAuditLog = "ExportAuditLog";
}

/// <summary>
/// 审计日志模块常量
/// </summary>
public static class AuditModule
{
    /// <summary>认证模块</summary>
    public const string Authentication = "Authentication";

    /// <summary>用户模块</summary>
    public const string User = "User";

    /// <summary>角色模块</summary>
    public const string Role = "Role";

    /// <summary>权限模块</summary>
    public const string Permission = "Permission";

    /// <summary>客户端模块</summary>
    public const string Client = "Client";

    /// <summary>租户模块</summary>
    public const string Tenant = "Tenant";

    /// <summary>帖子模块</summary>
    public const string Post = "Post";

    /// <summary>评论模块</summary>
    public const string Comment = "Comment";

    /// <summary>系统模块</summary>
    public const string System = "System";

    /// <summary>审计日志模块</summary>
    public const string AuditLog = "AuditLog";
}
