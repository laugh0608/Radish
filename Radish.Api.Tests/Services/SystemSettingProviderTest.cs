using System;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository;
using Radish.Model;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public class SystemSettingProviderTest
{
    [Fact]
    public async Task GetInt32Async_ShouldReturnCodeDefaultWhenOverrideMissing()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.PostTitleMinLengthKey)!;
        var repository = new Mock<ISystemConfigRepository>();
        repository
            .Setup(item => item.GetByKeyAsync(definition.Key))
            .ReturnsAsync((SystemConfigRecord?)null);
        var provider = new SystemSettingProvider(repository.Object);

        var value = await provider.GetInt32Async(definition.Key);

        Assert.Equal(int.Parse(SystemConfigDefaults.DefaultPostTitleMinLength), value);
    }

    [Fact]
    public async Task GetInt32Async_ShouldReturnEnabledOverride()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.PostBodyMinLengthKey)!;
        var repository = new Mock<ISystemConfigRepository>();
        repository
            .Setup(item => item.GetByKeyAsync(definition.Key))
            .ReturnsAsync(new SystemConfigRecord
            {
                Id = 20,
                Category = definition.Category,
                Key = definition.Key,
                Name = definition.Name,
                Value = "20",
                Type = definition.ValueType,
                IsEnabled = true,
                CreateTime = DateTime.Now.AddHours(-1),
                ModifyTime = DateTime.Now
            });
        var provider = new SystemSettingProvider(repository.Object);

        var value = await provider.GetInt32Async(definition.Key);

        Assert.Equal(20, value);
    }

    [Fact]
    public async Task GetInt32Async_ShouldExposeInvalidOverrideAsConfigurationError()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.CommentBodyMinLengthKey)!;
        var repository = new Mock<ISystemConfigRepository>();
        repository
            .Setup(item => item.GetByKeyAsync(definition.Key))
            .ReturnsAsync(new SystemConfigRecord
            {
                Id = 22,
                Category = definition.Category,
                Key = definition.Key,
                Name = definition.Name,
                Value = "1.5",
                Type = definition.ValueType,
                IsEnabled = true,
                CreateTime = DateTime.Now
            });
        var provider = new SystemSettingProvider(repository.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.GetInt32Async(definition.Key));

        Assert.Contains("必须是整数", exception.Message);
    }
}
