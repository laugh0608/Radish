using Radish.Model;

namespace Radish.Service;

/// <summary>
/// 固定文档启动同步前置条件守卫
/// </summary>
public static class WikiBuiltInSyncStartupGuard
{
    public static WikiBuiltInSyncSkipReason? GetSkipReason(
        bool showBuiltInDocs,
        bool hasWikiDocumentTable,
        bool hasWikiDocumentRevisionTable)
    {
        if (!showBuiltInDocs)
        {
            return WikiBuiltInSyncSkipReason.Disabled;
        }

        if (!hasWikiDocumentTable && !hasWikiDocumentRevisionTable)
        {
            return WikiBuiltInSyncSkipReason.BothTablesMissing;
        }

        if (!hasWikiDocumentTable)
        {
            return WikiBuiltInSyncSkipReason.DocumentTableMissing;
        }

        if (!hasWikiDocumentRevisionTable)
        {
            return WikiBuiltInSyncSkipReason.RevisionTableMissing;
        }

        return null;
    }
}
