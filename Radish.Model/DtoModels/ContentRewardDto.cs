using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

public sealed class CreateContentRewardDto
{
    [Required]
    [StringLength(20)]
    public string TargetType { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long TargetId { get; set; }

    [Required]
    [StringLength(30)]
    public string ReasonCode { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class ContentRewardTargetDto
{
    [Required]
    [StringLength(20)]
    public string TargetType { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long TargetId { get; set; }
}

public sealed class GetContentRewardTargetStatesDto
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public List<ContentRewardTargetDto> Targets { get; set; } = [];
}
