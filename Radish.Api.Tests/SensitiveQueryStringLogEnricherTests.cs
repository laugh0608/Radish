using System;
using Moq;
using Radish.Extension.Log;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests;

public class SensitiveQueryStringLogEnricherTests
{
    [Theory]
    [InlineData("QueryString")]
    [InlineData("RequestPath")]
    [InlineData("RequestUri")]
    [InlineData("Query")]
    public void Enrich_ShouldRedactSensitiveQueryParameters(string propertyName)
    {
        var logEvent = CreateLogEvent(
            propertyName,
            "/hub/chat?id=connection-1&access_token=secret-token&mode=websocket");

        new SensitiveQueryStringLogEnricher().Enrich(
            logEvent,
            Mock.Of<ILogEventPropertyFactory>());

        var property = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
        Assert.Equal(
            "/hub/chat?id=connection-1&access_token=[REDACTED]&mode=websocket",
            property.Value);
    }

    [Fact]
    public void Enrich_ShouldRedactOAuthCredentialsCaseInsensitively()
    {
        var logEvent = CreateLogEvent(
            "Uri",
            "/connect/authorize?CODE=authorization-code&client_secret=secret#fragment");

        new SensitiveQueryStringLogEnricher().Enrich(
            logEvent,
            Mock.Of<ILogEventPropertyFactory>());

        var property = Assert.IsType<ScalarValue>(logEvent.Properties["Uri"]);
        Assert.Equal(
            "/connect/authorize?CODE=[REDACTED]&client_secret=[REDACTED]#fragment",
            property.Value);
    }

    [Fact]
    public void Enrich_ShouldPreserveNonSensitiveProperties()
    {
        const string requestPath = "/api/channel-message/GetList?channelId=123&page=1";
        var logEvent = CreateLogEvent("RequestPath", requestPath);

        new SensitiveQueryStringLogEnricher().Enrich(
            logEvent,
            Mock.Of<ILogEventPropertyFactory>());

        var property = Assert.IsType<ScalarValue>(logEvent.Properties["RequestPath"]);
        Assert.Equal(requestPath, property.Value);
    }

    private static LogEvent CreateLogEvent(string propertyName, string propertyValue)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplate(Array.Empty<MessageTemplateToken>()),
            [new LogEventProperty(propertyName, new ScalarValue(propertyValue))]);
    }
}
