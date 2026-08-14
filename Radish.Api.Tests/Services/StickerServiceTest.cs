using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Common.AttributeTool;
using Radish.Common.CacheTool;
using Radish.Infrastructure.FileStorage;
using Radish.Infrastructure.ImageProcessing;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class StickerServiceTest
{
    [Theory]
    [InlineData(nameof(StickerService.DeleteGroupAsync))]
    [InlineData(nameof(StickerService.BatchUpdateSortAsync))]
    public void Multi_Row_Write_Should_Declare_Required_Transaction(string methodName)
    {
        var method = typeof(StickerService).GetMethod(methodName);

        var attribute = Assert.Single(method!.GetCustomAttributes<UseTranAttribute>());
        Assert.Equal(Propagation.Required, attribute.Propagation);
    }

    [Fact]
    public async Task BatchUpdateSortAsync_Should_Throw_When_Not_All_Rows_Are_Updated()
    {
        var groupRepository = new Mock<IBaseRepository<StickerGroup>>(MockBehavior.Loose);
        var stickerRepository = new Mock<IBaseRepository<Sticker>>(MockBehavior.Loose);
        groupRepository
            .Setup(repository => repository.QueryByIdAsync(12))
            .ReturnsAsync(new StickerGroup { Id = 12, TenantId = 7 });
        stickerRepository
            .Setup(repository => repository.QueryByIdsAsync(It.IsAny<List<long>>()))
            .ReturnsAsync(new List<Sticker>
            {
                new() { Id = 91, GroupId = 12 },
                new() { Id = 92, GroupId = 12 }
            });
        stickerRepository
            .Setup(repository => repository.UpdateRangeAsync(It.IsAny<List<Sticker>>()))
            .ReturnsAsync(1);
        var service = CreateService(groupRepository, stickerRepository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BatchUpdateSortAsync(
            new BatchUpdateStickerSortDto
            {
                GroupId = 12,
                Items =
                [
                    new StickerSortItemDto { Id = 91, Sort = 1 },
                    new StickerSortItemDto { Id = 92, Sort = 2 }
                ]
            },
            10001,
            "Admin"));

        Assert.Contains("未完整提交", error.Message);
    }

    [Fact]
    public async Task UpdateGroupStatusAsync_Should_Update_Only_Status_And_Audit_Columns()
    {
        var groupRepository = new Mock<IBaseRepository<StickerGroup>>(MockBehavior.Loose);
        var cache = new Mock<ICaching>(MockBehavior.Loose);
        Expression<Func<StickerGroup, StickerGroup>>? capturedUpdate = null;
        groupRepository
            .Setup(repository => repository.QueryByIdAsync(12))
            .ReturnsAsync(new StickerGroup { Id = 12, TenantId = 7, IsEnabled = true });
        groupRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<StickerGroup, StickerGroup>>>(),
                It.IsAny<Expression<Func<StickerGroup, bool>>>()))
            .Callback<Expression<Func<StickerGroup, StickerGroup>>, Expression<Func<StickerGroup, bool>>>(
                (update, _) => capturedUpdate = update)
            .ReturnsAsync(1);
        cache.Setup(item => item.RemoveAsync("sticker:groups:7")).Returns(Task.CompletedTask);
        var service = CreateService(groupRepository, caching: cache);

        var updated = await service.UpdateGroupStatusAsync(12, false, 10001, "Admin");

        Assert.True(updated);
        Assert.NotNull(capturedUpdate);
        var values = capturedUpdate!.Compile()(new StickerGroup());
        Assert.False(values.IsEnabled);
        Assert.Equal("Admin", values.ModifyBy);
        Assert.Equal(10001, values.ModifyId);
        groupRepository.Verify(repository => repository.UpdateAsync(It.IsAny<StickerGroup>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStickerAsync_Should_Reject_Sticker_Outside_Current_Tenant_Group_Scope()
    {
        var groupRepository = new Mock<IBaseRepository<StickerGroup>>(MockBehavior.Loose);
        var stickerRepository = new Mock<IBaseRepository<Sticker>>(MockBehavior.Loose);
        stickerRepository
            .Setup(repository => repository.QueryByIdAsync(91))
            .ReturnsAsync(new Sticker { Id = 91, GroupId = 44, Name = "outside" });
        groupRepository
            .Setup(repository => repository.QueryByIdAsync(44))
            .ReturnsAsync((StickerGroup?)null);
        var service = CreateService(groupRepository, stickerRepository);

        var updated = await service.UpdateStickerAsync(
            91,
            new UpdateStickerDto { Name = "changed" },
            10001,
            "Admin");

        Assert.False(updated);
        stickerRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Sticker>()), Times.Never);
    }

    [Fact]
    public async Task BatchUpdateSortAsync_Should_Reject_Cross_Group_Snapshot()
    {
        var groupRepository = new Mock<IBaseRepository<StickerGroup>>(MockBehavior.Loose);
        var stickerRepository = new Mock<IBaseRepository<Sticker>>(MockBehavior.Loose);
        groupRepository
            .Setup(repository => repository.QueryByIdAsync(12))
            .ReturnsAsync(new StickerGroup { Id = 12, TenantId = 7 });
        stickerRepository
            .Setup(repository => repository.QueryByIdsAsync(It.IsAny<List<long>>()))
            .ReturnsAsync(new List<Sticker> { new() { Id = 91, GroupId = 13 } });
        var service = CreateService(groupRepository, stickerRepository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BatchUpdateSortAsync(
            new BatchUpdateStickerSortDto
            {
                GroupId = 12,
                Items = [new StickerSortItemDto { Id = 91, Sort = 2 }]
            },
            10001,
            "Admin"));

        Assert.Contains("排序快照已失效", error.Message);
        stickerRepository.Verify(repository => repository.UpdateRangeAsync(It.IsAny<List<Sticker>>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGroupAsync_Should_Propagate_Child_Delete_Failure_For_Transaction_Rollback()
    {
        var groupRepository = new Mock<IBaseRepository<StickerGroup>>(MockBehavior.Loose);
        var stickerRepository = new Mock<IBaseRepository<Sticker>>(MockBehavior.Loose);
        groupRepository
            .Setup(repository => repository.QueryByIdAsync(12))
            .ReturnsAsync(new StickerGroup { Id = 12, TenantId = 7 });
        groupRepository
            .Setup(repository => repository.SoftDeleteByIdAsync(12, "Admin"))
            .ReturnsAsync(true);
        stickerRepository
            .Setup(repository => repository.SoftDeleteAsync(
                It.IsAny<Expression<Func<Sticker, bool>>>(),
                "Admin"))
            .ThrowsAsync(new InvalidOperationException("child write failed"));
        var service = CreateService(groupRepository, stickerRepository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteGroupAsync(12, 10001, "Admin"));

        Assert.Equal("child write failed", error.Message);
        groupRepository.Verify(repository => repository.UpdateColumnsAsync(
            It.IsAny<Expression<Func<StickerGroup, StickerGroup>>>(),
            It.IsAny<Expression<Func<StickerGroup, bool>>>()), Times.Never);
    }

    private static StickerService CreateService(
        Mock<IBaseRepository<StickerGroup>> groupRepository,
        Mock<IBaseRepository<Sticker>>? stickerRepository = null,
        Mock<ICaching>? caching = null)
    {
        return new StickerService(
            Mock.Of<IMapper>(),
            groupRepository.Object,
            (stickerRepository ?? new Mock<IBaseRepository<Sticker>>(MockBehavior.Loose)).Object,
            Mock.Of<IBaseRepository<Attachment>>(),
            (caching ?? new Mock<ICaching>(MockBehavior.Loose)).Object,
            Mock.Of<IFileStorage>(),
            Mock.Of<IImageProcessor>(),
            Mock.Of<ILogger<StickerService>>(),
            Mock.Of<IAttachmentUrlResolver>());
    }
}
