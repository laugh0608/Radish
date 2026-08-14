namespace Radish.Model.DtoModels;

/// <summary>Console 用户列表权威查询参数。</summary>
public sealed class ConsoleUserListQueryDto
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public bool? IsEnabled { get; set; }

    public string? RoleName { get; set; }
}
