using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public class SystemConfigServiceTest
{
    [Fact]
    public async Task GetSystemConfigsAsync_ShouldReturnRegisteredDefinitionsOnly()
    {
        var repository = new Mock<ISystemConfigRepository>();
        repository
            .Setup(item => item.GetAllAsync())
            .ReturnsAsync(new List<SystemConfigRecord>
            {
                new()
                {
                    Id = 3,
                    Category = "萝卜币配置",
                    Key = "Coin.DailyRewardLimit",
                    Name = "每日奖励上限",
                    Value = "100",
                    Type = "number",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-1),
                    ModifyTime = DateTime.Now
                },
                new()
                {
                    Id = 11,
                    Category = SystemConfigDefaults.SiteBrandingCategory,
                    Key = SystemConfigDefaults.SiteFaviconKey,
                    Name = SystemConfigDefaults.SiteFaviconName,
                    Value = SystemConfigDefaults.DefaultSiteFaviconPath,
                    Type = "string",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-1),
                    ModifyTime = DateTime.Now
                }
            });

        var service = CreateService(repository);

        var configs = await service.GetSystemConfigsAsync();

        Assert.Equal(SystemConfigDefaults.Definitions.Count, configs.Count);
        var faviconConfig = configs[0];
        Assert.Equal(SystemConfigDefaults.SiteFaviconKey, faviconConfig.VoKey);
        Assert.Equal(SystemConfigDefaults.DefaultSiteFaviconPath, faviconConfig.VoDefaultValue);
        Assert.Equal(SystemConfigDefaults.DefaultSiteFaviconPath, faviconConfig.VoEffectiveValue);
        Assert.False(faviconConfig.VoIsOverridden);
        Assert.Equal(SystemConfigRiskLevel.Low, faviconConfig.VoRiskLevel);
        Assert.Null(faviconConfig.VoMinNumberValue);
        Assert.Null(faviconConfig.VoMaxNumberValue);
        Assert.False(faviconConfig.VoRequiresInteger);
        Assert.Contains("标签页图标", faviconConfig.VoImpactSummary);
        Assert.Null(faviconConfig.VoModifyTime);

        var postTitleConfig = configs.Single(config => config.VoKey == SystemConfigDefaults.PostTitleMinLengthKey);
        Assert.Equal(1m, postTitleConfig.VoMinNumberValue);
        Assert.Equal(200m, postTitleConfig.VoMaxNumberValue);
        Assert.True(postTitleConfig.VoRequiresInteger);
        Assert.Contains("标题长度校验", postTitleConfig.VoImpactSummary);

        var postTitleMaxConfig = configs.Single(config => config.VoKey == SystemConfigDefaults.PostTitleMaxLengthKey);
        Assert.Equal(SystemConfigDefaults.DefaultPostTitleMaxLength, postTitleMaxConfig.VoDefaultValue);
        Assert.Equal(SystemConfigRiskLevel.Medium, postTitleMaxConfig.VoRiskLevel);
        Assert.Equal(1m, postTitleMaxConfig.VoMinNumberValue);
        Assert.Equal(200m, postTitleMaxConfig.VoMaxNumberValue);

        var postSummaryMaxConfig = configs.Single(config => config.VoKey == SystemConfigDefaults.PostSummaryMaxLengthKey);
        Assert.Equal(SystemConfigDefaults.DefaultPostSummaryMaxLength, postSummaryMaxConfig.VoDefaultValue);
        Assert.Equal(SystemConfigRiskLevel.Low, postSummaryMaxConfig.VoRiskLevel);
        Assert.Equal(20m, postSummaryMaxConfig.VoMinNumberValue);
        Assert.Equal(500m, postSummaryMaxConfig.VoMaxNumberValue);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldRejectMediumSettingWithoutReason()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.PostTitleMinLengthKey)!;
        var service = CreateService(new Mock<ISystemConfigRepository>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.UpdateConfigAsync(definition.Id, new UpdateSystemConfigDto
            {
                Value = "4",
                IsEnabled = true,
                Reason = " ",
                ConfirmRiskLevel = definition.RiskLevel,
                ConfirmKey = definition.Key
            }));

        Assert.Contains("需要填写修改原因", exception.Message);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldCreateOverrideForRegisteredMediumRiskSetting()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.PostTitleMinLengthKey)!;
        var repository = new Mock<ISystemConfigRepository>();
        var logRepository = new Mock<ISystemConfigChangeLogRepository>();
        SystemConfigRecord? capturedRecord = null;
        SystemConfigChangeLogRecord? capturedLog = null;

        repository
            .Setup(item => item.GetByKeyAsync(definition.Key))
            .ReturnsAsync((SystemConfigRecord?)null);
        repository
            .Setup(item => item.CreateAsync(It.IsAny<SystemConfigRecord>()))
            .Callback<SystemConfigRecord>(record => capturedRecord = record)
            .ReturnsAsync((SystemConfigRecord record) =>
            {
                record.Id = 21;
                return record;
            });
        logRepository
            .Setup(item => item.CreateAsync(It.IsAny<SystemConfigChangeLogRecord>()))
            .Callback<SystemConfigChangeLogRecord>(record => capturedLog = record)
            .ReturnsAsync((SystemConfigChangeLogRecord record) =>
            {
                record.Id = 3;
                return record;
            });

        var service = CreateService(repository, logRepository);

        var updatedConfig = await service.UpdateConfigAsync(definition.Id, new UpdateSystemConfigDto
        {
            Value = "4",
            IsEnabled = true,
            Reason = "提高标题有效信息量",
            ConfirmRiskLevel = definition.RiskLevel,
            ConfirmKey = definition.Key
        });

        Assert.NotNull(updatedConfig);
        Assert.True(updatedConfig!.VoIsOverridden);
        Assert.Equal("4", updatedConfig.VoEffectiveValue);
        Assert.NotNull(capturedRecord);
        Assert.Equal(definition.Key, capturedRecord!.Key);
        Assert.Equal("4", capturedRecord.Value);
        Assert.NotNull(capturedLog);
        Assert.Equal(SystemConfigRiskLevel.Medium, capturedLog!.RiskLevel);
        Assert.Equal(definition.RiskLevel, capturedLog.ConfirmRiskLevel);
        Assert.Equal(definition.Key, capturedLog.ConfirmKey);
        Assert.Equal("提高标题有效信息量", capturedLog.Reason);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldRejectNumberOutOfDefinitionRange()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.PostTitleMinLengthKey)!;
        var repository = new Mock<ISystemConfigRepository>();
        repository
            .Setup(item => item.GetByKeyAsync(definition.Key))
            .ReturnsAsync((SystemConfigRecord?)null);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.UpdateConfigAsync(definition.Id, new UpdateSystemConfigDto
            {
                Value = "201",
                IsEnabled = true,
                Reason = "验证范围",
                ConfirmRiskLevel = definition.RiskLevel,
                ConfirmKey = definition.Key
            }));

        Assert.Contains("不能大于 200", exception.Message);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldCreateOverrideForRegisteredLowRiskSetting()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.SiteFaviconKey)!;
        var repository = new Mock<ISystemConfigRepository>();
        var logRepository = new Mock<ISystemConfigChangeLogRepository>();
        SystemConfigRecord? capturedRecord = null;
        SystemConfigChangeLogRecord? capturedLog = null;

        repository
            .Setup(item => item.GetByKeyAsync(definition.Key))
            .ReturnsAsync((SystemConfigRecord?)null);
        repository
            .Setup(item => item.CreateAsync(It.IsAny<SystemConfigRecord>()))
            .Callback<SystemConfigRecord>(record => capturedRecord = record)
            .ReturnsAsync((SystemConfigRecord record) =>
            {
                record.Id = 12;
                return record;
            });
        logRepository
            .Setup(item => item.CreateAsync(It.IsAny<SystemConfigChangeLogRecord>()))
            .Callback<SystemConfigChangeLogRecord>(record => capturedLog = record)
            .ReturnsAsync((SystemConfigChangeLogRecord record) =>
            {
                record.Id = 1;
                return record;
            });

        var service = CreateService(repository, logRepository);

        var updatedConfig = await service.UpdateConfigAsync(definition.Id, new UpdateSystemConfigDto
        {
            Value = "/uploads/custom/site.ico",
            IsEnabled = true,
            Reason = "更新站点图标",
            ConfirmRiskLevel = definition.RiskLevel,
            ConfirmKey = definition.Key
        }, new SystemConfigChangeContext
        {
            OperatorUserId = 1001,
            OperatorUserName = "admin",
            RequestIp = "127.0.0.1",
            UserAgent = "unit-test"
        });

        Assert.NotNull(updatedConfig);
        Assert.True(updatedConfig!.VoIsOverridden);
        Assert.Equal("/uploads/custom/site.ico", updatedConfig.VoEffectiveValue);
        Assert.NotNull(capturedRecord);
        Assert.Equal(definition.Key, capturedRecord!.Key);
        Assert.Equal(definition.Category, capturedRecord.Category);
        Assert.Equal(definition.Description, capturedRecord.Description);
        Assert.True(capturedRecord.IsEnabled);
        Assert.NotNull(capturedLog);
        Assert.Equal(SystemConfigChangeAction.UpdateOverride, capturedLog!.ActionType);
        Assert.Equal(definition.DefaultValue, capturedLog.OldValue);
        Assert.Equal("/uploads/custom/site.ico", capturedLog.NewValue);
        Assert.Equal("更新站点图标", capturedLog.Reason);
        Assert.Equal(1001, capturedLog.OperatorUserId);
        Assert.Equal("admin", capturedLog.OperatorUserName);
    }

    [Fact]
    public async Task UpdateConfigAsync_WithDefaultValue_ShouldRemoveOverride()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.SiteFaviconKey)!;
        var repository = new Mock<ISystemConfigRepository>();
        repository
            .Setup(item => item.DeleteByKeyAsync(definition.Key))
            .ReturnsAsync(true);

        var service = CreateService(repository);

        var updatedConfig = await service.UpdateConfigAsync(definition.Id, new UpdateSystemConfigDto
        {
            Value = definition.DefaultValue,
            IsEnabled = true
        });

        Assert.NotNull(updatedConfig);
        Assert.False(updatedConfig!.VoIsOverridden);
        Assert.Equal(definition.DefaultValue, updatedConfig.VoEffectiveValue);
        repository.Verify(item => item.DeleteByKeyAsync(definition.Key), Times.Once);
        repository.Verify(item => item.CreateAsync(It.IsAny<SystemConfigRecord>()), Times.Never);
    }

    [Fact]
    public async Task CreateConfigAsync_ShouldRejectConsoleCreatedUnknownSettings()
    {
        var service = CreateService(new Mock<ISystemConfigRepository>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CreateConfigAsync(new CreateSystemConfigDto
            {
                Category = "测试",
                Key = "Test.Unknown",
                Name = "未知设置",
                Value = "1"
            }));

        Assert.Contains("不支持通过 Console 新增未知配置", exception.Message);
    }

    [Fact]
    public async Task RestoreConfigDefaultAsync_ShouldCreateChangeLogForOverride()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.SiteFaviconKey)!;
        var repository = new Mock<ISystemConfigRepository>();
        var logRepository = new Mock<ISystemConfigChangeLogRepository>();
        SystemConfigChangeLogRecord? capturedLog = null;

        repository
            .Setup(item => item.GetByKeyAsync(definition.Key))
            .ReturnsAsync(new SystemConfigRecord
            {
                Id = 12,
                Category = definition.Category,
                Key = definition.Key,
                Name = definition.Name,
                Value = "/uploads/custom/site.ico",
                Type = definition.ValueType,
                IsEnabled = true,
                CreateTime = DateTime.Now.AddHours(-2),
                ModifyTime = DateTime.Now.AddHours(-1)
            });
        repository
            .Setup(item => item.DeleteByKeyAsync(definition.Key))
            .ReturnsAsync(true);
        logRepository
            .Setup(item => item.CreateAsync(It.IsAny<SystemConfigChangeLogRecord>()))
            .Callback<SystemConfigChangeLogRecord>(record => capturedLog = record)
            .ReturnsAsync((SystemConfigChangeLogRecord record) =>
            {
                record.Id = 2;
                return record;
            });

        var service = CreateService(repository, logRepository);

        var restoredConfig = await service.RestoreConfigDefaultAsync(
            definition.Id,
            new RestoreSystemConfigDefaultDto
            {
                Reason = "回滚到默认图标",
                ConfirmRiskLevel = definition.RiskLevel,
                ConfirmKey = definition.Key
            },
            new SystemConfigChangeContext
            {
                OperatorUserId = 1002,
                OperatorUserName = "owner",
                RequestIp = "10.0.0.2",
                UserAgent = "unit-test"
            });

        Assert.NotNull(restoredConfig);
        Assert.False(restoredConfig!.VoIsOverridden);
        Assert.Equal(definition.DefaultValue, restoredConfig.VoEffectiveValue);
        Assert.NotNull(capturedLog);
        Assert.Equal(SystemConfigChangeAction.RestoreDefault, capturedLog!.ActionType);
        Assert.Equal("/uploads/custom/site.ico", capturedLog.OldValue);
        Assert.Equal(definition.DefaultValue, capturedLog.NewValue);
        Assert.Equal("回滚到默认图标", capturedLog.Reason);
        Assert.Equal("10.0.0.2", capturedLog.RequestIp);
    }

    [Fact]
    public async Task GetConfigChangeLogsAsync_ShouldReturnRegisteredSettingLogs()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.SiteFaviconKey)!;
        var repository = new Mock<ISystemConfigRepository>();
        var logRepository = new Mock<ISystemConfigChangeLogRepository>();

        logRepository
            .Setup(item => item.GetByKeyAsync(definition.Key, 20))
            .ReturnsAsync(new List<SystemConfigChangeLogRecord>
            {
                new()
                {
                    Id = 7,
                    Category = definition.Category,
                    Key = definition.Key,
                    Name = definition.Name,
                    ActionType = SystemConfigChangeAction.UpdateOverride,
                    OldValue = definition.DefaultValue,
                    NewValue = "/uploads/custom/site.ico",
                    DefaultValue = definition.DefaultValue,
                    Reason = "更新站点图标",
                    RiskLevel = definition.RiskLevel,
                    EffectiveMode = definition.EffectiveMode,
                    OperatorUserId = 1001,
                    OperatorUserName = "admin",
                    CreateTime = DateTime.Now
                }
            });

        var service = CreateService(repository, logRepository);

        var logs = await service.GetConfigChangeLogsAsync(definition.Id);

        Assert.NotNull(logs);
        Assert.Single(logs!);
        Assert.Equal(7, logs![0].VoId);
        Assert.Equal(definition.Key, logs[0].VoKey);
        Assert.Equal(SystemConfigChangeAction.UpdateOverride, logs[0].VoActionType);
        Assert.Equal("admin", logs[0].VoOperatorUserName);
    }

    private static SystemConfigService CreateService(
        Mock<ISystemConfigRepository> repository,
        Mock<ISystemConfigChangeLogRepository>? logRepository = null)
    {
        return new SystemConfigService(
            repository.Object,
            (logRepository ?? new Mock<ISystemConfigChangeLogRepository>()).Object);
    }
}
