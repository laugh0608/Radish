using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户实体</summary>
/// <remarks>对应仓储层生成的内存数据</remarks>
public class User : RootEntityTKey<long>
{
    /// <summary>初始化默认用户实例</summary>
    public User()
    {
        InitializeDefaults();
    }

    /// <summary>通过登录名与密码初始化用户</summary>
    /// <param name="loginName">登录名</param>
    /// <param name="loginPassword">登录密码</param>
    public User(string loginName, string loginPassword)
        : this(new UserInitializationOptions(loginName, loginPassword))
    {
    }

    /// <summary>通过初始化选项批量构造用户</summary>
    /// <param name="options">初始化选项</param>
    public User(UserInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyLoginInformation(options.LoginName, options.LoginPassword, options.UserName, options.UserEmail);
        ApplyProfileInformation(options.UserRealName, options.UserSex, options.UserAge, options.UserBirth,
            options.UserAddress);
        ApplyPermissionInformation(options);
        ApplyStatusInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        LoginName = string.Empty;
        UserName = string.Empty;
        UserEmail = string.Empty;
        LoginPassword = string.Empty;
        UserRealName = string.Empty;
        UserSex = (int)UserSexEnum.Unknown;
        UserAge = 18;
        UserBirth = null;
        UserAddress = string.Empty;
        StatusCode = (int)UserStatusCodeEnum.Unknown;
        CreateTime = DateTime.Now;
        UpdateTime = DateTime.Now;
        CriticalModifyTime = DateTime.Now;
        LastErrorTime = DateTime.Now;
        ErrorCount = 0;
        IsEnable = false;
        IsDeleted = true;
        DepartmentId = 0;
        DepartmentName = string.Empty;
        TenantId = 0;
        RoleIds = new List<long>();
        RoleNames = new List<string>();
        DepartmentIds = new List<long>();
        Remark = "There is no remark";
    }

    /// <summary>处理登录信息与账号标识</summary>
    private void ApplyLoginInformation(string loginName, string loginPassword, string? userName, string? userEmail)
    {
        LoginName = NormalizeRequired(loginName, nameof(loginName));
        LoginPassword = NormalizeRequired(loginPassword, nameof(loginPassword));
        UserName = !string.IsNullOrWhiteSpace(userName) ? userName.Trim() : LoginName;

        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            UserEmail = userEmail.Trim();
        }
    }

    /// <summary>处理个人信息（非必填）</summary>
    private void ApplyProfileInformation(string? userRealName, int? userSex, int? userAge, DateTime? userBirth,
        string? userAddress)
    {
        if (!string.IsNullOrWhiteSpace(userRealName))
        {
            UserRealName = userRealName.Trim();
        }

        if (userSex.HasValue)
        {
            UserSex = Clamp(userSex.Value, (int)UserSexEnum.Unknown, (int)UserSexEnum.Female);
        }

        if (userAge.HasValue)
        {
            UserAge = Math.Max(userAge.Value, 0);
        }

        if (userBirth.HasValue)
        {
            UserBirth = userBirth;
        }

        if (!string.IsNullOrWhiteSpace(userAddress))
        {
            UserAddress = userAddress.Trim();
        }
    }

    /// <summary>处理权限、租户相关信息</summary>
    private void ApplyPermissionInformation(UserInitializationOptions options)
    {
        if (options.TenantId.HasValue)
        {
            TenantId = options.TenantId.Value;
        }

        if (options.DepartmentId.HasValue)
        {
            DepartmentId = options.DepartmentId.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.DepartmentName))
        {
            DepartmentName = options.DepartmentName.Trim();
        }

        if (options.RoleIds != null)
        {
            RoleIds = NormalizeIds(options.RoleIds);
        }

        if (options.RoleNames != null)
        {
            RoleNames = NormalizeNames(options.RoleNames);
        }

        if (options.DepartmentIds != null)
        {
            DepartmentIds = NormalizeIds(options.DepartmentIds);
        }
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(UserInitializationOptions options)
    {
        if (options.StatusCode.HasValue)
        {
            StatusCode = options.StatusCode.Value;  // 若从外部直接传入，可考虑后续改为 UserStatusCode 枚举
        }

        if (options.IsEnable.HasValue)
        {
            IsEnable = options.IsEnable.Value;
            if (!options.StatusCode.HasValue && IsEnable && StatusCode == (int)UserStatusCodeEnum.Unknown)
            {
                StatusCode = (int)UserStatusCodeEnum.Normal;
            }
        }

        if (options.IsDeleted.HasValue)
        {
            IsDeleted = options.IsDeleted.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.Remark))
        {
            Remark = options.Remark.Trim();
        }
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} 不能为空。", paramName);
        }

        return value.Trim();
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static List<long> NormalizeIds(IEnumerable<long> ids)
    {
        return ids.Where(id => id > 0).Distinct().ToList();
    }

    private static List<string> NormalizeNames(IEnumerable<string> names)
    {
        return names
            .Select(name => name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

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
    public int UserSex { get; set; } = (int)UserSexEnum.Unknown;

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
    public int StatusCode { get; set; } = (int)UserStatusCodeEnum.Unknown;

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
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = true)]
    public long DepartmentId { get; set; } = 0;

    /// <summary>所在部门名称</summary>
    /// <remarks>不可为空，最大 200 字符</remarks>
    [SugarColumn(Length = 200,IsIgnore = true)]
    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>租户 Id</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

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

/// <summary>用户初始化选项</summary>
public sealed class UserInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="loginName">登录名</param>
    /// <param name="loginPassword">登录密码</param>
    public UserInitializationOptions(string loginName, string loginPassword)
    {
        LoginName = loginName ?? throw new ArgumentNullException(nameof(loginName));
        LoginPassword = loginPassword ?? throw new ArgumentNullException(nameof(loginPassword));
    }

    /// <summary>登录名</summary>
    public string LoginName { get; }

    /// <summary>登录密码</summary>
    public string LoginPassword { get; }

    /// <summary>用户名（用于展示）</summary>
    public string? UserName { get; set; }

    /// <summary>登录邮箱</summary>
    public string? UserEmail { get; set; }

    /// <summary>真实姓名</summary>
    public string? UserRealName { get; set; }

    /// <summary>性别</summary>
    public int? UserSex { get; set; }  // TODO: 后续可考虑改为 UserSex? 枚举类型

    /// <summary>年龄</summary>
    public int? UserAge { get; set; }

    /// <summary>生日</summary>
    public DateTime? UserBirth { get; set; }

    /// <summary>地址</summary>
    public string? UserAddress { get; set; }

    /// <summary>部门 Id</summary>
    public long? DepartmentId { get; set; }

    /// <summary>部门名</summary>
    public string? DepartmentName { get; set; }

    /// <summary>租户 Id</summary>
    public long? TenantId { get; set; }

    /// <summary>角色 Id 列表</summary>
    public IEnumerable<long>? RoleIds { get; set; }

    /// <summary>角色名称列表</summary>
    public IEnumerable<string>? RoleNames { get; set; }

    /// <summary>自定义部门 Id 列表</summary>
    public IEnumerable<long>? DepartmentIds { get; set; }

    /// <summary>是否启用</summary>
    public bool? IsEnable { get; set; }

    /// <summary>是否已删除</summary>
    public bool? IsDeleted { get; set; }

    /// <summary>状态码</summary>
    public int? StatusCode { get; set; }  // TODO: 后续可考虑改为 UserStatusCode? 枚举类型

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
