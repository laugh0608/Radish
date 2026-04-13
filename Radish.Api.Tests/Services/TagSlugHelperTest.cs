using Radish.Model;
using Xunit;

namespace Radish.Api.Tests.Services;

public class TagSlugHelperTest
{
    [Theory]
    [InlineData("社区新闻", null, "community-news")]
    [InlineData("社区活动", null, "community-events")]
    [InlineData("精华帖", null, "featured-posts")]
    [InlineData("碎碎念", null, "random-thoughts")]
    [InlineData("公告", null, "announcements")]
    [InlineData("问答", null, "questions")]
    [InlineData("投票", null, "polls")]
    [InlineData("抽奖", null, "lotteries")]
    [InlineData("Hello World", null, "hello-world")]
    [InlineData("碎碎念", "moments-notes", "moments-notes")]
    [InlineData("自定义标签", "custom-slug", "custom-slug")]
    public void BuildCanonicalSlug_Should_Return_Expected_Value(string name, string? preferredSlug, string expected)
    {
        var actual = TagSlugHelper.BuildCanonicalSlug(name, preferredSlug);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildCanonicalSlug_Should_Fallback_To_Unicode_Slug_For_Unknown_NonAscii_Tag()
    {
        var actual = TagSlugHelper.BuildCanonicalSlug("测试标签");

        Assert.Equal("u6d4b-u8bd5-u6807-u7b7e", actual);
    }
}
