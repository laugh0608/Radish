using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

public sealed class UserBlockMutationDto
{
    [Required]
    [StringLength(64)]
    public string TargetUserPublicId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string OperationKey { get; set; } = string.Empty;
}

/// <summary>旧 Direct Block / Unblock 入口的统一关系操作参数。</summary>
public sealed class DirectConversationBlockMutationDto
{
    [Required]
    [StringLength(100)]
    public string OperationKey { get; set; } = string.Empty;
}
