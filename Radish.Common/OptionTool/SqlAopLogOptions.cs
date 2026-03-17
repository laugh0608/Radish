using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>SqlSugar AOP 日志配置</summary>
public sealed class SqlAopLogOptions : IConfigurableOptions
{
    /// <summary>是否启用 SQL AOP 日志</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>是否记录查询日志</summary>
    public bool LogQuery { get; set; } = true;

    /// <summary>是否记录新增日志</summary>
    public bool LogInsert { get; set; } = true;

    /// <summary>是否记录更新日志</summary>
    public bool LogUpdate { get; set; } = true;

    /// <summary>是否记录删除日志</summary>
    public bool LogDelete { get; set; } = true;

    /// <summary>是否省略超长文本内容</summary>
    public bool OmitLargeText { get; set; } = true;

    /// <summary>超长文本阈值</summary>
    public int LargeTextThreshold { get; set; } = 256;

    /// <summary>强制省略正文的字段名</summary>
    public List<string> OmittedFields { get; set; } =
    [
        "MarkdownContent",
        "Content",
        "Body",
        "HtmlContent",
        "RequestBody",
        "ResponseBody",
        "OldContent",
        "NewContent",
        "ContentSnapshot"
    ];

    /// <summary>直接跳过日志记录的表名</summary>
    public List<string> SkipTables { get; set; } = [];

    /// <summary>直接跳过日志记录的操作人</summary>
    public List<string> SkipUsers { get; set; } = [];
}
