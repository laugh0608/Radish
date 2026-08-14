using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Gateway.PublicHead;
using Xunit;

namespace Radish.Api.Tests.Gateway;

public sealed class PublicHeadSnapshotMiddlewareTest
{
    [Fact]
    public async Task InvokeAsync_Should_Request_Forum_Tag_Snapshot_For_Tag_Path()
    {
        var handler = new PublicHeadHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DownstreamServices:ApiService:BaseUrl"] = "http://api.test",
                ["FrontendService:BaseUrl"] = "http://frontend.test"
            })
            .Build();
        var snapshotClient = new PublicHeadSnapshotClient(
            httpClient,
            configuration,
            memoryCache,
            NullLogger<PublicHeadSnapshotClient>.Instance);
        var nextCalled = false;
        var middleware = new PublicHeadSnapshotMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            memoryCache,
            NullLogger<PublicHeadSnapshotMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/forum/tag/csharp";
        context.Request.Headers.Accept = "text/html";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, snapshotClient);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var html = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.False(nextCalled);
        Assert.Contains("http://api.test/api/public-head/forum/tag/csharp", handler.RequestUrls);
        Assert.Contains("<title>C# - Radish 论坛</title>", html);
    }

    private sealed class PublicHeadHttpMessageHandler : HttpMessageHandler
    {
        public List<string> RequestUrls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestUrl = request.RequestUri!.ToString();
            RequestUrls.Add(requestUrl);

            if (requestUrl.Contains("/api/public-head/forum/tag/csharp"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "voTitle": "C# - Radish 论坛",
                          "voDescription": "C# 技术讨论",
                          "voCanonicalPath": "/forum/tag/csharp",
                          "voCanonicalUrl": "https://example.test/forum/tag/csharp",
                          "voOpenGraphType": "website",
                          "voJsonLd": "{\"@type\":\"CollectionPage\"}"
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<!doctype html><html><head><title>Radish</title></head><body></body></html>",
                    Encoding.UTF8,
                    "text/html")
            });
        }
    }
}
