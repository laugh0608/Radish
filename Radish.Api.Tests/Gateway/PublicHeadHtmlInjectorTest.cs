using Radish.Gateway.PublicHead;
using Xunit;

namespace Radish.Api.Tests.Gateway;

public class PublicHeadHtmlInjectorTest
{
    [Fact]
    public void Inject_Should_Replace_Default_Head_Tags_And_Add_JsonLd()
    {
        const string html = """
            <!doctype html>
            <html>
              <head>
                <title>Radish</title>
                <meta name="description" content="default" />
                <meta property="og:title" content="default" />
              </head>
              <body><div id="root"></div></body>
            </html>
            """;
        var snapshot = new PublicHeadSnapshot
        {
            VoTitle = "详情 <标题>",
            VoDescription = "详情描述",
            VoCanonicalUrl = "https://example.test/forum/post/post-alpha",
            VoOpenGraphType = "article",
            VoImageUrl = "https://example.test/image.png",
            VoJsonLd = """{"@context":"https://schema.org","name":"详情"}"""
        };

        var injectedHtml = PublicHeadHtmlInjector.Inject(html, snapshot);

        Assert.Contains("<title>详情 &lt;标题&gt;</title>", injectedHtml);
        Assert.Contains("content=\"详情描述\"", injectedHtml);
        Assert.Contains("rel=\"canonical\" href=\"https://example.test/forum/post/post-alpha\"", injectedHtml);
        Assert.Contains("property=\"og:image\"", injectedHtml);
        Assert.Contains("id=\"radish-public-jsonld\"", injectedHtml);
        Assert.DoesNotContain("content=\"default\"", injectedHtml);
    }

    [Fact]
    public void Inject_Should_Escape_JsonLd_Script_Close()
    {
        const string html = "<html><head><title>Radish</title></head><body></body></html>";
        var snapshot = new PublicHeadSnapshot
        {
            VoTitle = "标题",
            VoDescription = "描述",
            VoCanonicalUrl = "https://example.test/docs/guide",
            VoOpenGraphType = "article",
            VoJsonLd = """{"name":"</script><script>alert(1)</script>"}"""
        };

        var injectedHtml = PublicHeadHtmlInjector.Inject(html, snapshot);

        Assert.Contains("<\\/script><script>alert(1)<\\/script>", injectedHtml);
        Assert.DoesNotContain("</script><script>alert(1)</script>", injectedHtml);
    }
}
