namespace Radish.Model.ViewModels;

public class UpdateMyProfileDto
{
    public string? UserName { get; set; }

    public string? UserEmail { get; set; }

    public string? RealName { get; set; }

    public int? Sex { get; set; }

    public int? Age { get; set; }

    public DateTime? Birth { get; set; }

    public string? Address { get; set; }
}
