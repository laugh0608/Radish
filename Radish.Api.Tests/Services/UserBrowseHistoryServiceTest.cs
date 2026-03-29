using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public class UserBrowseHistoryServiceTest
{
    [Fact]
    public async Task RecordAsync_Should_InsertNewHistory_WithNormalizedSnapshot()
    {
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var repository = new Mock<IBaseRepository<UserBrowseHistory>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);

        repository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<UserBrowseHistory, bool>>?>()))
            .ReturnsAsync((UserBrowseHistory?)null);
        repository
            .Setup(repo => repo.AddAsync(It.Is<UserBrowseHistory>(history =>
                history.UserId == 1001 &&
                history.TargetType == "Post" &&
                history.TargetId == 9527 &&
                history.TargetSlug != null &&
                history.TargetSlug.Length == 200 &&
                history.Title.Length == 200 &&
                history.Summary != null &&
                history.Summary.Length == 500 &&
                history.CoverAttachmentId == 987654321 &&
                history.RoutePath != null &&
                history.RoutePath.Length == 500 &&
                history.ViewCount == 1 &&
                history.CreateBy == "Tester")))
            .ReturnsAsync(1);

        var service = new UserBrowseHistoryService(mapper.Object, repository.Object, attachmentUrlResolver.Object);

        await service.RecordAsync(new RecordBrowseHistoryDto
        {
            UserId = 1001,
            TenantId = 9,
            TargetType = "Post",
            TargetId = 9527,
            TargetSlug = new string('s', 220),
            Title = new string('t', 220),
            Summary = new string('m', 520),
            CoverAttachmentId = 987654321,
            RoutePath = new string('r', 520),
            OperatorName = "Tester"
        });

        repository.Verify(repo => repo.AddAsync(It.IsAny<UserBrowseHistory>()), Times.Once);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<UserBrowseHistory>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_Should_UpdateExistingHistory_AndIncrementViewCount()
    {
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var repository = new Mock<IBaseRepository<UserBrowseHistory>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);

        var existing = new UserBrowseHistory
        {
            Id = 1,
            UserId = 1001,
            TargetType = "Wiki",
            TargetId = 2048,
            TargetSlug = "wiki-old",
            Title = "旧标题",
            Summary = "旧摘要",
            CoverAttachmentId = 123,
            RoutePath = "/wiki/doc/old",
            ViewCount = 2,
            LastViewTime = DateTime.UtcNow.AddDays(-1)
        };

        repository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<UserBrowseHistory, bool>>?>()))
            .ReturnsAsync(existing);
        repository
            .Setup(repo => repo.UpdateAsync(It.Is<UserBrowseHistory>(history =>
                history.Id == 1 &&
                history.Title == "新标题" &&
                history.TargetSlug == "wiki-new" &&
                history.CoverAttachmentId == 456 &&
                history.RoutePath == "/wiki/doc/wiki-new" &&
                history.ViewCount == 3 &&
                history.ModifyBy == "Tester" &&
                history.ModifyId == 1001)))
            .ReturnsAsync(true);

        var service = new UserBrowseHistoryService(mapper.Object, repository.Object, attachmentUrlResolver.Object);

        await service.RecordAsync(new RecordBrowseHistoryDto
        {
            UserId = 1001,
            TenantId = 9,
            TargetType = "Wiki",
            TargetId = 2048,
            TargetSlug = "wiki-new",
            Title = "新标题",
            Summary = "新摘要",
            CoverAttachmentId = 456,
            RoutePath = "/wiki/doc/wiki-new",
            OperatorName = "Tester"
        });

        repository.Verify(repo => repo.AddAsync(It.IsAny<UserBrowseHistory>()), Times.Never);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<UserBrowseHistory>()), Times.Once);
    }
}
