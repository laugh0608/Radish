using System.ComponentModel;

namespace Radish.Common.TenantTool;

/// <summary>
/// 租户隔离方案
/// </summary>
public enum TenantTypeEnum
{
    /// <summary>
    /// 不隔离
    /// </summary>
    None = 0,

    // 这个 Id 隔离实际上用不上的，需要隔离的实体类只需要继承 ITenantEntity 接口即可
    // 例如 BusinessTable : ITenantEntity
    
    /// <summary>
    /// TenantId 隔离
    /// </summary>
    /// <remarks>在单个表中，通过字段 TenantId 隔离不同租户的数据行</remarks>
    [Description("TenantId 隔离")]
    TenantIds = 1,

    /// <summary>
    /// 表隔离
    /// </summary>
    /// <remarks>在单个数据库中，通过不同的表后缀分表隔离不同租户的数据行</remarks>
    [Description("表隔离")]
    Tables = 2,

    /// <summary>
    /// 库隔离
    /// </summary>
    /// <remarks>不同租户的数据存放在不同的数据库后缀的同一个表中</remarks>
    [Description("库隔离")]
    DataBases = 3,
}