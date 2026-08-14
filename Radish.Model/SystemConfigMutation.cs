namespace Radish.Model;

/// <summary>
/// 系统配置覆盖值与审计记录的原子变更命令
/// </summary>
public class SystemConfigMutation
{
    public string Key { get; set; } = string.Empty;

    public int ExpectedVersion { get; set; }

    public int? ObservedDefaultRecordVersion { get; set; }

    public string? ObservedDefaultRecordValue { get; set; }

    public SystemConfigRecord? NextRecord { get; set; }

    public SystemConfigChangeLogRecord? ChangeLog { get; set; }
}

/// <summary>
/// 系统配置原子变更结果
/// </summary>
public class SystemConfigMutationResult
{
    public bool IsVersionConflict { get; set; }

    public int CurrentVersion { get; set; }

    public SystemConfigRecord? Record { get; set; }
}
