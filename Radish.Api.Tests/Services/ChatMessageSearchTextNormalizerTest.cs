using Radish.Model;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ChatMessageSearchTextNormalizerTest
{
    [Theory]
    [InlineData("  Hello\r\n\tWORLD  ", "hello world")]
    [InlineData("欢迎 @[萝卜](123456) 回来", "欢迎 @萝卜 回来")]
    [InlineData("sticker://radish/hello", null)]
    [InlineData("前文 sticker://radish/hello 后文", "前文 后文")]
    [InlineData("控制\u0000字符\u0007测试", "控制字符测试")]
    public void Normalize_ShouldKeepOnlyStableVisibleText(string source, string? expected)
    {
        Assert.Equal(expected, ChatMessageSearchTextNormalizer.Normalize(source));
    }

    [Fact]
    public void Normalize_ShouldRespectMaximumLengthWithoutSplittingSurrogatePair()
    {
        var source = new string('a', ChatMessageSearchTextNormalizer.MaximumLength - 1) + "😀tail";

        var normalized = ChatMessageSearchTextNormalizer.Normalize(source);

        Assert.NotNull(normalized);
        Assert.True(normalized!.Length <= ChatMessageSearchTextNormalizer.MaximumLength);
        Assert.False(char.IsHighSurrogate(normalized[^1]));
    }
}
