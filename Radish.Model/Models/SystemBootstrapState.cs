using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model.Models;

public sealed class SystemBootstrapState : RootEntityTKey<long>
{
    public const long FirstAdminBootstrapId = 1;

    [SugarColumn(Length = 100, IsNullable = false)]
    public string BootstrapKey { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? CompletedUserId { get; set; }

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? CompletedEmail { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedTime { get; set; }
}
