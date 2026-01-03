using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>余额变动日志实体</summary>
/// <remarks>主键为 Id，类型为 long（雪花ID），记录每笔交易的账户分录</remarks>
[SugarTable("balance_change_log")]
[SugarIndex("idx_user_time", nameof(UserId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_transaction", nameof(TransactionId), OrderByType.Asc)]
public class BalanceChangeLog : RootEntityTKey<long>
{
    /// <summary>初始化默认余额变动日志实例</summary>
    public BalanceChangeLog()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        UserId = 0;
        TransactionId = 0;
        ChangeAmount = 0;
        BalanceBefore = 0;
        BalanceAfter = 0;
        ChangeType = string.Empty;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 变动信息

    /// <summary>用户 ID</summary>
    /// <remarks>不可为空，余额变动所属用户</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; } = 0;

    /// <summary>关联交易记录 ID</summary>
    /// <remarks>不可为空，关联到 coin_transaction 表的 ID</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "关联交易记录ID")]
    public long TransactionId { get; set; } = 0;

    /// <summary>变动金额（胡萝卜）</summary>
    /// <remarks>不可为空，正数表示增加，负数表示减少</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "变动金额")]
    public long ChangeAmount { get; set; } = 0;

    /// <summary>变动前余额（胡萝卜）</summary>
    /// <remarks>不可为空，用于对账和审计</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "变动前余额")]
    public long BalanceBefore { get; set; } = 0;

    /// <summary>变动后余额（胡萝卜）</summary>
    /// <remarks>不可为空，用于对账和审计</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "变动后余额")]
    public long BalanceAfter { get; set; } = 0;

    /// <summary>变动类型</summary>
    /// <remarks>
    /// 不可为空，最大 50 字符，枚举值：
    /// - EARN: 获得（系统赠送、奖励）
    /// - SPEND: 消费
    /// - TRANSFER_IN: 转账收入
    /// - TRANSFER_OUT: 转账支出
    /// - REFUND: 退款
    /// - PENALTY: 惩罚扣除
    /// - ADMIN_ADJUST: 管理员调整
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "变动类型")]
    public string ChangeType { get; set; } = string.Empty;

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
}
