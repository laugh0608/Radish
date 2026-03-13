using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.IService.Base;
using Radish.Model;
using Radish.Model.ViewModels;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(PostController))]
public class PostControllerTest
{
    [Fact]
    public async Task GetList_Should_Fill_Metadata_In_Batch_Without_Querying_Post_Detail_Per_Item()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        var posts = new List<PostVo>
        {
            new()
            {
                VoId = 9527,
                VoTitle = "论坛投票帖子",
                VoAuthorId = 0,
                VoCreateTime = DateTime.UtcNow
            }
        };

        postServiceMock
            .Setup(service => service.QueryPageAsync(
                It.IsAny<Expression<Func<Post, bool>>>(),
                1,
                20,
                It.IsAny<Expression<Func<Post, object>>>(),
                OrderByType.Desc))
            .ReturnsAsync((posts, 1));
        postServiceMock
            .Setup(service => service.FillPostListMetadataAsync(It.Is<List<PostVo>>(items => ReferenceEquals(items, posts))))
            .Callback<List<PostVo>>(items =>
            {
                items[0].VoCategoryName = "综合讨论";
                items[0].VoTags = "投票, 社区";
                items[0].VoHasPoll = true;
                items[0].VoPollTotalVoteCount = 18;
                items[0].VoPollIsClosed = false;
            })
            .Returns(Task.CompletedTask);
        commentServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<CommentVo>());

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetList();

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var pageModel = Assert.IsType<PageModel<PostVo>>(result.ResponseData);
        var post = Assert.Single(pageModel.Data);
        Assert.Equal("综合讨论", post.VoCategoryName);
        Assert.Equal("投票, 社区", post.VoTags);
        Assert.True(post.VoHasPoll);
        Assert.Equal(18, post.VoPollTotalVoteCount);
        Assert.False(post.VoPollIsClosed);

        postServiceMock.Verify(service => service.FillPostListMetadataAsync(It.IsAny<List<PostVo>>()), Times.Once);
        postServiceMock.Verify(service => service.GetPostDetailAsync(It.IsAny<long>(), It.IsAny<long?>()), Times.Never);
        attachmentServiceMock.Verify(service => service.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>()), Times.Never);
    }

    private static PostController CreateController(
        IPostService postService,
        IContentModerationService moderationService,
        IBaseService<Attachment, AttachmentVo> attachmentService,
        IBaseService<Comment, CommentVo> commentService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new PostController(
            postService,
            moderationService,
            attachmentService,
            commentService,
            currentUserAccessorMock.Object);
    }
}
