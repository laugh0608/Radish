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

    private async Task<List<SystemConfigRecord>> LoadRecordsCoreAsync()
    {
        Directory.CreateDirectory(_storageDirectoryPath);

        if (!File.Exists(_storageFilePath))
        {
            var seededRecords = CreateSeedRecords();
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
            records = CreateSeedRecords();
            await SaveRecordsCoreAsync(records);
            return records;
        }

        if (EnsureRequiredRecords(records))
        {
            await SaveRecordsCoreAsync(records);
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

    private static bool EnsureRequiredRecords(List<SystemConfigRecord> records)
    {
        var hasFaviconConfig = records.Any(item => item.Key.Equals(SystemConfigDefaults.SiteFaviconKey, StringComparison.OrdinalIgnoreCase));
        if (hasFaviconConfig)
        {
            return false;
        }

        var nextId = records.Count == 0 ? 1 : records.Max(item => item.Id) + 1;
        records.Add(CreateRecord(
            nextId,
            SystemConfigDefaults.SiteBrandingCategory,
            SystemConfigDefaults.SiteFaviconKey,
            SystemConfigDefaults.SiteFaviconName,
            SystemConfigDefaults.DefaultSiteFaviconPath,
            "浏览器标签页显示的网站图标，默认使用 DataBases/Uploads/DefaultIco/bailuobo.ico",
            "string",
            true,
            DateTime.Now.AddDays(-1),
            DateTime.Now
        ));
        return true;
    }

    private static List<SystemConfigRecord> CreateSeedRecords()
    {
        return
        [
            CreateRecord(1, "商城配置", "Shop.OrderTimeoutMinutes", "订单超时时间", "30", "订单超时时间（分钟）", "number", true, DateTime.Now.AddDays(-30), DateTime.Now.AddDays(-1)),
            CreateRecord(2, "商城配置", "Shop.StockWarningThreshold", "库存预警阈值", "10", "商品库存低于此值时发出预警", "number", true, DateTime.Now.AddDays(-30), DateTime.Now.AddDays(-2)),
            CreateRecord(3, "萝卜币配置", "Coin.DailyRewardLimit", "每日奖励上限", "100", "用户每日可获得的萝卜币奖励上限", "number", true, DateTime.Now.AddDays(-25), DateTime.Now.AddDays(-3)),
            CreateRecord(4, "萝卜币配置", "Coin.PostReward", "发帖奖励", "5", "用户发帖可获得的萝卜币奖励", "number", true, DateTime.Now.AddDays(-25), DateTime.Now.AddDays(-4)),
            CreateRecord(5, "经验值配置", "Experience.PostReward", "发帖经验奖励", "10", "用户发帖可获得的经验值", "number", true, DateTime.Now.AddDays(-20), DateTime.Now.AddDays(-5)),
            CreateRecord(6, "经验值配置", "Experience.CommentReward", "评论经验奖励", "5", "用户评论可获得的经验值", "number", true, DateTime.Now.AddDays(-20), DateTime.Now.AddDays(-6)),
            CreateRecord(7, "通知配置", "Notification.Enable", "启用通知推送", "true", "是否启用实时通知推送", "boolean", true, DateTime.Now.AddDays(-15), DateTime.Now.AddDays(-7)),
            CreateRecord(8, "通知配置", "Notification.DedupWindowMinutes", "通知去重时间窗口", "5", "相同类型通知的去重时间窗口（分钟）", "number", true, DateTime.Now.AddDays(-15), DateTime.Now.AddDays(-8)),
            CreateRecord(9, "文件上传", "FileUpload.MaxImageSize", "图片最大大小", "5242880", "图片文件最大大小（字节），默认 5MB", "number", true, DateTime.Now.AddDays(-10), DateTime.Now.AddDays(-9)),
            CreateRecord(10, "文件上传", "FileUpload.AllowedImageTypes", "允许的图片类型", "jpg,jpeg,png,gif,webp,bmp,svg,ico", "允许上传的图片文件类型", "string", true, DateTime.Now.AddDays(-10), DateTime.Now.AddDays(-10)),
            CreateRecord(11, SystemConfigDefaults.SiteBrandingCategory, SystemConfigDefaults.SiteFaviconKey, SystemConfigDefaults.SiteFaviconName, SystemConfigDefaults.DefaultSiteFaviconPath, "浏览器标签页显示的网站图标，默认使用 DataBases/Uploads/DefaultIco/bailuobo.ico", "string", true, DateTime.Now.AddDays(-5), DateTime.Now.AddDays(-1))
        ];
    }

    private static SystemConfigRecord CreateRecord(
        long id,
        string category,
        string key,
        string name,
        string value,
        string? description,
        string type,
        bool isEnabled,
        DateTime createTime,
        DateTime? modifyTime)
    {
        return new SystemConfigRecord
        {
            Id = id,
            Category = category,
            Key = key,
            Name = name,
            Value = value,
            Description = description,
            Type = type,
            IsEnabled = isEnabled,
            CreateTime = createTime,
            ModifyTime = modifyTime
        };
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
