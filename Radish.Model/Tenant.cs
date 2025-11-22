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