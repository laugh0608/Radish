using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Radish.Common.CacheTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>公开 sitemap XML 生成服务。</summary>
public class PublicSitemapService : IPublicSitemapService
{
    public const int SectionPageSize = 5000;
    public const int MaxSectionPageCount = 20;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly ConcurrentDictionary<string, string> LastSuccessfulXml = new();
    private static readonly string[] StaticPaths =
    [
        "/discover",
        "/forum",
        "/forum/question",
        "/forum/poll",
        "/forum/lottery",
        "/docs",
        "/docs/search",
        "/leaderboard",
        "/shop",
        "/shop/products"
    ];

    private readonly ICaching _cache;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<WikiDocument> _wikiDocumentRepository;
    private readonly IBaseRepository<Product> _productRepository;

#pragma warning disable CS0618
    private static readonly Expression<Func<Product, bool>> PublicVisibleProductExpression = p =>
        p.IsEnabled &&
        p.IsOnSale &&
        !p.IsDeleted &&
        !(p.ProductType == ProductType.Benefit &&
          p.BenefitType == BenefitType.Theme &&
          p.BenefitValue != ShopThemeResources.DarkNight &&
          p.BenefitValue != ShopThemeResources.Sakura) &&
        !(p.ProductType == ProductType.Benefit && (
            p.BenefitType == BenefitType.AvatarFrame ||
            p.BenefitType == BenefitType.Signature ||
            p.BenefitType == BenefitType.NameColor ||
            p.BenefitType == BenefitType.LikeEffect)) &&
        !(p.ProductType == ProductType.Consumable && (
            p.ConsumableType == ConsumableType.PostPinCard ||
            p.ConsumableType == ConsumableType.PostHighlightCard ||
            p.ConsumableType == ConsumableType.DoubleExpCard ||
            p.ConsumableType == ConsumableType.LotteryTicket));
#pragma warning restore CS0618

    public PublicSitemapService(
        ICaching cache,
        IBaseRepository<Post> postRepository,
        IBaseRepository<WikiDocument> wikiDocumentRepository,
        IBaseRepository<Product> productRepository)
    {
        _cache = cache;
        _postRepository = postRepository;
        _wikiDocumentRepository = wikiDocumentRepository;
        _productRepository = productRepository;
    }

    public Task<string> GetIndexXmlAsync(string publicBaseUrl)
    {
        var normalizedBaseUrl = NormalizePublicBaseUrl(publicBaseUrl);
        var cacheKey = BuildCacheKey("index", "root", 1, normalizedBaseUrl);

        return GetCachedOrGenerateAsync(
            cacheKey,
            () => GenerateIndexXmlAsync(normalizedBaseUrl),
            () => BuildSitemapIndexXml(
                [new SitemapEntry(CombineUrl(normalizedBaseUrl, "/sitemaps/static.xml"), DateTime.UtcNow)]));
    }

    public Task<string> GetSectionXmlAsync(string section, int pageIndex, string publicBaseUrl)
    {
        var normalizedSection = NormalizeSection(section);
        var normalizedPageIndex = Math.Clamp(pageIndex, 1, MaxSectionPageCount);
        var normalizedBaseUrl = NormalizePublicBaseUrl(publicBaseUrl);
        var cacheKey = BuildCacheKey("section", normalizedSection, normalizedPageIndex, normalizedBaseUrl);

        return GetCachedOrGenerateAsync(
            cacheKey,
            () => GenerateSectionXmlAsync(normalizedSection, normalizedPageIndex, normalizedBaseUrl),
            () => BuildUrlSetXml([]));
    }

    private async Task<string> GetCachedOrGenerateAsync(
        string cacheKey,
        Func<Task<string>> generateXml,
        Func<string> buildFallbackXml)
    {
        var cachedXml = await TryGetCachedXmlAsync(cacheKey);
        if (!string.IsNullOrWhiteSpace(cachedXml))
        {
            return cachedXml;
        }

        try
        {
            var xml = await generateXml();
            LastSuccessfulXml[cacheKey] = xml;
            await TrySetCachedXmlAsync(cacheKey, xml);
            return xml;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "生成公开 sitemap 失败，CacheKey={CacheKey}", cacheKey);
            return LastSuccessfulXml.TryGetValue(cacheKey, out var lastSuccessfulXml)
                ? lastSuccessfulXml
                : buildFallbackXml();
        }
    }

    private async Task<string?> TryGetCachedXmlAsync(string cacheKey)
    {
        try
        {
            return await _cache.GetStringAsync(cacheKey);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "读取 sitemap 缓存失败，CacheKey={CacheKey}", cacheKey);
            return null;
        }
    }

    private async Task TrySetCachedXmlAsync(string cacheKey, string xml)
    {
        try
        {
            await _cache.SetStringAsync(cacheKey, xml, CacheTtl);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "写入 sitemap 缓存失败，CacheKey={CacheKey}", cacheKey);
        }
    }

    private async Task<string> GenerateIndexXmlAsync(string publicBaseUrl)
    {
        var now = DateTime.UtcNow;
        var entries = new List<SitemapEntry>
        {
            new(CombineUrl(publicBaseUrl, "/sitemaps/static.xml"), now)
        };

        await AddSectionIndexEntriesAsync(entries, "forum", publicBaseUrl, now, () => _postRepository.QueryCountAsync(IsPublicPost));
        await AddSectionIndexEntriesAsync(entries, "docs", publicBaseUrl, now, () => _wikiDocumentRepository.QueryCountAsync(IsPublicWikiDocument));
        await AddSectionIndexEntriesAsync(entries, "shop", publicBaseUrl, now, () => _productRepository.QueryCountAsync(PublicVisibleProductExpression));

        return BuildSitemapIndexXml(entries);
    }

    private static async Task AddSectionIndexEntriesAsync(
        ICollection<SitemapEntry> entries,
        string section,
        string publicBaseUrl,
        DateTime lastModifiedAtUtc,
        Func<Task<int>> countFactory)
    {
        int totalCount;
        try
        {
            totalCount = await countFactory();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "生成 sitemap index 时统计 {Section} 分片失败", section);
            return;
        }

        var pageCount = Math.Min(MaxSectionPageCount, (int)Math.Ceiling(totalCount / (double)SectionPageSize));

        for (var pageIndex = 1; pageIndex <= pageCount; pageIndex++)
        {
            entries.Add(new SitemapEntry(
                CombineUrl(publicBaseUrl, $"/sitemaps/{section}-{pageIndex}.xml"),
                lastModifiedAtUtc));
        }
    }

    private async Task<string> GenerateSectionXmlAsync(string section, int pageIndex, string publicBaseUrl)
    {
        var entries = section switch
        {
            "static" => BuildStaticEntries(publicBaseUrl, DateTime.UtcNow),
            "forum" => await BuildForumEntriesAsync(pageIndex, publicBaseUrl),
            "docs" => await BuildDocsEntriesAsync(pageIndex, publicBaseUrl),
            "shop" => await BuildShopEntriesAsync(pageIndex, publicBaseUrl),
            _ => []
        };

        return BuildUrlSetXml(entries);
    }

    private static List<UrlEntry> BuildStaticEntries(string publicBaseUrl, DateTime generatedAtUtc)
    {
        return StaticPaths
            .Select(path => new UrlEntry(CombineUrl(publicBaseUrl, path), generatedAtUtc))
            .ToList();
    }

    private async Task<List<UrlEntry>> BuildForumEntriesAsync(int pageIndex, string publicBaseUrl)
    {
        var (posts, _) = await _postRepository.QueryPageAsync(
            IsPublicPost,
            pageIndex,
            SectionPageSize,
            post => post.ModifyTime ?? post.PublishTime ?? post.CreateTime,
            OrderByType.Desc,
            post => post.Id,
            OrderByType.Desc);

        return posts
            .Select(post => new UrlEntry(
                CombineUrl(publicBaseUrl, BuildForumPostPath(post.PublicId, post.Id)),
                ResolveLastModifiedUtc(post.ModifyTime, post.PublishTime, post.CreateTime)))
            .ToList();
    }

    private async Task<List<UrlEntry>> BuildDocsEntriesAsync(int pageIndex, string publicBaseUrl)
    {
        var (documents, _) = await _wikiDocumentRepository.QueryPageAsync(
            IsPublicWikiDocument,
            pageIndex,
            SectionPageSize,
            document => document.ModifyTime ?? document.PublishedAt ?? document.CreateTime,
            OrderByType.Desc,
            document => document.Id,
            OrderByType.Desc);

        return documents
            .Select(document => new UrlEntry(
                CombineUrl(publicBaseUrl, BuildDocsPath(document.Slug, document.Id)),
                ResolveLastModifiedUtc(document.ModifyTime, document.PublishedAt, document.CreateTime)))
            .ToList();
    }

    private async Task<List<UrlEntry>> BuildShopEntriesAsync(int pageIndex, string publicBaseUrl)
    {
        var (products, _) = await _productRepository.QueryPageAsync(
            PublicVisibleProductExpression,
            pageIndex,
            SectionPageSize,
            product => product.ModifyTime ?? product.OnSaleTime ?? product.CreateTime,
            OrderByType.Desc,
            product => product.Id,
            OrderByType.Desc);

        return products
            .Select(product => new UrlEntry(
                CombineUrl(publicBaseUrl, $"/shop/product/{product.Id}"),
                ResolveLastModifiedUtc(product.ModifyTime, product.OnSaleTime, product.CreateTime)))
            .ToList();
    }

    private static string BuildSitemapIndexXml(IEnumerable<SitemapEntry> entries)
    {
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(SitemapNamespace + "sitemapindex",
                entries.Select(entry =>
                    new XElement(SitemapNamespace + "sitemap",
                        new XElement(SitemapNamespace + "loc", entry.Location),
                        new XElement(SitemapNamespace + "lastmod", FormatLastModified(entry.LastModifiedAtUtc))))));

        return ToXml(document);
    }

    private static string BuildUrlSetXml(IEnumerable<UrlEntry> entries)
    {
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(SitemapNamespace + "urlset",
                entries.Select(entry =>
                    new XElement(SitemapNamespace + "url",
                        new XElement(SitemapNamespace + "loc", entry.Location),
                        new XElement(SitemapNamespace + "lastmod", FormatLastModified(entry.LastModifiedAtUtc))))));

        return ToXml(document);
    }

    private static string ToXml(XDocument document)
    {
        return $"{document.Declaration}{document.ToString(SaveOptions.DisableFormatting)}";
    }

    private static string BuildForumPostPath(string? publicId, long fallbackPostId)
    {
        var routeId = !string.IsNullOrWhiteSpace(publicId)
            ? publicId.Trim()
            : fallbackPostId.ToString(CultureInfo.InvariantCulture);

        return $"/forum/post/{Uri.EscapeDataString(routeId)}";
    }

    private static string BuildDocsPath(string? slug, long documentId)
    {
        if (!string.IsNullOrWhiteSpace(slug))
        {
            return $"/docs/{Uri.EscapeDataString(slug.Trim())}";
        }

        return $"/wiki/doc/{documentId}";
    }

    private static string NormalizeSection(string section)
    {
        var normalized = section.Trim().ToLowerInvariant();
        return normalized is "static" or "forum" or "docs" or "shop"
            ? normalized
            : string.Empty;
    }

    private static string NormalizePublicBaseUrl(string publicBaseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(publicBaseUrl)
            ? "https://radishx.com"
            : publicBaseUrl.Trim();

        return normalized.TrimEnd('/');
    }

    private static string CombineUrl(string publicBaseUrl, string path)
    {
        return $"{publicBaseUrl}{(path.StartsWith('/') ? path : $"/{path}")}";
    }

    private static string FormatLastModified(DateTime value)
    {
        return ResolveUtc(value).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static DateTime ResolveLastModifiedUtc(DateTime? primary, DateTime? secondary, DateTime fallback)
    {
        return ResolveUtc(primary ?? secondary ?? fallback);
    }

    private static DateTime ResolveUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string BuildCacheKey(string kind, string section, int pageIndex, string publicBaseUrl)
    {
        return $"public-sitemap:{kind}:{section}:{pageIndex}:{SectionPageSize}:{BuildHash(publicBaseUrl)}";
    }

    private static string BuildHash(string value)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hashBytes[..8]).ToLowerInvariant();
    }

    private static readonly Expression<Func<Post, bool>> IsPublicPost = post =>
        post.IsPublished && post.IsEnabled && !post.IsDeleted;

    private static readonly Expression<Func<WikiDocument, bool>> IsPublicWikiDocument = document =>
        document.Status == (int)WikiDocumentStatusEnum.Published &&
        document.Visibility == (int)WikiDocumentVisibilityEnum.Public &&
        document.Slug != string.Empty &&
        !document.IsDeleted;

    private sealed record SitemapEntry(string Location, DateTime LastModifiedAtUtc);

    private sealed record UrlEntry(string Location, DateTime LastModifiedAtUtc);
}
