using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户声明表</summary>
/// <remarks>用于存储 OIDC Claims 和其他自定义声明</remarks>
public class UserClaim : RootEntityTKey<long>
{
    /// <summary>初始化默认用户声明实例</summary>
    public UserClaim()
    {
        InitializeDefaults();
    }

    /// <summary>通过用户 Id、声明类型和声明值初始化用户声明</summary>
    /// <param name="userId">用户 Id</param>
    /// <param name="claimType">声明类型</param>
    /// <param name="claimValue">声明值</param>
    public UserClaim(long userId, string claimType, string claimValue)
        : this(new UserClaimInitializationOptions(userId, claimType, claimValue))
    {
    }

    /// <summary>通过初始化选项批量构造用户声明</summary>
    /// <param name="options">初始化选项</param>
    public UserClaim(UserClaimInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyStatusInformation(options);
        ApplyCreatorInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        UserId = 0;
        ClaimType = string.Empty;
        ClaimValue = string.Empty;
        IsDeleted = false;
        CreateBy = "System";
        CreateTime = DateTime.Now;
        ModifyBy = null;
        ModifyTime = null;
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(UserClaimInitializationOptions options)
    {
        if (options.UserId <= 0)
        {
            throw new ArgumentException("UserId 必须大于 0。", nameof(options.UserId));
        }

        UserId = options.UserId;
        ClaimType = NormalizeRequired(options.ClaimType, nameof(options.ClaimType));
        ClaimValue = NormalizeRequired(options.ClaimValue, nameof(options.ClaimValue));
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(UserClaimInitializationOptions options)
    {
        if (options.IsDeleted.HasValue)
        {
            IsDeleted = options.IsDeleted.Value;
        }
    }

    /// <summary>处理创建者信息</summary>
    private void ApplyCreatorInformation(UserClaimInitializationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CreateBy))
        {
            CreateBy = options.CreateBy.Trim();
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


    /// <summary>用户 Id</summary>
    /// <remarks>不可为空，关联到 User 表</remarks>
    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; } = 0;

    /// <summary>声明类型</summary>
    /// <remarks>
    /// <para>不可为空，最大 200 字符</para>
    /// <para>如：name, email, role, tenant_id, phone_number 等</para>
    /// </remarks>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string ClaimType { get; set; } = string.Empty;

    /// <summary>声明值</summary>
    /// <remarks>不可为空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string ClaimValue { get; set; } = string.Empty;

    /// <summary>是否已被删除</summary>
    /// <remarks>不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = true)]
    public bool IsDeleted { get; set; } = false;

    /// <summary>创建者的名称</summary>
    /// <remarks>不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间，更新时忽略该列</remarks>
    [SugarColumn(IsNullable = true, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>更新者名称</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>更新时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }
}

/// <summary>用户声明初始化选项</summary>
public sealed class UserClaimInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="userId">用户 Id</param>
    /// <param name="claimType">声明类型</param>
    /// <param name="claimValue">声明值</param>
    public UserClaimInitializationOptions(long userId, string claimType, string claimValue)
    {
        UserId = userId;
        ClaimType = claimType ?? throw new ArgumentNullException(nameof(claimType));
        ClaimValue = claimValue ?? throw new ArgumentNullException(nameof(claimValue));
    }

    /// <summary>用户 Id</summary>
    public long UserId { get; }

    /// <summary>声明类型</summary>
    public string ClaimType { get; }

    /// <summary>声明值</summary>
    public string ClaimValue { get; }

    /// <summary>是否已被删除</summary>
    public bool? IsDeleted { get; set; }

    /// <summary>创建者的名称</summary>
    public string? CreateBy { get; set; }
}
