using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.Common.CacheTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public class PublicHeadSnapshotServiceTest
{
    [Fact]
    public async Task GetForumPostSnapshotAsync_Should_Build_Public_Post_Head_Snapshot()
    {
        var cache = CreateCacheMock();
        var post = new Post
        {
            Id = 1001,
            PublicId = "post-alpha",
            Title = "公开帖子",
            Summary = "公开帖子摘要",
            Content = "# 正文",
            AuthorName = "作者",
            CoverAttachmentId = 9001,
            IsPublished = true,
            IsEnabled = true,
            IsDeleted = false,
            PublishTime = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc),
            ModifyTime = new DateTime(2026, 5, 2, 8, 0, 0, DateTimeKind.Utc),
            CreateTime = new DateTime(2026, 4, 30, 8, 0, 0, DateTimeKind.Utc)
        };
        var postRepository = CreateRepository(post);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        attachmentUrlResolver
            .Setup(resolver => resolver.ResolveAttachmentUrl(9001))
            .Returns("/_assets/attachments/9001");

        var service = CreateService(cache, postRepository: postRepository, attachmentUrlResolver: attachmentUrlResolver);

        var snapshot = await service.GetForumPostSnapshotAsync("post-alpha", "https://example.test/");

        Assert.NotNull(snapshot);
        Assert.Equal("公开帖子 - Radish 论坛", snapshot.VoTitle);
        Assert.Equal("https://example.test/forum/post/post-alpha", snapshot.VoCanonicalUrl);
        Assert.Equal("https://example.test/_assets/attachments/9001", snapshot.VoImageUrl);
        Assert.Contains("\"@type\":\"BlogPosting\"", snapshot.VoJsonLd);
        Assert.Contains("\"dateModified\":\"2026-05-02T08:00:00Z\"", snapshot.VoJsonLd);
    }

    [Fact]
    public async Task GetForumPostSnapshotAsync_Should_Not_Expose_Draft_Post()
    {
        var cache = CreateCacheMock();
        var postRepository = CreateRepository(new Post
        {
            Id = 1002,
            PublicId = "draft",
            Title = "草稿",
            IsPublished = false,
            IsEnabled = true,
            IsDeleted = false
        });
        var service = CreateService(cache, postRepository: postRepository);

        var snapshot = await service.GetForumPostSnapshotAsync("draft", "https://example.test");

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task GetDocsSnapshotAsync_Should_Only_Expose_Public_Published_Document()
    {
        var cache = CreateCacheMock();
        var wikiDocumentRepository = CreateRepository(new WikiDocument
        {
            Id = 2001,
            Title = "公开文档",
            Slug = "guide",
            Summary = "公开文档摘要",
            MarkdownContent = "正文",
            Status = (int)WikiDocumentStatusEnum.Published,
            Visibility = (int)WikiDocumentVisibilityEnum.Public,
            IsDeleted = false,
            PublishedAt = new DateTime(2026, 5, 3, 8, 0, 0, DateTimeKind.Utc),
            CreateTime = new DateTime(2026, 5, 2, 8, 0, 0, DateTimeKind.Utc)
        });
        var service = CreateService(cache, wikiDocumentRepository: wikiDocumentRepository);

        var snapshot = await service.GetDocsSnapshotAsync("guide", "https://example.test");

        Assert.NotNull(snapshot);
        Assert.Equal("公开文档 - Radish 文档", snapshot.VoTitle);
        Assert.Equal("https://example.test/docs/guide", snapshot.VoCanonicalUrl);
        Assert.Contains("\"@type\":\"Article\"", snapshot.VoJsonLd);
    }

    [Fact]
    public async Task GetShopProductSnapshotAsync_Should_Not_Expose_OffSale_Product()
    {
        var cache = CreateCacheMock();
        var productRepository = CreateRepository(new Product
        {
            Id = 3001,
            Name = "未上架商品",
            IsEnabled = true,
            IsOnSale = false,
            IsDeleted = false
        });
        var service = CreateService(cache, productRepository: productRepository);

        var snapshot = await service.GetShopProductSnapshotAsync(3001, "https://example.test");

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task GetShopProductSnapshotAsync_Should_Build_Product_JsonLd()
    {
        var cache = CreateCacheMock();
        var productRepository = CreateRepository(new Product
        {
            Id = 3002,
            Name = "公开商品",
            Description = "商品描述",
            CategoryId = "boost",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.RenameCard,
            IsEnabled = true,
            IsOnSale = true,
            IsDeleted = false,
            OnSaleTime = new DateTime(2026, 5, 4, 8, 0, 0, DateTimeKind.Utc),
            CreateTime = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc)
        });
        var service = CreateService(cache, productRepository: productRepository);

        var snapshot = await service.GetShopProductSnapshotAsync(3002, "https://example.test");

        Assert.NotNull(snapshot);
        Assert.Equal("product", snapshot.VoOpenGraphType);
        Assert.Equal("https://example.test/shop/product/3002", snapshot.VoCanonicalUrl);
        Assert.Contains("\"@type\":\"Product\"", snapshot.VoJsonLd);
        Assert.Contains("\"category\":\"boost\"", snapshot.VoJsonLd);
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

    private static Mock<IBaseRepository<TEntity>> CreateRepository<TEntity>(TEntity entity) where TEntity : class
    {
        var repository = new Mock<IBaseRepository<TEntity>>(MockBehavior.Strict);
        repository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<TEntity, bool>>?>()))
            .ReturnsAsync((Expression<Func<TEntity, bool>>? expression) =>
                expression is null || expression.Compile().Invoke(entity) ? entity : null);
        return repository;
    }

    private static PublicHeadSnapshotService CreateService(
        Mock<ICaching> cache,
        Mock<IBaseRepository<Post>>? postRepository = null,
        Mock<IBaseRepository<WikiDocument>>? wikiDocumentRepository = null,
        Mock<IBaseRepository<Product>>? productRepository = null,
        Mock<IAttachmentUrlResolver>? attachmentUrlResolver = null)
    {
        return new PublicHeadSnapshotService(
            cache.Object,
            (postRepository ?? new Mock<IBaseRepository<Post>>(MockBehavior.Strict)).Object,
            (wikiDocumentRepository ?? new Mock<IBaseRepository<WikiDocument>>(MockBehavior.Strict)).Object,
            (productRepository ?? new Mock<IBaseRepository<Product>>(MockBehavior.Strict)).Object,
            (attachmentUrlResolver ?? new Mock<IAttachmentUrlResolver>(MockBehavior.Strict)).Object);
    }
}
