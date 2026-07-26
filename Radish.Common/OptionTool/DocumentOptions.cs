using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>文档系统配置</summary>
public sealed class DocumentOptions : IConfigurableOptions
{
    /// <summary>是否展示固定文档</summary>
    public bool ShowBuiltInDocs { get; set; } = true;

    /// <summary>固定文档根目录（相对于解决方案根目录）</summary>
    public string BuiltInDocsPath { get; set; } = "Docs";

    /// <summary>固定文档静态资源访问前缀</summary>
    public string StaticAssetsRequestPath { get; set; } = "/docs-assets";

    public WikiAuthoringOptions Authoring { get; set; } = new();
}

public sealed class WikiAuthoringOptions
{
    public int MaxMarkdownUtf8Bytes { get; set; } = 1_048_576;
    public int MaxActiveOwnedDrafts { get; set; } = 20;
    public int MaxCollaboratorsPerDocument { get; set; } = 20;
    public int TerminalDraftRetentionDays { get; set; } = 90;
}
