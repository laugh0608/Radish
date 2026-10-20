#nullable enable

using Radish.Service;
using Radish.Model;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

public class WikiBuiltInSyncStartupGuardTests
{
    [Fact(DisplayName = "关闭固定文档时应按配置跳过启动同步")]
    public void GetSkipReason_ShouldReturnConfigReason_WhenBuiltInDocsDisabled()
    {
        var reason = WikiBuiltInSyncStartupGuard.GetSkipReason(
            showBuiltInDocs: false,
            hasWikiDocumentTable: false,
            hasWikiDocumentRevisionTable: false);

        reason.ShouldBe(WikiBuiltInSyncSkipReason.Disabled);
    }

    [Theory(DisplayName = "缺少 Wiki 表时应返回明确的缺表原因")]
    [InlineData(false, false, WikiBuiltInSyncSkipReason.BothTablesMissing)]
    [InlineData(false, true, WikiBuiltInSyncSkipReason.DocumentTableMissing)]
    [InlineData(true, false, WikiBuiltInSyncSkipReason.RevisionTableMissing)]
    public void GetSkipReason_ShouldIdentifyMissingTables(
        bool hasWikiDocumentTable,
        bool hasWikiDocumentRevisionTable,
        WikiBuiltInSyncSkipReason expectedReason)
    {
        var reason = WikiBuiltInSyncStartupGuard.GetSkipReason(
            showBuiltInDocs: true,
            hasWikiDocumentTable: hasWikiDocumentTable,
            hasWikiDocumentRevisionTable: hasWikiDocumentRevisionTable);

        reason.ShouldBe(expectedReason);
    }

    [Fact(DisplayName = "Wiki 表已就绪时不应跳过启动同步")]
    public void GetSkipReason_ShouldReturnNull_WhenWikiTablesReady()
    {
        var reason = WikiBuiltInSyncStartupGuard.GetSkipReason(
            showBuiltInDocs: true,
            hasWikiDocumentTable: true,
            hasWikiDocumentRevisionTable: true);

        reason.ShouldBeNull();
    }
}
