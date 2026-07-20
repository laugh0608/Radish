using System.Text.Json.Serialization;

namespace Radish.Model.DtoModels;

/// <summary>
/// 发布帖子请求DTO
/// </summary>
public class PublishPostDto
{
    /// <summary>帖子标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>帖子内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>客户端提交意图 ID，用于网络重试和重复提交保护</summary>
    public string? ClientSubmissionId { get; set; }

    /// <summary>内容类型（markdown、html、text）</summary>
    public string? ContentType { get; set; }

    /// <summary>分类 ID</summary>
    public long CategoryId { get; set; }

    /// <summary>标签名称列表</summary>
    public List<string>? TagNames { get; set; }

    /// <summary>是否作为问答帖发布</summary>
    public bool IsQuestion { get; set; }

    /// <summary>附带投票（可空）</summary>
    public CreatePollDto? Poll { get; set; }

    /// <summary>附带抽奖（可空）</summary>
    public CreateLotteryDto? Lottery { get; set; }

    /// <summary>
    /// 向后兼容旧字段 tags
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags
    {
        get => TagNames;
        set => TagNames = value;
    }
}
