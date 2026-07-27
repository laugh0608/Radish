using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Radish.Shared.Constants;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ForumContentRevisionServiceTest
{
    [Fact]
    public async Task AppendCommentRevisionAsync_ShouldPersistCurrentImmutableSnapshot()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        var revisionRepository = new Mock<IBaseRepository<CommentContentRevision>>(MockBehavior.Strict);
        var comment = new Comment("当前评论")
        {
            Id = 101,
            PostId = 201,
            AuthorId = 301,
            AuthorName = "Author",
            TenantId = 9,
            ContentRevision = 2,
            EditCount = 1,
            IsDeleted = false
        };
        CommentContentRevision? inserted = null;

        commentRepository.Setup(repository => repository.QueryByIdAsync(comment.Id)).ReturnsAsync(comment);
        revisionRepository
            .Setup(repository => repository.QueryFirstAsync(
                It.IsAny<Expression<Func<CommentContentRevision, bool>>?>()))
            .ReturnsAsync((CommentContentRevision?)null);
        revisionRepository
            .Setup(repository => repository.AddAsync(It.IsAny<CommentContentRevision>()))
            .Callback<CommentContentRevision>(revision => inserted = revision)
            .ReturnsAsync(901);

        var service = CreateService(commentRepository: commentRepository, commentRevisionRepository: revisionRepository);

        var result = await service.AppendCommentRevisionAsync(
            comment.Id,
            ForumContentRevisionSourceTypes.Edit,
            null,
            comment.AuthorId,
            comment.AuthorName);

        result.VoTargetId.ShouldBe(comment.Id);
        result.VoRevisionId.ShouldBe(901);
        result.VoContentRevision.ShouldBe(2);
        inserted.ShouldNotBeNull();
        inserted!.CommentId.ShouldBe(comment.Id);
        inserted.Content.ShouldBe("当前评论");
        inserted.RevisionNumber.ShouldBe(2);
        inserted.SourceType.ShouldBe(ForumContentRevisionSourceTypes.Edit);
        inserted.IntegrityStatus.ShouldBe(ForumContentRevisionIntegrityStatuses.Complete);
        commentRepository.VerifyAll();
        revisionRepository.VerifyAll();
    }

    [Fact]
    public async Task GetPostRevisionListAsync_ShouldUseCurrentRevisionUtcTimeInPublicSummary()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var revisionRepository = new Mock<IBaseRepository<PostContentRevision>>(MockBehavior.Strict);
        var post = new Post("已编辑帖子", "当前正文")
        {
            Id = 104,
            AuthorId = 304,
            TenantId = 9,
            EditCount = 2,
            ContentRevision = 3,
            ModifyTime = new DateTime(2026, 7, 26, 13, 0, 0),
            IsDeleted = false
        };
        var currentRevisionTime = new DateTime(2026, 7, 26, 5, 0, 0, DateTimeKind.Utc);
        postRepository.Setup(repository => repository.QueryByIdAsync(post.Id)).ReturnsAsync(post);
        revisionRepository
            .Setup(repository => repository.QueryFirstAsync(
                It.IsAny<Expression<Func<PostContentRevision, bool>>?>()))
            .ReturnsAsync(new PostContentRevision
            {
                Id = 903,
                PostId = post.Id,
                RevisionNumber = post.ContentRevision,
                CreateTime = currentRevisionTime
            });

        var service = CreateService(
            postRepository: postRepository,
            postRevisionRepository: revisionRepository);

        var result = await service.GetPostRevisionListAsync(
            post.Id,
            viewerId: 999,
            isAdmin: false,
            pageIndex: 1,
            pageSize: 20);

        result.VoIsEdited.ShouldBeTrue();
        result.VoEditCount.ShouldBe(2);
        result.VoCurrentContentRevision.ShouldBe(3);
        result.VoLastEditedAt.ShouldBe(currentRevisionTime);
        result.VoCanViewDetails.ShouldBeFalse();
        result.VoItems.ShouldBeEmpty();
        revisionRepository.Verify(
            repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<PostContentRevision, bool>>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<PostContentRevision, object>>?>(),
                It.IsAny<SqlSugar.OrderByType>()),
            Times.Never);
        postRepository.VerifyAll();
        revisionRepository.VerifyAll();
    }

    [Fact]
    public async Task GetCommentRevisionListAsync_ShouldExposeOnlyPublicSummaryToOtherUsers()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        var revisionRepository = new Mock<IBaseRepository<CommentContentRevision>>(MockBehavior.Strict);
        var comment = new Comment("已编辑评论")
        {
            Id = 102,
            PostId = 202,
            AuthorId = 302,
            TenantId = 9,
            EditCount = 3,
            ContentRevision = 4,
            ModifyTime = new DateTime(2026, 7, 26, 12, 0, 0),
            IsDeleted = false
        };
        var currentRevisionTime = new DateTime(2026, 7, 26, 4, 0, 0, DateTimeKind.Utc);
        commentRepository.Setup(repository => repository.QueryByIdAsync(comment.Id)).ReturnsAsync(comment);
        revisionRepository
            .Setup(repository => repository.QueryFirstAsync(
                It.IsAny<Expression<Func<CommentContentRevision, bool>>?>()))
            .ReturnsAsync(new CommentContentRevision
            {
                Id = 902,
                CommentId = comment.Id,
                RevisionNumber = comment.ContentRevision,
                CreateTime = currentRevisionTime
            });

        var service = CreateService(commentRepository: commentRepository, commentRevisionRepository: revisionRepository);

        var result = await service.GetCommentRevisionListAsync(
            comment.Id,
            viewerId: 999,
            isAdmin: false,
            pageIndex: 1,
            pageSize: 20);

        result.VoIsEdited.ShouldBeTrue();
        result.VoEditCount.ShouldBe(3);
        result.VoCurrentContentRevision.ShouldBe(4);
        result.VoLastEditedAt.ShouldBe(currentRevisionTime);
        result.VoCanViewDetails.ShouldBeFalse();
        result.VoItems.ShouldBeEmpty();
        revisionRepository.Verify(
            repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<CommentContentRevision, bool>>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<CommentContentRevision, object>>?>(),
                It.IsAny<SqlSugar.OrderByType>()),
            Times.Never);
        commentRepository.VerifyAll();
        revisionRepository.VerifyAll();
    }

    [Fact]
    public async Task RestoreCommentAsync_ShouldRejectStaleExpectedRevisionBeforeMutation()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        var revisionRepository = new Mock<IBaseRepository<CommentContentRevision>>(MockBehavior.Strict);
        var comment = new Comment("当前评论")
        {
            Id = 103,
            PostId = 203,
            AuthorId = 303,
            TenantId = 9,
            ContentRevision = 5,
            IsDeleted = false
        };
        commentRepository.Setup(repository => repository.QueryByIdAsync(comment.Id)).ReturnsAsync(comment);
        var service = CreateService(commentRepository: commentRepository, commentRevisionRepository: revisionRepository);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            service.RestoreCommentAsync(
                comment.Id,
                revisionId: 900,
                expectedContentRevision: 4,
                operatorId: comment.AuthorId,
                operatorName: "Author",
                isAdmin: false));

        exception.StatusCode.ShouldBe(409);
        exception.ErrorCode.ShouldBe(ForumContentRevisionErrorCodes.Conflict);
        revisionRepository.Verify(
            repository => repository.QueryByIdAsync(It.IsAny<long>()),
            Times.Never);
        commentRepository.VerifyAll();
    }

    private static ForumContentRevisionService CreateService(
        Mock<IBaseRepository<Comment>>? commentRepository = null,
        Mock<IBaseRepository<CommentContentRevision>>? commentRevisionRepository = null,
        Mock<IBaseRepository<Post>>? postRepository = null,
        Mock<IBaseRepository<PostContentRevision>>? postRevisionRepository = null)
    {
        return new ForumContentRevisionService(
            postRepository?.Object ?? Mock.Of<IBaseRepository<Post>>(),
            commentRepository?.Object ?? Mock.Of<IBaseRepository<Comment>>(),
            postRevisionRepository?.Object ?? Mock.Of<IBaseRepository<PostContentRevision>>(),
            Mock.Of<IBaseRepository<PostContentRevisionTag>>(),
            commentRevisionRepository?.Object ?? Mock.Of<IBaseRepository<CommentContentRevision>>(),
            Mock.Of<IBaseRepository<ForumContentRevisionAttachment>>(),
            Mock.Of<IBaseRepository<PostTag>>(),
            Mock.Of<IBaseRepository<Tag>>(),
            Mock.Of<IBaseRepository<Category>>(),
            Mock.Of<IBaseRepository<Attachment>>(),
            Mock.Of<IPostService>(),
            Mock.Of<ICommentService>());
    }
}
