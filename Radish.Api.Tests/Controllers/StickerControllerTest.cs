using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(StickerController))]
public class StickerControllerTest
{
    [Fact]
    public async Task GetAdminGroups_Should_Return_Groups()
    {
        // Arrange
        var serviceMock = CreateStickerServiceMock();
        serviceMock
            .Setup(s => s.GetAdminGroupsAsync(0))
            .ReturnsAsync(new List<StickerGroupVo>
            {
                new()
                {
                    VoId = 1,
                    VoCode = "default",
                    VoName = "默认表情包",
                    VoIsEnabled = true
                }
            });

        var controller = CreateController(serviceMock.Object);

        // Act
        var result = await controller.GetAdminGroups();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<List<StickerGroupVo>>(result.ResponseData);
        Assert.Single(payload);
    }

    [Fact]
    public async Task BatchAddStickers_Should_Return_Conflict_When_Service_Returns_Conflicts()
    {
        // Arrange
        var serviceMock = CreateStickerServiceMock();
        serviceMock
            .Setup(s => s.BatchAddStickersAsync(It.IsAny<BatchAddStickersDto>(), 10001, "Admin"))
            .ReturnsAsync(new StickerBatchAddResultVo
            {
                VoGroupId = 1,
                VoConflicts =
                {
                    new StickerBatchConflictVo
                    {
                        VoRowIndex = 0,
                        VoCode = "happy",
                        VoMessage = "与已有表情重复"
                    }
                }
            });

        var controller = CreateController(serviceMock.Object);
        var request = CreateBatchAddRequest();

        // Act
        var result = await controller.BatchAddStickers(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("BatchCodeConflict", result.Code);
        var payload = Assert.IsType<StickerBatchAddResultVo>(result.ResponseData);
        Assert.Single(payload.VoConflicts);
    }

    [Fact]
    public async Task BatchAddStickers_Should_Return_InternalServerError_When_Service_Returns_FailedItems()
    {
        // Arrange
        var serviceMock = CreateStickerServiceMock();
        serviceMock
            .Setup(s => s.BatchAddStickersAsync(It.IsAny<BatchAddStickersDto>(), 10001, "Admin"))
            .ReturnsAsync(new StickerBatchAddResultVo
            {
                VoGroupId = 1,
                VoFailedItems =
                {
                    new StickerBatchFailedItemVo
                    {
                        VoRowIndex = 0,
                        VoAttachmentId = 1,
                        VoCode = "happy",
                        VoMessage = "缩略图生成失败"
                    }
                }
            });

        var controller = CreateController(serviceMock.Object);

        // Act
        var result = await controller.BatchAddStickers(CreateBatchAddRequest());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("ImageProcessFailed", result.Code);
        var payload = Assert.IsType<StickerBatchAddResultVo>(result.ResponseData);
        Assert.Single(payload.VoFailedItems);
    }

    [Fact]
    public async Task BatchAddStickers_Should_Return_NotFound_When_Group_Not_Exists()
    {
        // Arrange
        var serviceMock = CreateStickerServiceMock();
        serviceMock
            .Setup(s => s.BatchAddStickersAsync(It.IsAny<BatchAddStickersDto>(), 10001, "Admin"))
            .ThrowsAsync(new InvalidOperationException("分组不存在或已删除"));

        var controller = CreateController(serviceMock.Object);

        // Act
        var result = await controller.BatchAddStickers(CreateBatchAddRequest());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("StickerGroupNotFound", result.Code);
    }

    [Fact]
    public async Task BatchAddStickers_Should_Return_Success_When_Service_Succeeds()
    {
        // Arrange
        var serviceMock = CreateStickerServiceMock();
        serviceMock
            .Setup(s => s.BatchAddStickersAsync(It.IsAny<BatchAddStickersDto>(), 10001, "Admin"))
            .ReturnsAsync(new StickerBatchAddResultVo
            {
                VoGroupId = 1,
                VoCreatedCount = 2,
                VoStickerIds = new List<long> { 101, 102 }
            });

        var controller = CreateController(serviceMock.Object);

        // Act
        var result = await controller.BatchAddStickers(CreateBatchAddRequest());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<StickerBatchAddResultVo>(result.ResponseData);
        Assert.Equal(2, payload.VoCreatedCount);
        Assert.Equal(2, payload.VoStickerIds.Count);
    }

    [Fact]
    public async Task BatchUpdateSort_Should_Return_UpdatedCount()
    {
        // Arrange
        var serviceMock = CreateStickerServiceMock();
        serviceMock
            .Setup(s => s.BatchUpdateSortAsync(It.IsAny<List<StickerSortItemDto>>(), 10001, "Admin"))
            .ReturnsAsync(3);

        var controller = CreateController(serviceMock.Object);
        var request = new BatchUpdateStickerSortDto
        {
            Items = new List<StickerSortItemDto>
            {
                new() { Id = 1, Sort = 1 },
                new() { Id = 2, Sort = 2 },
                new() { Id = 3, Sort = 3 }
            }
        };

        // Act
        var result = await controller.BatchUpdateSort(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<StickerBatchUpdateSortResultVo>(result.ResponseData);
        Assert.Equal(3, payload.VoUpdatedCount);
    }

    [Fact]
    public async Task CheckStickerCode_Should_Return_NotFound_When_Group_Not_Exists()
    {
        // Arrange
        var serviceMock = CreateStickerServiceMock();
        serviceMock
            .Setup(s => s.CheckGroupExistsAsync(1))
            .ReturnsAsync(false);

        var controller = CreateController(serviceMock.Object);

        // Act
        var result = await controller.CheckStickerCode(1, "happy");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("StickerGroupNotFound", result.Code);
        Assert.Equal("分组不存在或已删除", result.MessageInfo);
    }

    [Fact]
    public async Task CheckStickerCode_Should_Return_Success_When_Available()
    {
        // Arrange
        var serviceMock = CreateStickerServiceMock();
        serviceMock
            .Setup(s => s.CheckGroupExistsAsync(1))
            .ReturnsAsync(true);
        serviceMock
            .Setup(s => s.CheckStickerCodeAvailableAsync(1, "happy"))
            .ReturnsAsync(true);

        var controller = CreateController(serviceMock.Object);

        // Act
        var result = await controller.CheckStickerCode(1, "happy");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<StickerCodeCheckVo>(result.ResponseData);
        Assert.True(payload.VoAvailable);
        Assert.Equal("happy", payload.VoCode);
        Assert.Equal(1, payload.VoGroupId);
    }

    [Fact]
    public async Task CheckStickerCode_Should_Return_Conflict_When_Not_Available()
    {
        // Arrange
        var serviceMock = CreateStickerServiceMock();
        serviceMock
            .Setup(s => s.CheckGroupExistsAsync(1))
            .ReturnsAsync(true);
        serviceMock
            .Setup(s => s.CheckStickerCodeAvailableAsync(1, "happy"))
            .ReturnsAsync(false);

        var controller = CreateController(serviceMock.Object);

        // Act
        var result = await controller.CheckStickerCode(1, "happy");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("CodeAlreadyExists", result.Code);
    }

    private static StickerController CreateController(IStickerService stickerService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(x => x.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Admin",
            TenantId = 0
        });

        return new StickerController(stickerService, currentUserAccessorMock.Object);
    }

    private static Mock<IStickerService> CreateStickerServiceMock()
    {
        return new Mock<IStickerService>(MockBehavior.Strict);
    }

    private static BatchAddStickersDto CreateBatchAddRequest()
    {
        return new BatchAddStickersDto
        {
            GroupId = 1,
            Stickers = new List<BatchAddStickerItemDto>
            {
                new()
                {
                    AttachmentId = 1,
                    Code = "happy",
                    Name = "开心",
                    AllowInline = true
                }
            }
        };
    }
}
