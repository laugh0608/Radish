using JetBrains.Annotations;
using Microsoft.Extensions.Localization;
using Moq;
using Radish.Auth.Localization;
using Radish.Auth.Resources;
using Xunit;

namespace Radish.Api.Tests.Auth;

[TestSubject(typeof(AuthClientText))]
public class AuthClientTextTest
{
    [Fact]
    public void Resolve_ShouldUseStableClientResourceAndKeepUnknownFallback()
    {
        var localizer = new Mock<IStringLocalizer<Errors>>();
        localizer
            .Setup(item => item["auth.client.radish-console.description"])
            .Returns(new LocalizedString(
                "auth.client.radish-console.description",
                "Localized Console description"));
        localizer
            .Setup(item => item["auth.client.third-party.description"])
            .Returns(new LocalizedString(
                "auth.client.third-party.description",
                "auth.client.third-party.description",
                resourceNotFound: true));

        Assert.Equal(
            "Localized Console description",
            AuthClientText.Resolve(localizer.Object, "radish-console", "description", "Configured description"));
        Assert.Equal(
            "Configured description",
            AuthClientText.Resolve(localizer.Object, "third-party", "description", "Configured description"));
    }
}
