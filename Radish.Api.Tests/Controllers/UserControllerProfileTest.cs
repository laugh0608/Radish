using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public class UserControllerProfileTest
{
    [Fact]
    public async Task GetPublicProfile_Should_Aggregate_Authoritative_Public_Pet_Card()
    {
        const string userPublicId = "usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f";
        var user = new UserVo
        {
            Uuid = 1001,
            VoPublicId = userPublicId,
            VoUserName = "tester",
            VoDisplayName = "Tester",
            VoTenantId = 9
        };
        var petCard = new PetPublicCardVo
        {
            VoPublicId = "pet_018f6b6f7c7d70008f8f8f8f8f8f8f8f",
            VoName = "小萝卜",
            VoSpeciesKey = "radish",
            VoShapeKey = "sprout",
            VoGrowthStage = 2,
            VoMood = "happy"
        };
        var userService = new Mock<IUserService>(MockBehavior.Strict);
        userService
            .Setup(service => service.GetPublicUserByIdentifierAsync(userPublicId))
            .ReturnsAsync(user);
        var attachmentService = new Mock<IAttachmentService>(MockBehavior.Strict);
        attachmentService
            .Setup(service => service.GetLatestAvatarAssetAsync(1001))
            .ReturnsAsync((AttachmentAssetDto?)null);
        var adornmentService = new Mock<IUserAdornmentService>(MockBehavior.Strict);
        adornmentService
            .Setup(service => service.GetUserAdornmentAsync(1001))
            .ReturnsAsync((UserAdornmentVo?)null);
        var petService = new Mock<IPetService>(MockBehavior.Strict);
        petService
            .Setup(service => service.GetPublicCardAsync(1001, 9))
            .ReturnsAsync(petCard);
        var controller = new UserController(
            userService.Object,
            Mock.Of<ICurrentUserAccessor>(),
            Mock.Of<IPostService>(),
            Mock.Of<ICommentService>(),
            Mock.Of<IUserBrowseHistoryService>(),
            Mock.Of<IUserTimePreferenceService>(),
            attachmentService.Object,
            Options.Create(new TimeOptions()),
            adornmentService.Object,
            petService.Object);

        var result = await controller.GetPublicProfile(userPublicId);

        Assert.True(result.IsSuccess);
        var profile = Assert.IsType<UserPublicProfileVo>(result.ResponseData);
        Assert.Same(petCard, profile.VoPet);
        userService.VerifyAll();
        attachmentService.VerifyAll();
        adornmentService.VerifyAll();
        petService.VerifyAll();
    }

    [Fact]
    public async Task UpdateMyProfile_ShouldDelegateDisplayNameChangeToUserService()
    {
        var userService = new Mock<IUserService>(MockBehavior.Strict);
        var currentUserAccessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
        currentUserAccessor
            .SetupGet(item => item.Current)
            .Returns(new CurrentUser
            {
                IsAuthenticated = true,
                UserId = 1001,
                UserName = "tester"
            });
        userService
            .Setup(item => item.ChangeDisplayNameAsync(
                1001,
                "NewName",
                It.Is<UserDisplayNameChangeContext>(context =>
                    context.OperatorUserId == 1001 &&
                    context.OperatorUserName == "tester" &&
                    context.Source == UserDisplayNameChangeSources.Profile &&
                    context.Reason == "用户个人资料修改")))
            .ReturnsAsync(true);
        userService
            .Setup(item => item.UpdateColumnsAsync(
                It.IsAny<Expression<Func<User, User>>>(),
                It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(1);

        var controller = new UserController(
            userService.Object,
            currentUserAccessor.Object,
            Mock.Of<IPostService>(),
            Mock.Of<ICommentService>(),
            Mock.Of<IUserBrowseHistoryService>(),
            Mock.Of<IUserTimePreferenceService>(),
            Mock.Of<IAttachmentService>(),
            Options.Create(new TimeOptions()),
            Mock.Of<IUserAdornmentService>(),
            Mock.Of<IPetService>());

        var result = await controller.UpdateMyProfile(new UpdateMyProfileDto
        {
            UserName = " NewName "
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("更新成功", result.MessageInfo);
        userService.Verify(
            item => item.ChangeDisplayNameAsync(
                1001,
                "NewName",
                It.IsAny<UserDisplayNameChangeContext>()),
            Times.Once);
        userService.Verify(
            item => item.UpdateColumnsAsync(
                It.IsAny<Expression<Func<User, User>>>(),
                It.IsAny<Expression<Func<User, bool>>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0, "已清空头像")]
    [InlineData(9001, "设置成功")]
    public async Task SetMyAvatar_Should_Delegate_Transactional_Association_To_AttachmentService(
        long attachmentId,
        string expectedMessage)
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
        currentUserAccessor
            .SetupGet(item => item.Current)
            .Returns(new CurrentUser
            {
                IsAuthenticated = true,
                UserId = 1001,
                UserName = "tester"
            });
        var attachmentService = new Mock<IAttachmentService>(MockBehavior.Strict);
        attachmentService
            .Setup(service => service.SetCurrentAvatarAsync(attachmentId, 1001, "tester"))
            .Returns(Task.CompletedTask);
        var controller = new UserController(
            Mock.Of<IUserService>(),
            currentUserAccessor.Object,
            Mock.Of<IPostService>(),
            Mock.Of<ICommentService>(),
            Mock.Of<IUserBrowseHistoryService>(),
            Mock.Of<IUserTimePreferenceService>(),
            attachmentService.Object,
            Options.Create(new TimeOptions()),
            Mock.Of<IUserAdornmentService>(),
            Mock.Of<IPetService>());

        var result = await controller.SetMyAvatar(new SetMyAvatarDto { AttachmentId = attachmentId });

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedMessage, result.MessageInfo);
        attachmentService.VerifyAll();
    }
}
