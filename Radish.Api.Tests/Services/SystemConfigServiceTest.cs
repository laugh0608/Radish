using System;
using System.Collections.Generic;
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

        var service = new SystemConfigService(repository.Object);

        var configs = await service.GetSystemConfigsAsync();

        Assert.Single(configs);
        var faviconConfig = configs[0];
        Assert.Equal(SystemConfigDefaults.SiteFaviconKey, faviconConfig.VoKey);
        Assert.Equal(SystemConfigDefaults.DefaultSiteFaviconPath, faviconConfig.VoDefaultValue);
        Assert.Equal(SystemConfigDefaults.DefaultSiteFaviconPath, faviconConfig.VoEffectiveValue);
        Assert.False(faviconConfig.VoIsOverridden);
        Assert.Equal(SystemConfigRiskLevel.Low, faviconConfig.VoRiskLevel);
        Assert.Null(faviconConfig.VoModifyTime);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldCreateOverrideForRegisteredLowRiskSetting()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.SiteFaviconKey)!;
        var repository = new Mock<ISystemConfigRepository>();
        SystemConfigRecord? capturedRecord = null;

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

        var service = new SystemConfigService(repository.Object);

        var updatedConfig = await service.UpdateConfigAsync(definition.Id, new UpdateSystemConfigDto
        {
            Value = "/uploads/custom/site.ico",
            IsEnabled = true
        });

        Assert.NotNull(updatedConfig);
        Assert.True(updatedConfig!.VoIsOverridden);
        Assert.Equal("/uploads/custom/site.ico", updatedConfig.VoEffectiveValue);
        Assert.NotNull(capturedRecord);
        Assert.Equal(definition.Key, capturedRecord!.Key);
        Assert.Equal(definition.Category, capturedRecord.Category);
        Assert.Equal(definition.Description, capturedRecord.Description);
        Assert.True(capturedRecord.IsEnabled);
    }

    [Fact]
    public async Task UpdateConfigAsync_WithDefaultValue_ShouldRemoveOverride()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.SiteFaviconKey)!;
        var repository = new Mock<ISystemConfigRepository>();
        repository
            .Setup(item => item.DeleteByKeyAsync(definition.Key))
            .ReturnsAsync(true);

        var service = new SystemConfigService(repository.Object);

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
        var service = new SystemConfigService(Mock.Of<ISystemConfigRepository>());

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
}
