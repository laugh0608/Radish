using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户余额实体</summary>
/// <remarks>主键为 UserId，类型为 long，萝卜币系统核心表</remarks>
[SugarTable("UserBalance")]
public class UserBalance : RootEntityTKey<long>
{
    /// <summary>初始化默认用户余额实例</summary>
    public UserBalance()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        Balance = 0;
        FrozenBalance = 0;
        TotalEarned = 0;
        TotalSpent = 0;
        TotalTransferredIn = 0;
        TotalTransferredOut = 0;
        Version = 0;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 余额信息

    /// <summary>可用余额（胡萝卜）</summary>
    /// <remarks>不可为空，默认为 0，存储单位为胡萝卜（最小单位）</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "可用余额")]
    public long Balance { get; set; } = 0;

    /// <summary>冻结余额（胡萝卜）</summary>
    /// <remarks>不可为空，默认为 0，冻结的余额不可使用</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "冻结余额")]
    public long FrozenBalance { get; set; } = 0;

    #endregion

    #region 统计信息

    /// <summary>累计获得（胡萝卜）</summary>
    /// <remarks>不可为空，默认为 0，包括系统赠送、奖励等所有收入</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "累计获得")]
    public long TotalEarned { get; set; } = 0;

    /// <summary>累计消费（胡萝卜）</summary>
    /// <remarks>不可为空，默认为 0，包括购买商品、置顶等所有消费</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "累计消费")]
    public long TotalSpent { get; set; } = 0;

    /// <summary>累计转入（胡萝卜）</summary>
    /// <remarks>不可为空，默认为 0，从其他用户转账收到的金额</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "累计转入")]
    public long TotalTransferredIn { get; set; } = 0;

    /// <summary>累计转出（胡萝卜）</summary>
    /// <remarks>不可为空，默认为 0，转账给其他用户的金额</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "累计转出")]
    public long TotalTransferredOut { get; set; } = 0;

    #endregion

    #region 并发控制

    /// <summary>乐观锁版本号</summary>
    /// <remarks>不可为空，默认为 0，每次更新时自动递增，防止并发冲突</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "乐观锁版本号")]
    public int Version { get; set; } = 0;

    #endregion

    #region 租户信息

    /// <summary>租户 Id</summary>
    /// <remarks>不可为空，默认为 0，支持多租户隔离</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "租户ID")]
    public long TenantId { get; set; } = 0;

    #endregion

    #region 审计信息

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间，更新时忽略该列</remarks>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者名称</summary>
    /// <remarks>不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "创建者名称")]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 Id</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "创建者ID")]
    public long CreateId { get; set; } = 0;

    /// <summary>修改时间</summary>
    /// <remarks>可空</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者名称</summary>
    /// <remarks>可空，最大 50 字符</remarks>
    [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "修改者名称")]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 Id</summary>
    /// <remarks>可空</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改者ID")]
    public long? ModifyId { get; set; }

    #endregion

    #region 索引

    // 索引通过 SqlSugar Code First 自动创建
    // - idx_balance: Balance 降序（用于排行榜）
    // - idx_updated_at: ModifyTime 降序（用于查询最近更新）

    #endregion
}
