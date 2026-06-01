using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Radish.Common;
using Radish.Common.CacheTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Serilog;

namespace Radish.Service;

/// <summary>公开详情页 HTML head 快照服务。</summary>
public class PublicHeadSnapshotService : IPublicHeadSnapshotService
{
    private const string SiteName = "Radish";
    private const string DefaultDescription = "Radish 是一个用于创作、讨论、文档沉淀和轻量商城的社区工作台。";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(20);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex HtmlTagPattern = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownLinkPattern = new(@"\[([^\]]+)\]\([^\)]+\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownSymbolPattern = new(@"[#>*_`~!\-]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ICaching _cache;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<WikiDocument> _wikiDocumentRepository;
    private readonly IBaseRepository<Product> _productRepository;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;

#pragma warning disable CS0618
    private static readonly Expression<Func<Product, bool>> PublicVisibleProductExpression = product =>
        product.IsEnabled &&
        product.IsOnSale &&
        !product.IsDeleted &&
        !(product.ProductType == ProductType.Benefit && (
            product.BenefitType == BenefitType.Badge ||
            product.BenefitType == BenefitType.AvatarFrame ||
            product.BenefitType == BenefitType.Title ||
            product.BenefitType == BenefitType.Theme ||
            product.BenefitType == BenefitType.Signature ||
            product.BenefitType == BenefitType.NameColor ||
            product.BenefitType == BenefitType.LikeEffect)) &&
        !(product.ProductType == ProductType.Consumable && (
            product.ConsumableType == ConsumableType.PostPinCard ||
            product.ConsumableType == ConsumableType.PostHighlightCard ||
            product.ConsumableType == ConsumableType.DoubleExpCard ||
            product.ConsumableType == ConsumableType.LotteryTicket));
#pragma warning restore CS0618

    public PublicHeadSnapshotService(
        ICaching cache,
        IBaseRepository<Post> postRepository,
        IBaseRepository<WikiDocument> wikiDocumentRepository,
        IBaseRepository<Product> productRepository,
        IAttachmentUrlResolver attachmentUrlResolver)
    {
        _cache = cache;
        _postRepository = postRepository;
        _wikiDocumentRepository = wikiDocumentRepository;
        _productRepository = productRepository;
        _attachmentUrlResolver = attachmentUrlResolver;
    }

    public Task<PublicHeadSnapshotVo?> GetForumPostSnapshotAsync(string postKey, string publicBaseUrl)
    {
        var normalizedPostKey = postKey.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPostKey))
        {
            return Task.FromResult<PublicHeadSnapshotVo?>(null);
        }

        var normalizedBaseUrl = NormalizePublicBaseUrl(publicBaseUrl);
        var cacheKey = BuildCacheKey("forum-post", normalizedPostKey, normalizedBaseUrl);

        return GetCachedOrGenerateAsync(cacheKey, async () =>
        {
            var post = await QueryPublicPostAsync(normalizedPostKey);
            return post is null ? null : BuildForumPostSnapshot(post, normalizedBaseUrl);
        });
    }

    public Task<PublicHeadSnapshotVo?> GetDocsSnapshotAsync(string slug, string publicBaseUrl)
    {
        var normalizedSlug = slug.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            return Task.FromResult<PublicHeadSnapshotVo?>(null);
        }

        var normalizedBaseUrl = NormalizePublicBaseUrl(publicBaseUrl);
        var cacheKey = BuildCacheKey("docs", normalizedSlug, normalizedBaseUrl);

        return GetCachedOrGenerateAsync(cacheKey, async () =>
        {
            var document = await _wikiDocumentRepository.QueryFirstAsync(doc =>
                doc.Slug == normalizedSlug &&
                doc.Status == (int)WikiDocumentStatusEnum.Published &&
                doc.Visibility == (int)WikiDocumentVisibilityEnum.Public &&
                !doc.IsDeleted);

            return document is null ? null : BuildDocsSnapshot(document, normalizedBaseUrl);
        });
    }

    public Task<PublicHeadSnapshotVo?> GetShopProductSnapshotAsync(long productId, string publicBaseUrl)
    {
        if (productId <= 0)
        {
            return Task.FromResult<PublicHeadSnapshotVo?>(null);
        }

        var normalizedBaseUrl = NormalizePublicBaseUrl(publicBaseUrl);
        var cacheKey = BuildCacheKey("shop-product", productId.ToString(CultureInfo.InvariantCulture), normalizedBaseUrl);

        return GetCachedOrGenerateAsync(cacheKey, async () =>
        {
            var product = await _productRepository.QueryFirstAsync(PublicVisibleProductExpression.And(product => product.Id == productId));
            return product is null ? null : BuildShopProductSnapshot(product, normalizedBaseUrl);
        });
    }

    private async Task<PublicHeadSnapshotVo?> GetCachedOrGenerateAsync(
        string cacheKey,
        Func<Task<PublicHeadSnapshotVo?>> generateSnapshot)
    {
        var cachedSnapshot = await TryGetCachedSnapshotAsync(cacheKey);
        if (cachedSnapshot is not null)
        {
            return cachedSnapshot;
        }

        var snapshot = await generateSnapshot();
        if (snapshot is not null)
        {
            await TrySetCachedSnapshotAsync(cacheKey, snapshot);
        }

        return snapshot;
    }

    private async Task<PublicHeadSnapshotVo?> TryGetCachedSnapshotAsync(string cacheKey)
    {
        try
        {
            var cachedJson = await _cache.GetStringAsync(cacheKey);
            return string.IsNullOrWhiteSpace(cachedJson)
                ? null
                : JsonSerializer.Deserialize<PublicHeadSnapshotVo>(cachedJson, JsonOptions);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "读取公开 head 快照缓存失败，CacheKey={CacheKey}", cacheKey);
            return null;
        }
    }

    private async Task TrySetCachedSnapshotAsync(string cacheKey, PublicHeadSnapshotVo snapshot)
    {
        try
        {
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(snapshot, JsonOptions), CacheTtl);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "写入公开 head 快照缓存失败，CacheKey={CacheKey}", cacheKey);
        }
    }

    private async Task<Post?> QueryPublicPostAsync(string postKey)
    {
        if (long.TryParse(postKey, NumberStyles.None, CultureInfo.InvariantCulture, out var postId) && postId > 0)
        {
            return await _postRepository.QueryFirstAsync(post =>
                post.Id == postId &&
                post.IsPublished &&
                post.IsEnabled &&
                !post.IsDeleted);
        }

        return await _postRepository.QueryFirstAsync(post =>
            post.PublicId == postKey &&
            post.IsPublished &&
            post.IsEnabled &&
            !post.IsDeleted);
    }

    private PublicHeadSnapshotVo BuildForumPostSnapshot(Post post, string publicBaseUrl)
    {
        var path = $"/forum/post/{Uri.EscapeDataString(!string.IsNullOrWhiteSpace(post.PublicId) ? post.PublicId.Trim() : post.Id.ToString(CultureInfo.InvariantCulture))}";
        var title = $"{NormalizeText(post.Title, "未命名帖子")} - Radish 论坛";
        var description = BuildDescription(post.Summary, post.Content);
        var publishedAt = ResolveUtc(post.PublishTime ?? post.CreateTime);
        var modifiedAt = ResolveUtc(post.ModifyTime ?? post.PublishTime ?? post.CreateTime);
        var imageUrl = ResolveAbsoluteImageUrl(post.CoverAttachmentId, publicBaseUrl);
        var canonicalUrl = CombineUrl(publicBaseUrl, path);

        return new PublicHeadSnapshotVo
        {
            VoTitle = title,
            VoDescription = description,
            VoCanonicalPath = path,
            VoCanonicalUrl = canonicalUrl,
            VoOpenGraphType = "article",
            VoImageUrl = imageUrl,
            VoPublishedAt = publishedAt,
            VoModifiedAt = modifiedAt,
            VoJsonLd = BuildJsonLd(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "BlogPosting",
                ["headline"] = post.Title,
                ["description"] = description,
                ["url"] = canonicalUrl,
                ["mainEntityOfPage"] = canonicalUrl,
                ["datePublished"] = FormatDateTime(publishedAt),
                ["dateModified"] = FormatDateTime(modifiedAt),
                ["author"] = BuildNamedThing("Person", NormalizeText(post.AuthorName, SiteName)),
                ["publisher"] = BuildNamedThing("Organization", SiteName),
                ["image"] = imageUrl
            })
        };
    }

    private PublicHeadSnapshotVo BuildDocsSnapshot(WikiDocument document, string publicBaseUrl)
    {
        var path = $"/docs/{Uri.EscapeDataString(document.Slug.Trim())}";
        var title = $"{NormalizeText(document.Title, "未命名文档")} - Radish 文档";
        var description = BuildDescription(document.Summary, document.MarkdownContent);
        var publishedAt = ResolveUtc(document.PublishedAt ?? document.CreateTime);
        var modifiedAt = ResolveUtc(document.ModifyTime ?? document.PublishedAt ?? document.CreateTime);
        var imageUrl = ResolveAbsoluteImageUrl(document.CoverAttachmentId, publicBaseUrl);
        var canonicalUrl = CombineUrl(publicBaseUrl, path);

        return new PublicHeadSnapshotVo
        {
            VoTitle = title,
            VoDescription = description,
            VoCanonicalPath = path,
            VoCanonicalUrl = canonicalUrl,
            VoOpenGraphType = "article",
            VoImageUrl = imageUrl,
            VoPublishedAt = publishedAt,
            VoModifiedAt = modifiedAt,
            VoJsonLd = BuildJsonLd(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "Article",
                ["headline"] = document.Title,
                ["description"] = description,
                ["url"] = canonicalUrl,
                ["mainEntityOfPage"] = canonicalUrl,
                ["datePublished"] = FormatDateTime(publishedAt),
                ["dateModified"] = FormatDateTime(modifiedAt),
                ["author"] = BuildNamedThing("Organization", SiteName),
                ["publisher"] = BuildNamedThing("Organization", SiteName),
                ["image"] = imageUrl
            })
        };
    }

    private PublicHeadSnapshotVo BuildShopProductSnapshot(Product product, string publicBaseUrl)
    {
        var path = $"/shop/product/{product.Id}";
        var title = $"{NormalizeText(product.Name, "未命名商品")} - Radish 商城";
        var description = BuildDescription(product.Description, $"{product.Name} - Radish 商城商品");
        var publishedAt = ResolveUtc(product.OnSaleTime ?? product.CreateTime);
        var modifiedAt = ResolveUtc(product.ModifyTime ?? product.OnSaleTime ?? product.CreateTime);
        var imageUrl = ResolveAbsoluteImageUrl(product.CoverAttachmentId ?? product.IconAttachmentId, publicBaseUrl);
        var canonicalUrl = CombineUrl(publicBaseUrl, path);

        return new PublicHeadSnapshotVo
        {
            VoTitle = title,
            VoDescription = description,
            VoCanonicalPath = path,
            VoCanonicalUrl = canonicalUrl,
            VoOpenGraphType = "product",
            VoImageUrl = imageUrl,
            VoPublishedAt = publishedAt,
            VoModifiedAt = modifiedAt,
            VoJsonLd = BuildJsonLd(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "Product",
                ["name"] = product.Name,
                ["description"] = description,
                ["url"] = canonicalUrl,
                ["image"] = imageUrl,
                ["category"] = product.CategoryId,
                ["releaseDate"] = FormatDateTime(publishedAt)
            })
        };
    }

    private string? ResolveAbsoluteImageUrl(long? attachmentId, string publicBaseUrl)
    {
        if (!attachmentId.HasValue || attachmentId.Value <= 0)
        {
            return null;
        }

        var imagePath = _attachmentUrlResolver.ResolveAttachmentUrl(attachmentId.Value);
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return null;
        }

        return Uri.TryCreate(imagePath, UriKind.Absolute, out var absoluteUri) &&
               (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps)
            ? imagePath
            : CombineUrl(publicBaseUrl, imagePath);
    }

    private static string BuildDescription(string? primary, string? fallback)
    {
        var description = NormalizePlainText(primary);
        if (string.IsNullOrWhiteSpace(description))
        {
            description = NormalizePlainText(fallback);
        }

        return Truncate(string.IsNullOrWhiteSpace(description) ? DefaultDescription : description, 180);
    }

    private static string NormalizePlainText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = HtmlTagPattern.Replace(value, " ");
        text = MarkdownLinkPattern.Replace(text, "$1");
        text = MarkdownSymbolPattern.Replace(text, " ");
        return NormalizeText(text, string.Empty);
    }

    private static string NormalizeText(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = WhitespacePattern.Replace(value, " ").Trim();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");
    }

    private static Dictionary<string, object?> BuildNamedThing(string type, string name)
    {
        return new Dictionary<string, object?>
        {
            ["@type"] = type,
            ["name"] = name
        };
    }

    private static string BuildJsonLd(Dictionary<string, object?> payload)
    {
        var compactPayload = payload
            .Where(pair => pair.Value switch
            {
                null => false,
                string stringValue => !string.IsNullOrWhiteSpace(stringValue),
                _ => true
            })
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return JsonSerializer.Serialize(compactPayload, JsonOptions);
    }

    private static string NormalizePublicBaseUrl(string publicBaseUrl)
    {
        return string.IsNullOrWhiteSpace(publicBaseUrl)
            ? "https://radishx.com"
            : publicBaseUrl.Trim().TrimEnd('/');
    }

    private static string CombineUrl(string publicBaseUrl, string path)
    {
        return $"{publicBaseUrl}{(path.StartsWith('/') ? path : $"/{path}")}";
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

    private static string FormatDateTime(DateTime value)
    {
        return value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static string BuildCacheKey(string kind, string key, string publicBaseUrl)
    {
        return $"public-head:{kind}:{BuildHash(key)}:{BuildHash(publicBaseUrl)}";
    }

    private static string BuildHash(string value)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hashBytes[..8]).ToLowerInvariant();
    }
}
