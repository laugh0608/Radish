using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.Common.CacheTool;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Service;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class PublicSitemapServiceTest
{
    [Fact]
    public async Task GetIndexXmlAsync_Should_List_Static_And_Capped_Detail_Sitemaps()
    {
        var cache = CreateCacheMock();
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var wikiDocumentRepository = new Mock<IBaseRepository<WikiDocument>>(MockBehavior.Strict);
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Strict);

        postRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(1);
        wikiDocumentRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<WikiDocument, bool>>?>()))
            .ReturnsAsync(PublicSitemapService.SectionPageSize + 1);
        productRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Product, bool>>?>()))
            .ReturnsAsync(PublicSitemapService.SectionPageSize * (PublicSitemapService.MaxSectionPageCount + 1));

        var service = CreateService(cache, postRepository, wikiDocumentRepository, productRepository);

        var xml = await service.GetIndexXmlAsync("https://example.test/");

        Assert.Contains("<sitemapindex", xml);
        Assert.Contains("https://example.test/sitemaps/static.xml", xml);
        Assert.Contains("https://example.test/sitemaps/forum-1.xml", xml);
        Assert.Contains("https://example.test/sitemaps/docs-2.xml", xml);
        Assert.Contains("https://example.test/sitemaps/shop-20.xml", xml);
        Assert.DoesNotContain("https://example.test/sitemaps/shop-21.xml", xml);
    }

    [Fact]
    public async Task GetSectionXmlAsync_Should_Build_Forum_Urls_With_Canonical_Lastmod()
    {
        var cache = CreateCacheMock();
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var wikiDocumentRepository = new Mock<IBaseRepository<WikiDocument>>(MockBehavior.Strict);
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Strict);

        postRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<Post, bool>>?>(),
                1,
                PublicSitemapService.SectionPageSize,
                It.IsAny<Expression<Func<Post, object>>?>(),
                OrderByType.Desc,
                It.IsAny<Expression<Func<Post, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((
                [
                    new Post
                    {
                        Id = 1001,
                        PublicId = "post alpha",
                        IsPublished = true,
                        IsEnabled = true,
                        IsDeleted = false,
                        PublishTime = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc),
                        ModifyTime = new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc),
                        CreateTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
                    },
                    new Post
                    {
                        Id = 1002,
                        PublicId = null,
                        IsPublished = true,
                        IsEnabled = true,
                        IsDeleted = false,
                        PublishTime = new DateTime(2026, 1, 4, 8, 0, 0, DateTimeKind.Utc),
                        CreateTime = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc)
                    }
                ],
                2));

        var service = CreateService(cache, postRepository, wikiDocumentRepository, productRepository);

        var xml = await service.GetSectionXmlAsync("forum", 1, "https://example.test");

        Assert.Contains("https://example.test/forum/post/post%20alpha", xml);
        Assert.Contains("https://example.test/forum/post/1002", xml);
        Assert.Contains("2026-01-03T12:00:00Z", xml);
        Assert.Contains("2026-01-04T08:00:00Z", xml);
    }

    [Fact]
    public async Task GetSectionXmlAsync_Should_Return_Cached_Xml_When_Available()
    {
        const string cachedXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><urlset />";
        var cache = new Mock<ICaching>(MockBehavior.Strict);
        cache
            .Setup(caching => caching.GetStringAsync(It.IsAny<string>()))
            .ReturnsAsync(cachedXml);

        var service = CreateService(cache);

        var xml = await service.GetSectionXmlAsync("forum", 1, "https://example.test");

        Assert.Equal(cachedXml, xml);
    }

    [Fact]
    public async Task GetSectionXmlAsync_Should_Return_Empty_Urlset_When_Query_Fails_Without_Cache()
    {
        var cache = CreateCacheMock();
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var wikiDocumentRepository = new Mock<IBaseRepository<WikiDocument>>(MockBehavior.Strict);
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Strict);

        postRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<Post, bool>>?>(),
                1,
                PublicSitemapService.SectionPageSize,
                It.IsAny<Expression<Func<Post, object>>?>(),
                OrderByType.Desc,
                It.IsAny<Expression<Func<Post, object>>?>(),
                OrderByType.Desc))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var service = CreateService(cache, postRepository, wikiDocumentRepository, productRepository);

        var xml = await service.GetSectionXmlAsync("forum", 1, "https://query-fails.example.test");

        Assert.Contains("<urlset", xml);
        Assert.DoesNotContain("<url>", xml);
    }

    private static Mock<ICaching> CreateCacheMock()
    {
        var cache = new Mock<ICaching>(MockBehavior.Strict);
        cache
            .Setup(caching => caching.GetStringAsync(It.IsAny<string>()))
            .ReturnsAsync((string)null!);
        cache
            .Setup(caching => caching.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        return cache;
    }

    private static PublicSitemapService CreateService(
        Mock<ICaching> cache,
        Mock<IBaseRepository<Post>>? postRepository = null,
        Mock<IBaseRepository<WikiDocument>>? wikiDocumentRepository = null,
        Mock<IBaseRepository<Product>>? productRepository = null)
    {
        return new PublicSitemapService(
            cache.Object,
            (postRepository ?? new Mock<IBaseRepository<Post>>(MockBehavior.Strict)).Object,
            (wikiDocumentRepository ?? new Mock<IBaseRepository<WikiDocument>>(MockBehavior.Strict)).Object,
            (productRepository ?? new Mock<IBaseRepository<Product>>(MockBehavior.Strict)).Object);
    }
}
