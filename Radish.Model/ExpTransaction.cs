using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>经验值交易记录实体</summary>
/// <remarks>主键为 Id，类型为 long（雪花ID），记录所有经验值变动流水</remarks>
[SugarTable("ExpTransaction")]
[SugarIndex("idx_user_time", nameof(UserId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_exp_type", nameof(ExpType), OrderByType.Asc)]
[SugarIndex("idx_created_at", nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_dedup", nameof(UserId), OrderByType.Asc, nameof(ExpType), OrderByType.Asc, nameof(BusinessType), OrderByType.Asc, nameof(BusinessId), OrderByType.Asc, nameof(CreatedDate), OrderByType.Asc)]
public class ExpTransaction : RootEntityTKey<long>
{
    /// <summary>初始化默认经验值交易记录实例</summary>
    public ExpTransaction()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        UserId = 0;
        ExpType = string.Empty;
        ExpAmount = 0;
        BusinessType = string.Empty;
        BusinessId = null;
        Remark = string.Empty;
        ExpBefore = 0;
        ExpAfter = 0;
        LevelBefore = 0;
        LevelAfter = 0;
        CreatedDate = DateTime.Today;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 交易基础信息

    /// <summary>用户 ID</summary>
    /// <remarks>不可为空，外键关联 User 表</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; } = 0;

    /// <summary>经验值类型</summary>
    /// <remarks>
    /// 不可为空，最大 50 字符，枚举值：
    /// - POST_CREATE: 发布帖子
    /// - POST_LIKED: 帖子被点赞
    /// - COMMENT_CREATE: 发布评论
    /// - COMMENT_LIKED: 评论被点赞
    /// - COMMENT_REPLIED: 评论被回复
    /// - LIKE_OTHERS: 给他人点赞
    /// - GOD_COMMENT: 成为神评
    /// - SOFA_COMMENT: 成为沙发
    /// - DAILY_LOGIN: 每日登录
    /// - WEEKLY_LOGIN: 连续登录(周)
    /// - PROFILE_COMPLETE: 完善资料
    /// - FIRST_POST: 首次发帖
    /// - FIRST_COMMENT: 首次评论
    /// - ADMIN_ADJUST: 管理员调整
    /// - PENALTY: 惩罚扣除
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "经验值类型")]
    public string ExpType { get; set; } = string.Empty;

    /// <summary>经验值变动量</summary>
    /// <remarks>不可为空，正数表示增加，负数表示扣除</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "经验值变动量")]
    public int ExpAmount { get; set; } = 0;

    #endregion

    #region 业务关联

    /// <summary>业务类型</summary>
    /// <remarks>可空，最大 50 字符（Post/Comment/User 等）</remarks>
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

    #region 快照信息

    /// <summary>变动前经验值</summary>
    /// <remarks>不可为空，记录变动前的累计经验值</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "变动前经验值")]
    public long ExpBefore { get; set; } = 0;

    /// <summary>变动后经验值</summary>
    /// <remarks>不可为空，记录变动后的累计经验值</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "变动后经验值")]
    public long ExpAfter { get; set; } = 0;

    /// <summary>变动前等级</summary>
    /// <remarks>不可为空，记录变动前的用户等级</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "变动前等级")]
    public int LevelBefore { get; set; } = 0;

    /// <summary>变动后等级</summary>
    /// <remarks>不可为空，记录变动后的用户等级</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "变动后等级")]
    public int LevelAfter { get; set; } = 0;

    #endregion

    #region 去重标识

    /// <summary>创建日期</summary>
    /// <remarks>不可为空，用于每日上限和去重控制</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "创建日期")]
    public DateTime CreatedDate { get; set; } = DateTime.Today;

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
