using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Radish.Auth.OpenIddict;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Security;

public class OpenIddictTransportSecurityPolicyTests
{
    [Fact]
    public void DevelopmentWithExplicitOptIn_ShouldDisableTransportSecurityRequirement()
    {
        var result = OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            CreateConfiguration(true),
            CreateEnvironment(Environments.Development));

        result.ShouldBeTrue();
    }

    [Fact]
    public void DevelopmentWithoutOptIn_ShouldKeepTransportSecurityRequirement()
    {
        var result = OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            CreateConfiguration(false),
            CreateEnvironment(Environments.Development));

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void NonDevelopmentWithInsecureHttp_ShouldFailFast(string environmentName)
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
                CreateConfiguration(true),
                CreateEnvironment(environmentName)));

        exception.Message.ShouldContain("AllowInsecureHttp=true");
        exception.Message.ShouldContain(environmentName);
    }

    [Fact]
    public void ProductionWithSecureTransport_ShouldKeepTransportSecurityRequirement()
    {
        var result = OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement(
            CreateConfiguration(false),
            CreateEnvironment(Environments.Production));

        result.ShouldBeFalse();
    }

    private static IConfiguration CreateConfiguration(bool allowInsecureHttp)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenIddict:Server:AllowInsecureHttp"] = allowInsecureHttp.ToString()
            })
            .Build();
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }
}
