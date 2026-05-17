using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace Radish.Gateway.PublicHead;

/// <summary>公开详情页 HTML head 注入器。</summary>
public static class PublicHeadHtmlInjector
{
    private static readonly HtmlEncoder HtmlEncoder = HtmlEncoder.Create(UnicodeRanges.All);
    private static readonly Regex HeadClosePattern = new("</head>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TitlePattern = new(@"<title\b[^>]*>.*?</title>\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex ManagedMetaPattern = new(@"<meta\s+[^>]*(?:name|property)=[""'](?:description|og:site_name|og:title|og:description|og:type|og:url|og:image|twitter:card|twitter:title|twitter:description|twitter:image)[""'][^>]*>\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalPattern = new(@"<link\s+[^>]*rel=[""']canonical[""'][^>]*>\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex JsonLdPattern = new(@"<script\s+[^>]*id=[""']radish-public-jsonld[""'][^>]*>.*?</script>\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public static string Inject(string html, PublicHeadSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(html) || !HeadClosePattern.IsMatch(html))
        {
            return html;
        }

        var cleanedHtml = JsonLdPattern.Replace(html, string.Empty);
        cleanedHtml = TitlePattern.Replace(cleanedHtml, string.Empty, 1);
        cleanedHtml = ManagedMetaPattern.Replace(cleanedHtml, string.Empty);
        cleanedHtml = CanonicalPattern.Replace(cleanedHtml, string.Empty);

        return HeadClosePattern.Replace(cleanedHtml, BuildHeadTags(snapshot), 1);
    }

    private static string BuildHeadTags(PublicHeadSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"  <title>{EncodeText(snapshot.VoTitle)}</title>");
        AppendMetaName(builder, "description", snapshot.VoDescription);
        AppendLink(builder, "canonical", snapshot.VoCanonicalUrl);
        AppendMetaProperty(builder, "og:site_name", "Radish");
        AppendMetaProperty(builder, "og:title", snapshot.VoTitle);
        AppendMetaProperty(builder, "og:description", snapshot.VoDescription);
        AppendMetaProperty(builder, "og:type", snapshot.VoOpenGraphType);
        AppendMetaProperty(builder, "og:url", snapshot.VoCanonicalUrl);
        AppendMetaName(builder, "twitter:card", string.IsNullOrWhiteSpace(snapshot.VoImageUrl) ? "summary" : "summary_large_image");
        AppendMetaName(builder, "twitter:title", snapshot.VoTitle);
        AppendMetaName(builder, "twitter:description", snapshot.VoDescription);

        if (!string.IsNullOrWhiteSpace(snapshot.VoImageUrl))
        {
            AppendMetaProperty(builder, "og:image", snapshot.VoImageUrl);
            AppendMetaName(builder, "twitter:image", snapshot.VoImageUrl);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.VoJsonLd))
        {
            builder.AppendLine($"  <script type=\"application/ld+json\" id=\"radish-public-jsonld\">{EscapeJsonLd(snapshot.VoJsonLd)}</script>");
        }

        builder.Append("</head>");
        return builder.ToString();
    }

    private static void AppendMetaName(StringBuilder builder, string name, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            builder.AppendLine($"  <meta name=\"{EncodeAttribute(name)}\" content=\"{EncodeAttribute(content)}\" />");
        }
    }

    private static void AppendMetaProperty(StringBuilder builder, string property, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            builder.AppendLine($"  <meta property=\"{EncodeAttribute(property)}\" content=\"{EncodeAttribute(content)}\" />");
        }
    }

    private static void AppendLink(StringBuilder builder, string rel, string? href)
    {
        if (!string.IsNullOrWhiteSpace(href))
        {
            builder.AppendLine($"  <link rel=\"{EncodeAttribute(rel)}\" href=\"{EncodeAttribute(href)}\" />");
        }
    }

    private static string EncodeText(string value)
    {
        return HtmlEncoder.Encode(value);
    }

    private static string EncodeAttribute(string value)
    {
        return HtmlEncoder.Encode(value);
    }

    private static string EscapeJsonLd(string value)
    {
        return value.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
    }
}
