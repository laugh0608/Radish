namespace Radish.Service;

/// <summary>
/// 固定文档启动同步前置条件守卫
/// </summary>
public static class WikiBuiltInSyncStartupGuard
{
    private const string DbMigrateHint = "请先执行 DbMigrate apply。";

    public static string? GetSkipReason(
        bool showBuiltInDocs,
        bool hasWikiDocumentTable,
        bool hasWikiDocumentRevisionTable)
    {
        if (!showBuiltInDocs)
        {
            return "Document.ShowBuiltInDocs=false";
        }

        if (!hasWikiDocumentTable && !hasWikiDocumentRevisionTable)
        {
            return $"WikiDocument / WikiDocumentRevision 表未初始化，已跳过固定文档启动同步；{DbMigrateHint}";
        }

        if (!hasWikiDocumentTable)
        {
            return $"WikiDocument 表未初始化，已跳过固定文档启动同步；{DbMigrateHint}";
        }

        if (!hasWikiDocumentRevisionTable)
        {
            return $"WikiDocumentRevision 表未初始化，已跳过固定文档启动同步；{DbMigrateHint}";
        }

        return null;
    }
}
