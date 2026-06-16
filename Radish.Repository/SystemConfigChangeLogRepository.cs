using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Radish.Common.CoreTool;
using Radish.IRepository;
using Radish.Model;

namespace Radish.Repository;

/// <summary>
/// 系统设置变更审计仓储
/// </summary>
public class SystemConfigChangeLogRepository : ISystemConfigChangeLogRepository
{
    private readonly string _storageDirectoryPath;
    private readonly string _storageFilePath;
    private readonly SemaphoreSlim _syncRoot = new(1, 1);
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true
    };

    public SystemConfigChangeLogRepository()
    {
        _storageDirectoryPath = Path.Combine(AppPathTool.GetDataBasesPath(), "SystemConfigs");
        _storageFilePath = Path.Combine(_storageDirectoryPath, "system-config-change-logs.json");
    }

    public async Task<List<SystemConfigChangeLogRecord>> GetByKeyAsync(string key, int take)
    {
        await _syncRoot.WaitAsync();
        try
        {
            var records = await LoadRecordsCoreAsync();
            var normalizedTake = Math.Clamp(take, 1, 100);
            return records
                .Where(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.CreateTime)
                .ThenByDescending(item => item.Id)
                .Take(normalizedTake)
                .Select(CloneRecord)
                .ToList();
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    public async Task<SystemConfigChangeLogRecord> CreateAsync(SystemConfigChangeLogRecord record)
    {
        await _syncRoot.WaitAsync();
        try
        {
            var records = await LoadRecordsCoreAsync();
            var nextRecord = CloneRecord(record);
            nextRecord.Id = records.Count == 0 ? 1 : records.Max(item => item.Id) + 1;
            nextRecord.CreateTime = nextRecord.CreateTime == default ? DateTime.Now : nextRecord.CreateTime;
            records.Add(nextRecord);
            await SaveRecordsCoreAsync(records);
            return CloneRecord(nextRecord);
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    private async Task<List<SystemConfigChangeLogRecord>> LoadRecordsCoreAsync()
    {
        Directory.CreateDirectory(_storageDirectoryPath);

        if (!File.Exists(_storageFilePath))
        {
            var records = new List<SystemConfigChangeLogRecord>();
            await SaveRecordsCoreAsync(records);
            return records;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_storageFilePath);
            return string.IsNullOrWhiteSpace(json)
                ? new List<SystemConfigChangeLogRecord>()
                : JsonSerializer.Deserialize<List<SystemConfigChangeLogRecord>>(json, _jsonSerializerOptions) ?? new List<SystemConfigChangeLogRecord>();
        }
        catch (JsonException)
        {
            var records = new List<SystemConfigChangeLogRecord>();
            await SaveRecordsCoreAsync(records);
            return records;
        }
    }

    private async Task SaveRecordsCoreAsync(List<SystemConfigChangeLogRecord> records)
    {
        Directory.CreateDirectory(_storageDirectoryPath);
        var orderedRecords = records
            .OrderBy(item => item.Id)
            .ToList();
        var json = JsonSerializer.Serialize(orderedRecords, _jsonSerializerOptions);
        await File.WriteAllTextAsync(_storageFilePath, json);
    }

    private static SystemConfigChangeLogRecord CloneRecord(SystemConfigChangeLogRecord record)
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
}
