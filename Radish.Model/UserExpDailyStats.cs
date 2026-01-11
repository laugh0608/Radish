using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户经验值每日统计实体</summary>
/// <remarks>主键为 Id，记录用户每日经验值获取统计</remarks>
[SugarTable("UserExpDailyStats")]
[SugarIndex("idx_user_date", nameof(UserId), OrderByType.Asc, nameof(StatDate), OrderByType.Desc)]
public class UserExpDailyStats : RootEntityTKey<long>
{
    /// <summary>初始化默认每日统计实例</summary>
    public UserExpDailyStats()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        UserId = 0;
        StatDate = DateTime.Today;
        ExpEarned = 0;
        ExpFromPost = 0;
        ExpFromComment = 0;
        ExpFromLike = 0;
        ExpFromHighlight = 0;
        ExpFromLogin = 0;
        PostCount = 0;
        CommentCount = 0;
        LikeGivenCount = 0;
        LikeReceivedCount = 0;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 统计基础信息

    /// <summary>用户 ID</summary>
    /// <remarks>不可为空，外键关联 User 表</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; } = 0;

    /// <summary>统计日期</summary>
    /// <remarks>不可为空，格式 yyyy-MM-dd</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "统计日期")]
    public DateTime StatDate { get; set; } = DateTime.Today;

    #endregion

    #region 经验值统计

    /// <summary>当日获得经验值总计</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "当日获得经验值")]
    public long ExpEarned { get; set; } = 0;

    /// <summary>来自发帖的经验值</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "来自发帖")]
    public int ExpFromPost { get; set; } = 0;

    /// <summary>来自评论的经验值</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "来自评论")]
    public int ExpFromComment { get; set; } = 0;

    /// <summary>来自点赞的经验值</summary>
    /// <remarks>不可为空，默认为 0（包括点赞他人和被点赞）</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "来自点赞")]
    public int ExpFromLike { get; set; } = 0;

    /// <summary>来自神评/沙发的经验值</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "来自神评沙发")]
    public int ExpFromHighlight { get; set; } = 0;

    /// <summary>来自登录的经验值</summary>
    /// <remarks>不可为空，默认为 0（包括每日登录和连续登录）</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "来自登录")]
    public int ExpFromLogin { get; set; } = 0;

    #endregion

    #region 行为统计

    /// <summary>当日发帖数</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "当日发帖数")]
    public int PostCount { get; set; } = 0;

    /// <summary>当日评论数</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "当日评论数")]
    public int CommentCount { get; set; } = 0;

    /// <summary>当日点赞数</summary>
    /// <remarks>不可为空，默认为 0（点赞他人）</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "当日点赞数")]
    public int LikeGivenCount { get; set; } = 0;

    /// <summary>当日被点赞数</summary>
    /// <remarks>不可为空，默认为 0（被他人点赞）</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "当日被点赞数")]
    public int LikeReceivedCount { get; set; } = 0;

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
