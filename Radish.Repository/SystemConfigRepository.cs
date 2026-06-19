using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Radish.Common.CoreTool;
using Radish.IRepository;
using Radish.Model;

namespace Radish.Repository;

/// <summary>
/// 系统配置仓储
/// </summary>
public class SystemConfigRepository : ISystemConfigRepository
{
    private readonly string _storageDirectoryPath;
    private readonly string _storageFilePath;
    private readonly SemaphoreSlim _syncRoot = new(1, 1);
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true
    };

    public SystemConfigRepository()
    {
        _storageDirectoryPath = Path.Combine(AppPathTool.GetDataBasesPath(), "SystemConfigs");
        _storageFilePath = Path.Combine(_storageDirectoryPath, "system-configs.json");
    }

    public async Task<List<SystemConfigRecord>> GetAllAsync()
    {
        await _syncRoot.WaitAsync();
        try
        {
            var records = await LoadRecordsCoreAsync();
            return records.Select(CloneRecord).ToList();
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    public async Task<SystemConfigRecord?> GetByIdAsync(long id)
    {
        await _syncRoot.WaitAsync();
        try
        {
            var records = await LoadRecordsCoreAsync();
            var record = records.FirstOrDefault(item => item.Id == id);
            return record == null ? null : CloneRecord(record);
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    public async Task<SystemConfigRecord?> GetByKeyAsync(string key)
    {
        await _syncRoot.WaitAsync();
        try
        {
            var records = await LoadRecordsCoreAsync();
            var record = records.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            return record == null ? null : CloneRecord(record);
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    public async Task<SystemConfigRecord> CreateAsync(SystemConfigRecord record)
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

    public async Task<SystemConfigRecord?> UpdateAsync(SystemConfigRecord record)
    {
        await _syncRoot.WaitAsync();
        try
        {
            var records = await LoadRecordsCoreAsync();
            var targetIndex = records.FindIndex(item => item.Id == record.Id);
            if (targetIndex < 0)
            {
                return null;
            }

            var nextRecord = CloneRecord(record);
            if (!nextRecord.ModifyTime.HasValue)
            {
                nextRecord.ModifyTime = DateTime.Now;
            }

            records[targetIndex] = nextRecord;
            await SaveRecordsCoreAsync(records);
            return CloneRecord(nextRecord);
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    public async Task<bool> DeleteAsync(long id)
    {
        await _syncRoot.WaitAsync();
        try
        {
            var records = await LoadRecordsCoreAsync();
            var removed = records.RemoveAll(item => item.Id == id) > 0;
            if (!removed)
            {
                return false;
            }

            await SaveRecordsCoreAsync(records);
            return true;
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    public async Task<bool> DeleteByKeyAsync(string key)
    {
        await _syncRoot.WaitAsync();
        try
        {
            var records = await LoadRecordsCoreAsync();
            var removed = records.RemoveAll(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removed)
            {
                return false;
            }

            await SaveRecordsCoreAsync(records);
            return true;
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    private async Task<List<SystemConfigRecord>> LoadRecordsCoreAsync()
    {
        Directory.CreateDirectory(_storageDirectoryPath);

        if (!File.Exists(_storageFilePath))
        {
            var seededRecords = new List<SystemConfigRecord>();
            await SaveRecordsCoreAsync(seededRecords);
            return seededRecords;
        }

        List<SystemConfigRecord>? records;
        try
        {
            var json = await File.ReadAllTextAsync(_storageFilePath);
            records = string.IsNullOrWhiteSpace(json)
                ? new List<SystemConfigRecord>()
                : JsonSerializer.Deserialize<List<SystemConfigRecord>>(json, _jsonSerializerOptions) ?? new List<SystemConfigRecord>();
        }
        catch (JsonException)
        {
            records = new List<SystemConfigRecord>();
            await SaveRecordsCoreAsync(records);
            return records;
        }

        return records;
    }

    private async Task SaveRecordsCoreAsync(List<SystemConfigRecord> records)
    {
        Directory.CreateDirectory(_storageDirectoryPath);
        var orderedRecords = records
            .OrderBy(item => item.Id)
            .ToList();
        var json = JsonSerializer.Serialize(orderedRecords, _jsonSerializerOptions);
        await File.WriteAllTextAsync(_storageFilePath, json);
    }

    private static SystemConfigRecord CloneRecord(SystemConfigRecord record)
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
            CreateTime = record.CreateTime,
            ModifyTime = record.ModifyTime
        };
    }
}
