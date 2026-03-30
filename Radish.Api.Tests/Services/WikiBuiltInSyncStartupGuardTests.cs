#nullable enable

using Radish.Service;
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

        reason.ShouldBe("Document.ShowBuiltInDocs=false");
    }

    [Theory(DisplayName = "缺少 Wiki 表时应提示先执行 DbMigrate")]
    [InlineData(false, false, "WikiDocument / WikiDocumentRevision 表未初始化，已跳过固定文档启动同步；请先执行 DbMigrate apply。")]
    [InlineData(false, true, "WikiDocument 表未初始化，已跳过固定文档启动同步；请先执行 DbMigrate apply。")]
    [InlineData(true, false, "WikiDocumentRevision 表未初始化，已跳过固定文档启动同步；请先执行 DbMigrate apply。")]
    public void GetSkipReason_ShouldReturnDbMigrateHint_WhenWikiTablesMissing(
        bool hasWikiDocumentTable,
        bool hasWikiDocumentRevisionTable,
        string expectedReason)
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
