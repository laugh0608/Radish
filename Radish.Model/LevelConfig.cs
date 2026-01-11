using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>等级配置实体</summary>
/// <remarks>主键为 Level，配置每个等级的信息（昵称、经验值要求、奖励、特权等）</remarks>
[SugarTable("LevelConfig")]
[SugarIndex("idx_level", nameof(Level), OrderByType.Asc)]
[SugarIndex("idx_sort_order", nameof(SortOrder), OrderByType.Asc)]
public class LevelConfig : RootEntityTKey<int>
{
    /// <summary>初始化默认等级配置实例</summary>
    public LevelConfig()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        Level = 0;
        LevelName = string.Empty;
        ExpRequired = 0;
        ExpCumulative = 0;
        ThemeColor = string.Empty;
        IconUrl = string.Empty;
        BadgeUrl = string.Empty;
        Description = string.Empty;
        Privileges = string.Empty;
        IsEnabled = true;
        SortOrder = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 等级基础信息

    /// <summary>等级</summary>
    /// <remarks>不可为空，主键，范围 0-10（凡人 到 飞升）</remarks>
    [SugarColumn(IsNullable = false, IsPrimaryKey = true, ColumnDescription = "等级")]
    public int Level { get; set; } = 0;

    /// <summary>等级昵称</summary>
    /// <remarks>不可为空，最大 50 字符（凡人/练气/筑基/金丹/元婴/化神/炼虚/合体/大乘/渡劫/飞升）</remarks>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "等级昵称")]
    public string LevelName { get; set; } = string.Empty;

    /// <summary>升到下一级所需经验值</summary>
    /// <remarks>不可为空，0 表示已达到最高等级</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "升到下一级所需经验值")]
    public long ExpRequired { get; set; } = 0;

    /// <summary>累计经验值</summary>
    /// <remarks>不可为空，达到此等级需要的总经验值</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "累计经验值")]
    public long ExpCumulative { get; set; } = 0;

    #endregion

    #region 视觉效果

    /// <summary>主题色</summary>
    /// <remarks>可空，最大 20 字符（十六进制颜色值，如 #FFC107）</remarks>
    [SugarColumn(Length = 20, IsNullable = true, ColumnDescription = "主题色")]
    public string? ThemeColor { get; set; } = string.Empty;

    /// <summary>等级图标 URL</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "等级图标URL")]
    public string? IconUrl { get; set; } = string.Empty;

    /// <summary>等级徽章 URL</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "等级徽章URL")]
    public string? BadgeUrl { get; set; } = string.Empty;

    #endregion

    #region 描述与特权

    /// <summary>等级描述</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "等级描述")]
    public string? Description { get; set; } = string.Empty;

    /// <summary>特权列表</summary>
    /// <remarks>可空，JSON 格式存储，如 ["自定义主题", "专属勋章"]</remarks>
    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "特权列表(JSON)")]
    public string? Privileges { get; set; } = string.Empty;

    #endregion

    #region 配置管理

    /// <summary>是否启用</summary>
    /// <remarks>不可为空，默认为 true</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否启用")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>排序</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "排序")]
    public int SortOrder { get; set; } = 0;

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
