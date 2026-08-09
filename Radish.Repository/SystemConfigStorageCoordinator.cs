using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Radish.Common.CoreTool;
using Radish.Model;

namespace Radish.Repository;

/// <summary>
/// 协调系统配置覆盖值与审计记录的可恢复原子持久化。
/// </summary>
internal static class SystemConfigStorageCoordinator
{
    private const int LockRetryCount = 100;
    private const int LockRetryDelayMilliseconds = 20;
    private static readonly SemaphoreSlim SyncRoot = new(1, 1);
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true
    };

    public static async Task<T> ReadAsync<T>(Func<StorageState, T> reader)
    {
        await SyncRoot.WaitAsync();
        try
        {
            await using var storageLock = await AcquireStorageLockAsync();
            var state = await LoadStateAsync();
            return reader(state);
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    public static async Task<SystemConfigMutationResult> ApplyMutationAsync(SystemConfigMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        await SyncRoot.WaitAsync();
        try
        {
            await using var storageLock = await AcquireStorageLockAsync();
            var state = await LoadStateAsync();
            var currentRecord = state.Configs
                .Where(item => item.Key.Equals(mutation.Key, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.ModifyTime ?? item.CreateTime)
                .FirstOrDefault();
            var storedVersion = currentRecord == null ? 0 : NormalizeVersion(currentRecord.Version);
            var observedDefaultRecordStillMatches = currentRecord != null &&
                mutation.ExpectedVersion == 0 &&
                mutation.ObservedDefaultRecordVersion == storedVersion &&
                mutation.ObservedDefaultRecordValue == currentRecord.Value;
            var currentVersion = currentRecord?.IsEnabled == true && !observedDefaultRecordStillMatches
                ? storedVersion
                : 0;

            if (currentVersion != mutation.ExpectedVersion)
            {
                return new SystemConfigMutationResult
                {
                    IsVersionConflict = true,
                    CurrentVersion = currentVersion,
                    Record = currentRecord == null ? null : CloneConfig(currentRecord)
                };
            }

            if (mutation.ChangeLog == null && IsNoOpMutation(currentRecord, mutation.NextRecord))
            {
                return new SystemConfigMutationResult
                {
                    CurrentVersion = currentVersion,
                    Record = currentRecord == null ? null : CloneConfig(currentRecord)
                };
            }

            state.Configs.RemoveAll(item => item.Key.Equals(mutation.Key, StringComparison.OrdinalIgnoreCase));
            SystemConfigRecord? nextRecord = null;
            if (mutation.NextRecord != null)
            {
                nextRecord = CloneConfig(mutation.NextRecord);
                nextRecord.Id = currentRecord?.Id ?? NextId(state.Configs.Select(item => item.Id));
                nextRecord.Key = mutation.Key;
                nextRecord.Version = currentVersion + 1;
                nextRecord.CreateTime = currentRecord?.CreateTime ?? (nextRecord.CreateTime == default ? DateTime.Now : nextRecord.CreateTime);
                nextRecord.ModifyTime ??= DateTime.Now;
                state.Configs.Add(nextRecord);
            }

            if (mutation.ChangeLog != null)
            {
                var changeLog = CloneChangeLog(mutation.ChangeLog);
                changeLog.Id = NextId(state.ChangeLogs.Select(item => item.Id));
                changeLog.CreateTime = changeLog.CreateTime == default ? DateTime.Now : changeLog.CreateTime;
                state.ChangeLogs.Add(changeLog);
            }

            var journal = new StorageJournal
            {
                Configs = OrderConfigs(state.Configs),
                ChangeLogs = OrderLogs(state.ChangeLogs)
            };
            await SaveJsonAtomicallyAsync(GetJournalPath(), journal);
            await SaveJsonAtomicallyAsync(GetConfigsPath(), journal.Configs);
            await SaveJsonAtomicallyAsync(GetChangeLogsPath(), journal.ChangeLogs);
            File.Delete(GetJournalPath());

            return new SystemConfigMutationResult
            {
                CurrentVersion = nextRecord?.Version ?? 0,
                Record = nextRecord == null ? null : CloneConfig(nextRecord)
            };
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    private static async Task<StorageState> LoadStateAsync()
    {
        Directory.CreateDirectory(GetStorageDirectoryPath());
        await RecoverJournalAsync();

        return new StorageState
        {
            Configs = await LoadJsonAsync<List<SystemConfigRecord>>(GetConfigsPath()) ?? [],
            ChangeLogs = await LoadJsonAsync<List<SystemConfigChangeLogRecord>>(GetChangeLogsPath()) ?? []
        };
    }

    private static async Task RecoverJournalAsync()
    {
        var journalPath = GetJournalPath();
        if (!File.Exists(journalPath))
        {
            return;
        }

        var journal = await LoadJsonAsync<StorageJournal>(journalPath)
            ?? throw new InvalidDataException("系统配置恢复日志为空");
        await SaveJsonAtomicallyAsync(GetConfigsPath(), OrderConfigs(journal.Configs));
        await SaveJsonAtomicallyAsync(GetChangeLogsPath(), OrderLogs(journal.ChangeLogs));
        File.Delete(journalPath);
    }

    private static async Task<T?> LoadJsonAsync<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        var json = await File.ReadAllTextAsync(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"系统配置存储文件为空：{Path.GetFileName(path)}");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"系统配置存储文件损坏：{Path.GetFileName(path)}", exception);
        }
    }

    private static async Task SaveJsonAtomicallyAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(GetStorageDirectoryPath());
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(value, JsonSerializerOptions);
            await File.WriteAllTextAsync(temporaryPath, json);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task<FileStream> AcquireStorageLockAsync()
    {
        Directory.CreateDirectory(GetStorageDirectoryPath());
        for (var attempt = 0; attempt < LockRetryCount; attempt++)
        {
            try
            {
                return new FileStream(GetLockPath(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < LockRetryCount - 1)
            {
                await Task.Delay(LockRetryDelayMilliseconds);
            }
        }

        throw new IOException("无法取得系统配置存储锁");
    }

    private static string GetStorageDirectoryPath() => Path.Combine(AppPathTool.GetDataBasesPath(), "SystemConfigs");

    private static string GetConfigsPath() => Path.Combine(GetStorageDirectoryPath(), "system-configs.json");

    private static string GetChangeLogsPath() => Path.Combine(GetStorageDirectoryPath(), "system-config-change-logs.json");

    private static string GetJournalPath() => Path.Combine(GetStorageDirectoryPath(), "system-config-mutation.json");

    private static string GetLockPath() => Path.Combine(GetStorageDirectoryPath(), ".system-config.lock");

    private static List<SystemConfigRecord> OrderConfigs(IEnumerable<SystemConfigRecord> records) => records.OrderBy(item => item.Id).ToList();

    private static List<SystemConfigChangeLogRecord> OrderLogs(IEnumerable<SystemConfigChangeLogRecord> records) => records.OrderBy(item => item.Id).ToList();

    private static long NextId(IEnumerable<long> ids) => ids.DefaultIfEmpty().Max() + 1;

    private static int NormalizeVersion(int version) => Math.Max(1, version);

    private static bool IsNoOpMutation(SystemConfigRecord? currentRecord, SystemConfigRecord? nextRecord)
    {
        if (nextRecord == null)
        {
            return currentRecord == null;
        }

        return currentRecord != null &&
               currentRecord.IsEnabled == nextRecord.IsEnabled &&
               currentRecord.Category == nextRecord.Category &&
               currentRecord.Name == nextRecord.Name &&
               currentRecord.Value == nextRecord.Value &&
               currentRecord.Description == nextRecord.Description &&
               currentRecord.Type == nextRecord.Type;
    }

    internal static SystemConfigRecord CloneConfig(SystemConfigRecord record)
    {
        return new SystemConfigRecord
        {
            Id = record.Id,
            Category = record.Category,
            Key = record.Key,
            Name = record.Name,
            Value = record.Value,
            Description = record.Description,
            Type = record.Type,
            IsEnabled = record.IsEnabled,
            Version = NormalizeVersion(record.Version),
            CreateTime = record.CreateTime,
            ModifyTime = record.ModifyTime
        };
    }

    internal static SystemConfigChangeLogRecord CloneChangeLog(SystemConfigChangeLogRecord record)
    {
        return new SystemConfigChangeLogRecord
        {
            Id = record.Id,
            Category = record.Category,
            Key = record.Key,
            Name = record.Name,
            ActionType = record.ActionType,
            OldValue = record.OldValue,
            NewValue = record.NewValue,
            DefaultValue = record.DefaultValue,
            Reason = record.Reason,
            RiskLevel = record.RiskLevel,
            EffectiveMode = record.EffectiveMode,
            ConfirmRiskLevel = record.ConfirmRiskLevel,
            ConfirmKey = record.ConfirmKey,
            OperatorUserId = record.OperatorUserId,
            OperatorUserName = record.OperatorUserName,
            RequestIp = record.RequestIp,
            UserAgent = record.UserAgent,
            CreateTime = record.CreateTime
        };
    }

    internal sealed class StorageState
    {
        public List<SystemConfigRecord> Configs { get; init; } = [];

        public List<SystemConfigChangeLogRecord> ChangeLogs { get; init; } = [];
    }

    private sealed class StorageJournal
    {
        public List<SystemConfigRecord> Configs { get; init; } = [];

        public List<SystemConfigChangeLogRecord> ChangeLogs { get; init; } = [];
    }
}
