using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class UserPostBookmarkServiceTest
{
    private static readonly DateTime NowUtc =
        new(2026, 7, 29, 4, 0, 0, DateTimeKind.Utc);
    private const string PostPublicId = "pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f";
    private const string BookmarkPublicId = "bmk_018f6b6f7c7d70008f8f8f8f8f8f8f8f";

    [Fact]
    public async Task SetStateAsync_ShouldNormalizePublicIdAndReturnAuthoritativeState()
    {
        var fixture = new Fixture();
        var post = CreatePost();
        fixture.BookmarkRepository
            .Setup(item => item.QueryPostByPublicIdAsync(9, PostPublicId))
            .ReturnsAsync(post);
        fixture.BookmarkRepository
            .Setup(item => item.SetStateAsync(It.Is<PostBookmarkStateCommand>(command =>
                command.TenantId == 9 &&
                command.UserId == 1001 &&
                command.PostId == 7001 &&
                command.OperatorName == "alice" &&
                command.IsBookmarked &&
                command.NowUtc == NowUtc)))
            .ReturnsAsync(new PostBookmarkWriteResult(
                new UserPostBookmark
                {
                    Id = 8001,
                    PublicId = BookmarkPublicId,
                    TenantId = 9,
                    UserId = 1001,
                    PostId = 7001,
                    BookmarkedAt = NowUtc
                },
                post,
                true,
                4,
                true));

        var result = await fixture.Service.SetStateAsync(
            9,
            1001,
            "alice",
            PostPublicId.ToUpperInvariant(),
            true);

        Assert.Equal(BookmarkPublicId, result.VoBookmarkPublicId);
        Assert.Equal(PostPublicId, result.VoPostPublicId);
        Assert.True(result.VoIsBookmarked);
        Assert.Equal(4, result.VoCollectCount);
        Assert.Equal(NowUtc, result.VoBookmarkedAt);
    }

    [Fact]
    public async Task SetStateAsync_ShouldMapUnavailableUserToStableError()
    {
        var fixture = new Fixture();
        fixture.BookmarkRepository
            .Setup(item => item.QueryPostByPublicIdAsync(9, PostPublicId))
            .ReturnsAsync(CreatePost());
        fixture.BookmarkRepository
            .Setup(item => item.SetStateAsync(It.IsAny<PostBookmarkStateCommand>()))
            .ThrowsAsync(new PostBookmarkUserUnavailableException());

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Service.SetStateAsync(9, 1001, "alice", PostPublicId, true));

        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        Assert.Equal("PostBookmark.UserUnavailable", exception.ErrorCode);
        Assert.Equal("error.post_bookmark.user_unavailable", exception.MessageKey);
    }

    [Fact]
    public async Task GetMineAsync_ShouldBatchAvailableTargetsAndRedactUnavailableTargets()
    {
        var fixture = new Fixture();
        fixture.BookmarkRepository
            .Setup(item => item.QueryMineAsync(9, 1001, 1, 20))
            .ReturnsAsync((
                (IReadOnlyList<UserPostBookmark>)
                [
                    new UserPostBookmark
                    {
                        Id = 8002,
                        PublicId = "bmk_018f6b6f7c7d70008f8f8f8f8f8f8f80",
                        TenantId = 9,
                        UserId = 1001,
                        PostId = 7999,
                        BookmarkedAt = NowUtc
                    },
                    new UserPostBookmark
                    {
                        Id = 8001,
                        PublicId = BookmarkPublicId,
                        TenantId = 9,
                        UserId = 1001,
                        PostId = 7001,
                        BookmarkedAt = NowUtc.AddMinutes(-1)
                    }
                ],
                2));
        fixture.PostRepository
            .Setup(item => item.QueryAsync(It.IsAny<Expression<Func<Post, bool>>>()))
            .ReturnsAsync([CreatePost()]);
        fixture.UserRepository
            .Setup(item => item.QueryAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync([
                new User
                {
                    Id = 2001,
                    TenantId = 9,
                    PublicId = "usr_018f6b6f7c7d70008f8f8f8f8f8f8f80",
                    UserName = "author",
                    IsEnable = true
                }
            ]);
        fixture.CategoryRepository
            .Setup(item => item.QueryAsync(It.IsAny<Expression<Func<Category, bool>>>()))
            .ReturnsAsync([new Category("后端") { Id = 3001 }]);
        fixture.PostTagRepository
            .Setup(item => item.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>>()))
            .ReturnsAsync([new PostTag(7001, 4001) { Id = 5001 }]);
        fixture.TagRepository
            .Setup(item => item.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync([new Tag("收藏") { Id = 4001, Slug = "bookmark" }]);

        var result = await fixture.Service.GetMineAsync(9, 1001, 1, 20);

        Assert.Equal(2, result.VoTotal);
        var unavailable = result.VoItems[0];
        Assert.Equal(PostBookmarkTargetStatuses.Unavailable, unavailable.VoTargetStatus);
        Assert.Null(unavailable.VoPostPublicId);
        Assert.Null(unavailable.VoTitle);
        Assert.Null(unavailable.VoAuthorName);
        Assert.Empty(unavailable.VoTags);

        var available = result.VoItems[1];
        Assert.Equal(PostBookmarkTargetStatuses.Available, available.VoTargetStatus);
        Assert.Equal(PostPublicId, available.VoPostPublicId);
        Assert.Equal("收藏测试帖子", available.VoTitle);
        Assert.Equal("author", available.VoAuthorName);
        Assert.Equal("后端", available.VoCategoryName);
        Assert.Equal("bookmark", Assert.Single(available.VoTags).VoSlug);

        fixture.PostRepository.Verify(
            item => item.QueryAsync(It.IsAny<Expression<Func<Post, bool>>>()),
            Times.Once);
        fixture.UserRepository.Verify(
            item => item.QueryAsync(It.IsAny<Expression<Func<User, bool>>>()),
            Times.Once);
        fixture.CategoryRepository.Verify(
            item => item.QueryAsync(It.IsAny<Expression<Func<Category, bool>>>()),
            Times.Once);
        fixture.PostTagRepository.Verify(
            item => item.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>>()),
            Times.Once);
        fixture.TagRepository.Verify(
            item => item.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>>()),
            Times.Once);
    }

    private static Post CreatePost() =>
        new("收藏测试帖子", "用于验证收藏列表安全摘要的正文")
        {
            Id = 7001,
            PublicId = PostPublicId,
            TenantId = 9,
            AuthorId = 2001,
            AuthorName = "author",
            CategoryId = 3001,
            IsPublished = true,
            IsEnabled = true,
            CollectCount = 4,
            CreateTime = NowUtc.AddDays(-1)
        };

    private sealed class Fixture
    {
        public Mock<IUserPostBookmarkRepository> BookmarkRepository { get; } =
            new(MockBehavior.Strict);

        public Mock<IBaseRepository<Post>> PostRepository { get; } =
            new(MockBehavior.Strict);

        public Mock<IBaseRepository<User>> UserRepository { get; } =
            new(MockBehavior.Strict);

        public Mock<IBaseRepository<Category>> CategoryRepository { get; } =
            new(MockBehavior.Strict);

        public Mock<IBaseRepository<PostTag>> PostTagRepository { get; } =
            new(MockBehavior.Strict);

        public Mock<IBaseRepository<Tag>> TagRepository { get; } =
            new(MockBehavior.Strict);

        public UserPostBookmarkService Service { get; }

        public Fixture()
        {
            Service = new UserPostBookmarkService(
                BookmarkRepository.Object,
                PostRepository.Object,
                UserRepository.Object,
                CategoryRepository.Object,
                PostTagRepository.Object,
                TagRepository.Object,
                new FixedTimeProvider(new DateTimeOffset(NowUtc)));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }
}
