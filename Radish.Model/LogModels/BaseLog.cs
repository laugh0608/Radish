using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model.LogModels;

/// <summary>日志基础模型类</summary>
public abstract class BaseLog : RootEntityTKey<long>
{
    /// <summary>
    /// 日志记录时间
    /// </summary>
    [SplitField] public DateTime DateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 日志级别
    /// </summary>
    [SugarColumn(IsNullable = true)] public string Level { get; set; } = "Information";

    /// <summary>
    /// 日志信息
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "longtext,text,clob")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 日志信息模板
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "longtext,text,clob")]
    public string MessageTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 日志属性
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "longtext,text,clob")]
    public string Properties { get; set; } = string.Empty;
}