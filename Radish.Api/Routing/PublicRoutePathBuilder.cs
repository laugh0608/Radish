namespace Radish.Api.Routing;

internal static class PublicRoutePathBuilder
{
    public static string BuildForumPostPath(long postId)
    {
        return $"/forum/post/{postId}";
    }

    public static string BuildShopProductPath(long productId)
    {
        return $"/shop/product/{productId}";
    }

    public static string BuildDocsPath(string? slug, long? documentId = null)
    {
        var normalizedSlug = slug?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSlug))
        {
            return $"/docs/{Uri.EscapeDataString(normalizedSlug)}";
        }

        return documentId is > 0
            ? $"/wiki/doc/{documentId.Value}"
            : "/docs";
    }
}
