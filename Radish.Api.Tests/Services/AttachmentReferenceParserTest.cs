using System.Linq;
using Radish.Shared;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class AttachmentReferenceParserTest
{
    [Fact]
    public void ExtractAttachmentIds_ShouldIgnoreMarkdownCodeExamples()
    {
        const string markdown = """
            ![current](attachment://101)

            `[inline](attachment://102)`

            ```markdown
            ![fenced](attachment://103)
            ```

            ~~~text
            [second-fence](attachment://104)
            ~~~

            [download](attachment://105)
            """;

        var attachmentIds = AttachmentReferenceParser.ExtractAttachmentIds(markdown);

        Assert.Equal([101L, 105L], attachmentIds.Order());
    }

    [Fact]
    public void ExtractLegacyAttachmentUrls_ShouldReportOnlyRenderableUserUploadLinks()
    {
        const string markdown = """
            ![legacy](/uploads/Wiki/old.png)
            [absolute](https://old.example.test/uploads/Document/guide.pdf)
            ![trusted](/uploads/DefaultIco/bailuobo.ico)
            `[example](/uploads/Wiki/example.png)`
            """;

        var legacyUrls = AttachmentReferenceParser.ExtractLegacyAttachmentUrls(markdown);

        Assert.Equal(
            [
                "/uploads/Wiki/old.png",
                "https://old.example.test/uploads/Document/guide.pdf"
            ],
            legacyUrls.Order());
    }
}
