using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户经验值实体</summary>
/// <remarks>主键为 UserId，记录用户的等级和经验值信息</remarks>
[SugarTable("UserExperience")]
[SugarIndex("idx_level", nameof(CurrentLevel), OrderByType.Desc)]
[SugarIndex("idx_total_exp", nameof(TotalExp), OrderByType.Desc)]
public class UserExperience : RootEntityTKey<long>
{
    /// <summary>初始化默认用户经验值实例</summary>
    public UserExperience()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        CurrentLevel = 0;
        CurrentExp = 0;
        TotalExp = 0;
        LevelUpAt = null;
        ExpFrozen = false;
        FrozenUntil = null;
        FrozenReason = string.Empty;
        Version = 0;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 经验值信息

    /// <summary>当前等级</summary>
    /// <remarks>不可为空，默认为 0（凡人），范围 0-10</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "当前等级")]
    public int CurrentLevel { get; set; } = 0;

    /// <summary>当前经验值</summary>
    /// <remarks>不可为空，默认为 0，表示当前等级内的经验值进度</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "当前经验值")]
    public long CurrentExp { get; set; } = 0;

    /// <summary>累计总经验值</summary>
    /// <remarks>不可为空，默认为 0，包含所有历史获得的经验值</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "累计总经验值")]
    public long TotalExp { get; set; } = 0;

    /// <summary>最近升级时间</summary>
    /// <remarks>可空，NULL 表示从未升级</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "最近升级时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? LevelUpAt { get; set; }

    #endregion

    #region 防刷与惩罚

    /// <summary>经验值是否冻结</summary>
    /// <remarks>不可为空，默认为 false，作弊惩罚时设为 true</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "经验值是否冻结")]
    public bool ExpFrozen { get; set; } = false;

    /// <summary>冻结到期时间</summary>
    /// <remarks>可空，NULL 表示未冻结或永久冻结</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "冻结到期时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? FrozenUntil { get; set; }

    /// <summary>冻结原因</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "冻结原因")]
    public string? FrozenReason { get; set; } = string.Empty;

    #endregion

    #region 并发控制

    /// <summary>乐观锁版本号</summary>
    /// <remarks>不可为空，默认为 0，每次更新时自增</remarks>
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
}
