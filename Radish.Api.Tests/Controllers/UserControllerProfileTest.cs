using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public class UserControllerProfileTest
{
    [Fact]
    public async Task UpdateMyProfile_ShouldUseSystemSettingDisplayNameLength()
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

        var systemSettingProvider = new Mock<ISystemSettingProvider>(MockBehavior.Strict);
        systemSettingProvider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.DisplayNameMinLengthKey))
            .ReturnsAsync(4);
        systemSettingProvider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.DisplayNameMaxLengthKey))
            .ReturnsAsync(int.Parse(SystemConfigDefaults.DefaultDisplayNameMaxLength));

        var controller = new UserController(
            userService.Object,
            currentUserAccessor.Object,
            Mock.Of<IPostService>(),
            Mock.Of<ICommentService>(),
            Mock.Of<IUserBrowseHistoryService>(),
            Mock.Of<IUserTimePreferenceService>(),
            Mock.Of<IAttachmentService>(),
            Mock.Of<INotificationPushService>(),
            systemSettingProvider.Object,
            Options.Create(new TimeOptions()));

        var result = await controller.UpdateMyProfile(new UpdateMyProfileDto
        {
            UserName = "abc"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("显示名长度必须在 4-24 个字符之间", result.MessageInfo);
        systemSettingProvider.Verify(
            item => item.GetInt32Async(SystemConfigDefaults.DisplayNameMinLengthKey),
            Times.Once);
        systemSettingProvider.Verify(
            item => item.GetInt32Async(SystemConfigDefaults.DisplayNameMaxLengthKey),
            Times.Once);
    }
}
