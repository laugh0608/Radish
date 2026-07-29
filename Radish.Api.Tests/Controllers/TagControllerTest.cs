using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Resources;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public sealed class TagControllerTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task GetHotTags_Should_Return_Stable_BadRequest_When_TopCount_Is_Invalid(int topCount)
    {
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var controller = CreateController(tagService.Object);

        var result = await controller.GetHotTags(topCount);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TagDiscoveryErrorCodes.TopCountInvalid, result.Code);
        Assert.Equal(
            TagDiscoveryErrorCodes.ResolveMessageKey(TagDiscoveryErrorCodes.TopCountInvalid),
            result.MessageKey);
    }

    [Fact]
    public async Task GetRelated_Should_Return_Stable_NotFound_When_Source_Tag_Is_Unavailable()
    {
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        tagService
            .Setup(service => service.GetRelatedTagsAsync("missing", 8))
            .ReturnsAsync((List<TagVo>?)null);
        var controller = CreateController(tagService.Object);

        var result = await controller.GetRelated("missing");

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(TagDiscoveryErrorCodes.TagUnavailable, result.Code);
        Assert.Equal(
            TagDiscoveryErrorCodes.ResolveMessageKey(TagDiscoveryErrorCodes.TagUnavailable),
            result.MessageKey);
    }

    [Fact]
    public async Task GetRelated_Should_Return_Public_Related_Tags()
    {
        var relatedTags = new List<TagVo>
        {
            new()
            {
                VoId = 5002,
                VoName = "ASP.NET",
                VoSlug = "aspnet",
                VoPostCount = 3
            }
        };
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        tagService
            .Setup(service => service.GetRelatedTagsAsync("csharp", 8))
            .ReturnsAsync(relatedTags);
        var controller = CreateController(tagService.Object);

        var result = await controller.GetRelated("csharp");

        Assert.True(result.IsSuccess);
        Assert.Equal(relatedTags, Assert.IsType<List<TagVo>>(result.ResponseData));
    }

    private static TagController CreateController(ITagService tagService)
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
        currentUserAccessor
            .SetupGet(accessor => accessor.Current)
            .Returns(CurrentUser.Anonymous);

        var localizer = new Mock<IStringLocalizer<Errors>>();
        localizer
            .Setup(item => item[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key, resourceNotFound: true));

        return new TagController(
            tagService,
            currentUserAccessor.Object,
            localizer.Object);
    }
}
