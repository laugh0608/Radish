using System.Text.Json.Serialization;

namespace Radish.Gateway.PublicHead;

/// <summary>API 返回的公开详情页 HTML head 快照。</summary>
public sealed class PublicHeadSnapshot
{
    [JsonPropertyName("voTitle")]
    public string VoTitle { get; set; } = string.Empty;

    [JsonPropertyName("voDescription")]
    public string VoDescription { get; set; } = string.Empty;

    [JsonPropertyName("voCanonicalPath")]
    public string VoCanonicalPath { get; set; } = string.Empty;

    [JsonPropertyName("voCanonicalUrl")]
    public string VoCanonicalUrl { get; set; } = string.Empty;

    [JsonPropertyName("voOpenGraphType")]
    public string VoOpenGraphType { get; set; } = "article";

    [JsonPropertyName("voImageUrl")]
    public string? VoImageUrl { get; set; }

    [JsonPropertyName("voPublishedAt")]
    public DateTime? VoPublishedAt { get; set; }

    [JsonPropertyName("voModifiedAt")]
    public DateTime? VoModifiedAt { get; set; }

    [JsonPropertyName("voJsonLd")]
    public string VoJsonLd { get; set; } = string.Empty;
}
