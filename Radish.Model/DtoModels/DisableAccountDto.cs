using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

public sealed class DisableAccountDto
{
    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}
