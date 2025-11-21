using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户实体</summary>
/// <remarks>对应仓储层生成的内存数据</remarks>
public class User : RootEntityTKey<long>
{
    #region 登录相关

    /// <summary>登录账号 1 LoginName</summary>
    /// <remarks>
    /// <para>优先使用</para>
    /// <para>不可为空，最大 200 字符</para>
    /// </remarks>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string LoginName { get; set; } = string.Empty;

    /// <summary>登录账号 2 UserName</summary>
    /// <remarks>不可为空，最大 200 字符</remarks>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>登录账号 3 UserEmail</summary>
    /// <remarks>不可为空，最大 200 字符</remarks>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>登录密码</summary>
    /// <remarks>不可为空，最大 200 字符</remarks>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string LoginPassword { get; set; } = string.Empty;

    #endregion

    #region 个人信息相关

    /// <summary>真实姓名</summary>
    /// <remarks>不可为空，最大 50 字符</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string UserRealName { get; set; } = string.Empty;

    /// <summary>性别</summary>
    /// <remarks>
    /// <para>0 为保密，1 为男，2 为女</para>
    /// <para>不可为空，默认为 0</para>
    /// </remarks>
    [SugarColumn(IsNullable = true)]
    public int UserSex { get; set; } = 0;

    /// <summary>年龄</summary>
    /// <remarks>不可为空，默认为 18</remarks>
    [SugarColumn(IsNullable = true)]
    public int UserAge { get; set; } = 18;

    /// <summary>生日</summary>
    /// <remarks>可空，限定格式为 yyyy-MM-dd</remarks>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime? UserBirth { get; set; }

    /// <summary>地址</summary>
    /// <remarks>可空，限定字符为 2000</remarks>
    [SugarColumn(Length = 2000)]
    public string UserAddress { get; set; } = string.Empty;

    #endregion

    #region 状态相关

    /// <summary>状态</summary>
    /// <remarks>不可为空，默认为 -1</remarks>
    [SugarColumn(IsNullable = true)]
    public int StatusCode { get; set; } = -1;

    /// <summary>创建时间</summary>
    /// <remarks>更新时忽略改列</remarks>
    [SugarColumn(IsNullable = true, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? UpdateTime { get; set; }

    /// <summary>关键业务修改时间</summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? CriticalModifyTime { get; set; }

    /// <summary>最后异常时间</summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? LastErrorTime { get; set; }

    /// <summary>错误次数</summary>
    /// <remarks>默认为 0</remarks>
    public int ErrorCount { get; set; } = 0;

    /// <summary>用户是否启用</summary>
    /// <remarks>不可为空，默认为 true</remarks>
    [SugarColumn(IsNullable = true)]
    public bool IsEnable { get; set; } = false;

    /// <summary>用户是否被删除</summary>
    /// <remarks>不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = true)]
    public bool IsDeleted { get; set; } = true;

    #endregion

    #region 权限相关

    /// <summary>部门 Id</summary>
    /// <remarks>不可为空，默认为 1000001</remarks>
    [SugarColumn(IsNullable = true)]
    public long DepartmentId { get; set; } = 1000001;

    /// <summary>所在部门名称</summary>
    /// <remarks>不可为空，最大 200 字符</remarks>
    [SugarColumn(Length = 200,IsIgnore = true)]
    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>租户 Id</summary>
    /// <remarks>不可为空，默认为 1000001</remarks>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 1000001;

    /// <summary>所拥有角色的 Ids</summary>
    /// <remarks>不可为空</remarks>
    [SugarColumn(IsIgnore = true)]
    public List<long> RoleIds { get; set; } = new List<long>();

    /// <summary>角色名称列表</summary>
    /// <remarks>不可为空</remarks>
    [SugarColumn(IsIgnore = true)]
    public List<string> RoleNames { get; set; } = new List<string>();

    /// <summary>自定义权限的部门 Ids</summary>
    /// <remarks>不可为空</remarks>
    [SugarColumn(IsIgnore = true)]
    public List<long> DepartmentIds { get; set; } = new List<long>();

    #endregion

    /// <summary>备注</summary>
    /// <remarks>不可为空，最大 2000 字符，默认为 "There is no remark"</remarks>
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string Remark { get; set; } = "There is no remark";
}