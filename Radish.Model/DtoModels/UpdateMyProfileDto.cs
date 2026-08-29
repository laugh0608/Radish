namespace Radish.Model.DtoModels;

public class UpdateMyProfileDto
{
    public string? UserName { get; set; }

    public string? UserEmail { get; set; }

    public int? Sex { get; set; }

    public int? Age { get; set; }

    public DateTime? Birth { get; set; }

    /// <summary>
    /// 地址。未传或传 null 时保持原值，传空字符串时清空地址。
    /// </summary>
    public string? Address { get; set; }
}
