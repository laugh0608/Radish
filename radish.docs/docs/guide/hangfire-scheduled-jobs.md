# Hangfire 定时任务指南

> **状态**：✅ 已实现
> **最后更新**：2025-12-22
> **维护者**：Radish Team

## 📋 概述

Hangfire 是一个开源的 .NET 后台作业处理框架，支持定时任务、延迟任务和 recurring 任务。Radish 项目集成 Hangfire 用于执行文件清理等维护性任务。

**核心特性**：
- ⏰ 支持多种任务类型（Fire-and-forget、Delayed、Recurring、Continuations）
- 📊 内置 Dashboard 界面，便于监控和管理任务
- 💾 SQLite 存储，无需额外数据库服务
- 🔒 基于角色的权限控制
- 🛡️ 自动重试和错误处理

**适用场景**：
- 定期清理临时文件
- 数据库维护任务
- 报表生成
- 邮件发送
- 缓存预热

---

## 🚀 快速开始

### 访问 Dashboard

Hangfire Dashboard 已集成到 Gateway 和 Console 项目中：

- **Gateway 环境**：访问 `https://localhost:5000/hangfire`
- **Console 环境**：访问 `http://localhost:3002/hangfire`
- **直接访问 API**：`http://localhost:5100/hangfire`

**权限要求**：
- 本地访问（localhost）无限制
- 远程访问需要登录且角色为 `Admin` 或 `System`

### 查看系统任务

在 Dashboard 中可以看到以下系统任务：

1. **cleanup-deleted-files**：清理软删除文件（每天凌晨 3 点）
2. **cleanup-temp-files**：清理临时文件（每小时）
3. **cleanup-recycle-bin**：清理回收站文件（每天凌晨 3 点）
4. **cleanup-orphan-attachments**：清理孤立附件（每小时）

---

## ⚙️ 配置说明

### Hangfire 基础配置

在 `appsettings.json` 中配置 Hangfire：

```json
{
  "Hangfire": {
    // SQLite 数据库文件路径
    "ConnectionString": "Data Source=DataBases/Radish.Hangfire.db",

    // Dashboard 配置
    "Dashboard": {
      "Enable": true,              // 是否启用 Dashboard
      "RoutePrefix": "/hangfire",  // Dashboard 路由前缀
      "AllowLocalOnly": false      // 是否仅允许本地访问（未实现）
    }
  }
}
```

### 文件清理任务配置

```json
{
  "Hangfire": {
    "FileCleanup": {
      // 软删除文件清理
      "DeletedFiles": {
        "Enable": true,
        "Cron": "0 3 * * *",        // 每天凌晨 3 点（Cron 表达式）
        "RetentionDays": 30         // 保留 30 天
      },

      // 临时文件清理
      "TempFiles": {
        "Enable": true,
        "Cron": "0 * * * *",        // 每小时
        "RetentionHours": 2         // 保留 2 小时
      },

      // 回收站清理
      "RecycleBin": {
        "Enable": true,
        "Cron": "0 3 * * *",        // 每天凌晨 3 点
        "RetentionDays": 90         // 保留 90 天
      },

      // 孤立附件清理
      "OrphanAttachments": {
        "Enable": true,
        "Cron": "0 * * * *",        // 每小时
        "RetentionHours": 24        // 保留 24 小时
      }
    }
  }
}
```

---

## 🔧 任务实现

### 自定义任务类

所有定时任务都继承自 `FileCleanupJob` 类：

```csharp
/// <summary>
/// 文件清理定时任务
/// </summary>
public class FileCleanupJob
{
    private readonly ILogger<FileCleanupJob> _logger;
    private readonly IAttachmentService _attachmentService;
    private readonly IFileStorage _fileStorage;

    public FileCleanupJob(
        ILogger<FileCleanupJob> logger,
        IAttachmentService attachmentService,
        IFileStorage fileStorage)
    {
        _logger = logger;
        _attachmentService = attachmentService;
        _fileStorage = fileStorage;
    }

    /// <summary>
    /// 清理软删除的文件
    /// </summary>
    public async Task CleanupDeletedFilesAsync(int retentionDays = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        // 查询超过保留期的软删除文件
        var filesToDelete = await _attachmentService.QueryAsync(
            a => a.IsDeleted && a.DeleteTime < cutoffDate);

        foreach (var file in filesToDelete)
        {
            try
            {
                // 删除物理文件
                await _fileStorage.DeleteAsync(file.StoragePath);

                // 删除缩略图
                if (!string.IsNullOrEmpty(file.ThumbnailPath))
                {
                    await _fileStorage.DeleteAsync(file.ThumbnailPath);
                }

                // 记录日志
                _logger.LogInformation("已删除过期文件: {FileName}", file.StoragePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除文件失败: {FileName}", file.StoragePath);
            }
        }

        _logger.LogInformation("清理软删除文件完成，共处理 {Count} 个文件", filesToDelete.Count);
    }

    /// <summary>
    /// 清理临时文件
    /// </summary>
    public async Task CleanupTempFilesAsync(int retentionHours = 2)
    {
        // 实现逻辑...
    }

    /// <summary>
    /// 清理回收站
    /// </summary>
    public async Task CleanupRecycleBinAsync(int retentionDays = 90)
    {
        // 实现逻辑...
    }

    /// <summary>
    /// 清理孤立附件
    /// </summary>
    public async Task CleanupOrphanAttachmentsAsync(int retentionHours = 24)
    {
        // 实现逻辑...
    }
}
```

### 任务注册

在 `Program.cs` 中注册定时任务：

```csharp
// 读取配置
var fileCleanupConfig = builder.Configuration.GetSection("Hangfire:FileCleanup");

// 注册软删除文件清理任务
var deletedFilesConfig = fileCleanupConfig.GetSection("DeletedFiles");
if (deletedFilesConfig.GetValue<bool>("Enable"))
{
    var retentionDays = deletedFilesConfig.GetValue<int>("RetentionDays");
    var schedule = deletedFilesConfig.GetValue<string>("Cron");

    RecurringJob.AddOrUpdate<FileCleanupJob>(
        "cleanup-deleted-files",
        job => job.CleanupDeletedFilesAsync(retentionDays),
        schedule,
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
}

// 注册其他任务...
```

---

## 🔐 权限控制

### Dashboard 授权过滤器

```csharp
/// <summary>
/// Hangfire Dashboard 授权过滤器
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // 本地访问允许
        if (httpContext.Request.IsLocal())
        {
            return true;
        }

        // 远程访问需要验证身份和权限
        if (!httpContext.User.Identity?.IsAuthenticated ?? false)
        {
            return false;
        }

        // 仅 Admin 和 System 角色可访问
        return httpContext.User.IsInRole("Admin") || httpContext.User.IsInRole("System");
    }
}
```

### 配置 Dashboard 权限

```csharp
var dashboardEnabled = builder.Configuration.GetValue<bool>("Hangfire:Dashboard:Enable", true);
if (dashboardEnabled)
{
    var routePrefix = builder.Configuration["Hangfire:Dashboard:RoutePrefix"] ?? "/hangfire";

    app.UseHangfireDashboard(routePrefix, new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });
}
```

---

## 📊 任务监控

### Dashboard 功能

Hangfire Dashboard 提供以下功能：

1. **仪表板**：显示任务统计信息
2. **任务列表**：查看所有任务的状态
3. **重试任务**：手动重试失败的任务
4. **删除任务**：删除不需要的任务
5. **任务详情**：查看任务执行日志

### 任务状态说明

- **Enqueued**：等待执行
- **Processing**：正在执行
- **Succeeded**：执行成功
- **Failed**：执行失败（会自动重试）
- **Deleted**：已删除

### 日志记录

所有任务执行都会记录到日志系统：

```csharp
_logger.LogInformation("任务开始执行: {JobId}", BackgroundJob.Id);
_logger.LogInformation("任务执行完成: {JobId}, 处理文件数: {Count}", BackgroundJob.Id, count);
_logger.LogError(ex, "任务执行失败: {JobId}", BackgroundJob.Id);
```

---

## 🔄 Cron 表达式

### 常用表达式

| 表达式 | 说明 | 示例 |
|--------|------|------|
| `0 * * * *` | 每小时执行 | 整点清理临时文件 |
| `0 3 * * *` | 每天凌晨 3 点 | 清理过期文件 |
| `0 0 * * 0` | 每周日午夜 | 周报生成 |
| `0 0 1 * *` | 每月 1 日午夜 | 月度统计 |
| `*/5 * * * *` | 每 5 分钟 | 心跳检查 |

### 在线工具

推荐使用 [Cron-Generator](https://www.cron-generator.org/) 生成和测试 Cron 表达式。

---

## 🛠️ 最佳实践

### 1. 任务设计原则

- **幂等性**：任务可以安全地重复执行
- **异常处理**：捕获并记录所有异常
- **日志记录**：记录任务执行的关键信息
- **超时控制**：避免长时间运行的任务

### 2. 性能优化

```csharp
// 批量处理，避免逐条查询
var filesToDelete = await _attachmentService.QueryAsync(
    a => a.IsDeleted && a.DeleteTime < cutoffDate,
    pageSize: 1000);

// 并行处理（谨慎使用）
await Parallel.ForEachAsync(filesToDelete, async (file, token) =>
{
    await ProcessFileAsync(file);
});
```

### 3. 错误恢复

```csharp
public async Task CleanupDeletedFilesAsync(int retentionDays = 30)
{
    try
    {
        // 主要逻辑
        await ProcessFilesAsync(retentionDays);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "文件清理任务失败");

        // 可选：发送告警通知
        await SendAlertAsync("文件清理任务失败", ex.Message);

        // 重新抛出异常，让 Hangfire 记录失败
        throw;
    }
}
```

### 4. 配置管理

- 使用配置文件管理所有参数
- 提供合理的默认值
- 支持运行时修改（可选）

---

## 🔍 故障排查

### 常见问题

#### 1. Dashboard 403 错误

**原因**：权限不足或未登录

**解决方案**：
- 本地访问使用 `localhost` 或 `127.0.0.1`
- 远程访问确保已登录且角色为 `Admin` 或 `System`

#### 2. 任务不执行

**原因**：Cron 表达式错误或任务未注册

**解决方案**：
- 验证 Cron 表达式格式
- 检查 `Program.cs` 中的任务注册代码
- 查看 Hangfire 日志

#### 3. SQLite 数据库锁定

**原因**：多个实例同时访问 SQLite 文件

**解决方案**：
- 确保只有一个 Hangfire Server 实例
- 使用 SQL Server 或 PostgreSQL（生产环境）

### 调试技巧

1. **查看任务日志**：在 Dashboard 中点击任务查看详细日志
2. **手动触发任务**：使用 Dashboard 的手动触发功能
3. **查看 Hangfire 日志**：配置详细的日志级别

---

## 📚 参考资料

- [Hangfire 官方文档](https://docs.hangfire.io/en/latest/)
- [Cron 表达式教程](https://crontab.guru/)
- [ASP.NET Core 后台任务](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)

---

**文档状态**：已完成
**最后更新**：2025-12-22
**版本**：v1.0