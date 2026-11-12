using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Radish.Api.Tests.ErrorHandling;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests;

public sealed partial class PublicMetadataLoggingTests
{
    public static IEnumerable<object[]> OutputFailureRoutes()
    {
        foreach (var route in HangfireOutputLoggingTests.OutputRoutes())
        foreach (var source in new[] { "head-cache", "sitemap-generation", "gateway-request", "gateway-injection" })
            yield return route.Concat(new object[] { source }).ToArray();
    }

    [Theory]
    [MemberData(nameof(OutputFailureRoutes))]
    public async Task FinalOutput_ShouldPreserveOutputFailureBehavior(bool candidate, string environment, bool audit, string source)
    {
        using var broken = new HangfireBrokenWriter();
        using var output = new HttpBoundaryCapture(candidate, environment,
            service: source.StartsWith("gateway-", StringComparison.Ordinal) ? "gateway" : "api", audit: audit, output: broken);
        using var global = new ApplicationLogScope(output.Logger);
        var sinkFailure = new IOException(Secret);
        output.Sink.EmitFailure = sinkFailure;

        switch (source)
        {
            case "head-cache":
            {
                var fixture = new Fixture();
                fixture.Cache.Setup(c => c.GetStringAsync(It.IsAny<string>())).ThrowsAsync(new IOException("cache read"));
                if (audit)
                    await AssertAuditFailure(() => fixture.HeadController.GetStaticRoute("forum"));
                else
                    Assert.IsType<OkObjectResult>(await fixture.HeadController.GetStaticRoute("forum"));
                fixture.Cache.Verify(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), TimeSpan.FromMinutes(20)),
                    audit ? Times.Never() : Times.Once());
                break;
            }
            case "sitemap-generation":
            {
                var fixture = new Fixture();
                fixture.Tags.Setup(t => t.QueryIndexableTagPageAsync(1, PublicSitemapService.SectionPageSize))
                    .ThrowsAsync(new IOException("query"));
                if (audit)
                    await AssertAuditFailure(() => fixture.Sitemap.GetSectionXmlAsync("tags", 1, fixture.BaseUrl));
                else
                {
                    var xml = await fixture.Sitemap.GetSectionXmlAsync("tags", 1, fixture.BaseUrl);
                    Assert.Contains("<urlset", xml);
                    Assert.DoesNotContain("<url>", xml);
                }
                fixture.Cache.Verify(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
                break;
            }
            case "gateway-request":
            {
                using var fixture = new GatewayFixture(output.Logger);
                fixture.Handler.Response = _ => throw new HttpRequestException("request");
                var token = TestContext.Current.CancellationToken;
                if (audit)
                    await AssertAuditFailure(() => fixture.Client.GetSnapshotAsync("/docs", token));
                else
                    Assert.Null(await fixture.Client.GetSnapshotAsync("/docs", token));
                Assert.Equal(1, fixture.Handler.Calls);
                break;
            }
            case "gateway-injection":
            {
                using var fixture = new GatewayFixture(output.Logger);
                var context = fixture.Context();
                using var stream = new FailingStream();
                context.Response.Body = stream;
                if (audit)
                    await AssertAuditFailure(() => fixture.Middleware.InvokeAsync(context, fixture.Client));
                else
                    await fixture.Middleware.InvokeAsync(context, fixture.Client);
                Assert.Equal(2, fixture.Handler.Calls);
                Assert.Equal(!audit, fixture.NextCalled);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }

        Assert.Empty(output.Events);
        if (candidate)
        {
            using var emergency = JsonDocument.Parse(output.Emergency.ToString());
            Assert.Equal("pipeline.output_failed", emergency.RootElement.GetProperty("eventCode").GetString());
            Assert.DoesNotContain(Secret, output.Emergency.ToString());
        }
        else
            Assert.Equal("", output.Emergency.ToString());

        async Task AssertAuditFailure(Func<Task> invoke)
        {
            var failure = await Assert.ThrowsAsync<AggregateException>(invoke);
            Assert.Same(sinkFailure, Assert.Single(failure.Flatten().InnerExceptions));
        }
    }
}
