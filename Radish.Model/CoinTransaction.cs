using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>萝卜币交易记录实体</summary>
/// <remarks>主键为 Id，类型为 long（雪花ID），记录所有萝卜币交易流水</remarks>
[SugarTable("coin_transaction")]
[SugarIndex("idx_from_user_created", nameof(FromUserId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_to_user_created", nameof(ToUserId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_transaction_no", nameof(TransactionNo), OrderByType.Asc)]
[SugarIndex("idx_created_at", nameof(CreateTime), OrderByType.Desc)]
public class CoinTransaction : RootEntityTKey<long>
{
    /// <summary>初始化默认交易记录实例</summary>
    public CoinTransaction()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        TransactionNo = string.Empty;
        FromUserId = null;
        ToUserId = null;
        Amount = 0;
        Fee = 0;
        TransactionType = string.Empty;
        Status = "PENDING";
        BusinessType = string.Empty;
        BusinessId = null;
        Remark = string.Empty;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 交易基础信息

    /// <summary>交易流水号</summary>
    /// <remarks>不可为空，最大 64 字符，全局唯一，格式：TXN_{SnowflakeId}</remarks>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "交易流水号")]
    public string TransactionNo { get; set; } = string.Empty;

    /// <summary>发起方用户 ID</summary>
    /// <remarks>可空，NULL 表示系统发起（如系统赠送、奖励）</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "发起方用户ID")]
    public long? FromUserId { get; set; }

    /// <summary>接收方用户 ID</summary>
    /// <remarks>可空，NULL 表示系统接收（如消费、惩罚扣除）</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "接收方用户ID")]
    public long? ToUserId { get; set; }

    /// <summary>交易金额（胡萝卜）</summary>
    /// <remarks>不可为空，存储单位为胡萝卜（最小单位），必须大于 0</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "交易金额")]
    public long Amount { get; set; } = 0;

    /// <summary>手续费（胡萝卜）</summary>
    /// <remarks>不可为空，默认为 0，转账时收取的手续费</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "手续费")]
    public long Fee { get; set; } = 0;

    #endregion

    #region 交易类型与状态

    /// <summary>交易类型</summary>
    /// <remarks>
    /// 不可为空，最大 50 字符，枚举值：
    /// - SYSTEM_GRANT: 系统赠送
    /// - LIKE_REWARD: 点赞奖励
    /// - COMMENT_REWARD: 评论奖励
    /// - TRANSFER: 用户转账
    /// - TIP: 打赏
    /// - CONSUME: 消费
    /// - REFUND: 退款
    /// - PENALTY: 惩罚扣除
    /// - ADMIN_ADJUST: 管理员调整
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "交易类型")]
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>交易状态</summary>
    /// <remarks>
    /// 不可为空，最大 20 字符，枚举值：
    /// - PENDING: 待处理
    /// - SUCCESS: 成功
    /// - FAILED: 失败
    /// </remarks>
    [SugarColumn(Length = 20, IsNullable = false, ColumnDescription = "交易状态")]
    public string Status { get; set; } = "PENDING";

    #endregion

    #region 业务关联

    /// <summary>业务类型</summary>
    /// <remarks>可空，最大 50 字符（Post/Comment/User/System 等）</remarks>
    [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "业务类型")]
    public string? BusinessType { get; set; } = string.Empty;

    /// <summary>业务 ID</summary>
    /// <remarks>可空（如 PostId、CommentId 等）</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "业务ID")]
    public long? BusinessId { get; set; }

    /// <summary>备注</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "备注")]
    public string? Remark { get; set; } = string.Empty;

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
