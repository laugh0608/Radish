using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

public sealed class SetPostBookmarkStateDto
{
    [Required]
    [StringLength(64)]
    public string PostIdentifier { get; set; } = string.Empty;

    public bool IsBookmarked { get; set; }
}

public sealed class RemovePostBookmarkDto
{
    [Required]
    [StringLength(64)]
    public string BookmarkIdentifier { get; set; } = string.Empty;
}
