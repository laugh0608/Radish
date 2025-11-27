using System;
using Radish.Common.TenantTool;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>
/// 租户表
/// </summary>
/// <remarks>
/// <para>根据 TenantType 分为两种方案:</para>
/// <para>1. 按 TenantId 字段区分</para>
/// <para>2. 按租户分库</para>
/// <para>注意: 使用 TenantId 字段方案，无需配置分库的连接</para>
/// </remarks>
public class Tenant : RootEntityTKey<long>
{
    /// <summary>初始化默认租户实例</summary>
    public Tenant()
    {
        InitializeDefaults();
    }

    /// <summary>通过租户名和租户类型初始化租户</summary>
    /// <param name="tenantName">租户名称</param>
    /// <param name="tenantType">租户类型</param>
    public Tenant(string tenantName, TenantTypeEnum tenantType)
        : this(new TenantInitializationOptions(tenantName, tenantType))
    {
    }

    /// <summary>通过初始化选项批量构造租户</summary>
    /// <param name="options">初始化选项</param>
    public Tenant(TenantInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyConnectionInformation(options);
        ApplyStatusInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        TenantName = "System";
        TenantType = TenantTypeEnum.Id;
        TenantConfigId = string.Empty;
        TenantHost = string.Empty;
        DbType = null;
        DbConnectionStr = string.Empty;
        IsEnable = false;
        TenantRemark = "There is no remark";
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(TenantInitializationOptions options)
    {
        TenantName = NormalizeRequired(options.TenantName, nameof(options.TenantName));
        TenantType = options.TenantType;

        if (!string.IsNullOrWhiteSpace(options.TenantConfigId))
        {
            TenantConfigId = options.TenantConfigId.Trim();
        }
    }

    /// <summary>处理连接信息</summary>
    private void ApplyConnectionInformation(TenantInitializationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.TenantHost))
        {
            TenantHost = options.TenantHost.Trim();
        }

        if (options.DbType.HasValue)
        {
            DbType = options.DbType.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.DbConnectionStr))
        {
            DbConnectionStr = options.DbConnectionStr.Trim();
        }
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(TenantInitializationOptions options)
    {
        if (options.IsEnable.HasValue)
        {
            IsEnable = options.IsEnable.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.TenantRemark))
        {
            TenantRemark = options.TenantRemark.Trim();
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


    /// <summary>
    /// 租户名称
    /// </summary>
    public string TenantName { get; set; } = "System";

    /// <summary>
    /// 租户类型
    /// </summary>
    public TenantTypeEnum TenantType { get; set; }

    /// <summary>
    /// 数据库/租户标识 不可重复
    /// </summary>
    /// <remarks>使用 TenantId 分字段方案，可无需配置</remarks>
    [SugarColumn(Length = 64)]
    public string TenantConfigId { get; set; } = string.Empty;

    /// <summary>
    /// 主机地址
    /// </summary>
    /// <remarks>使用 TenantId 分字段方案，可无需配置</remarks>
    [SugarColumn(IsNullable = true)]
    public string TenantHost { get; set; } = string.Empty;

    /// <summary>
    /// 数据库类型
    /// </summary>
    /// <remarks>使用 TenantId 分字段方案，可无需配置</remarks>
    [SugarColumn(IsNullable = true)]
    public SqlSugar.DbType? DbType { get; set; }

    /// <summary>
    /// 数据库连接字符串
    /// </summary>
    /// <remarks>使用 TenantId 分字段方案，可无需配置</remarks>
    [SugarColumn(IsNullable = true)]
    public string DbConnectionStr { get; set; } = string.Empty;

    /// <summary>
    /// 状态
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public bool IsEnable { get; set; } = false;

    /// <summary>
    /// 备注
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public string TenantRemark { get; set; } = "There is no remark";
}

/// <summary>租户初始化选项</summary>
public sealed class TenantInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="tenantName">租户名称</param>
    /// <param name="tenantType">租户类型</param>
    public TenantInitializationOptions(string tenantName, TenantTypeEnum tenantType)
    {
        TenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName));
        TenantType = tenantType;
    }

    /// <summary>租户名称</summary>
    public string TenantName { get; }

    /// <summary>租户类型</summary>
    public TenantTypeEnum TenantType { get; }

    /// <summary>数据库/租户标识</summary>
    public string? TenantConfigId { get; set; }

    /// <summary>主机地址</summary>
    public string? TenantHost { get; set; }

    /// <summary>数据库类型</summary>
    public SqlSugar.DbType? DbType { get; set; }

    /// <summary>数据库连接字符串</summary>
    public string? DbConnectionStr { get; set; }

    /// <summary>是否启用</summary>
    public bool? IsEnable { get; set; }

    /// <summary>备注</summary>
    public string? TenantRemark { get; set; }
}