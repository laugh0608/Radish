using Radish.Extension.Log;
using Radish.Common.OptionTool;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using SqlSugar;

namespace Radish.Api.Services;

/// <summary>启动同步的既有消费边界；失败后继续启动，不承诺此前写入已回滚。</summary>
public static class WikiBuiltInDocumentStartupSync
{
    public static async Task RunAsync(IServiceProvider services, DocumentOptions options, Serilog.ILogger logger)
    {
        try
        {
            using var scope = services.CreateScope();
            var sqlSugarClient = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            var documentTable = sqlSugarClient.EntityMaintenance.GetEntityInfo<WikiDocument>().DbTableName;
            var revisionTable = sqlSugarClient.EntityMaintenance.GetEntityInfo<WikiDocumentRevision>().DbTableName;
            // 两次既有查询仍在守卫调用前执行；不因展示开关改变数据库访问顺序。
            var reason = WikiBuiltInSyncStartupGuard.GetSkipReason(options.ShowBuiltInDocs,
                sqlSugarClient.DbMaintenance.IsAnyTable(documentTable, false),
                sqlSugarClient.DbMaintenance.IsAnyTable(revisionTable, false));
            if (reason != null)
            {
                HostProgramLog.BuiltInSyncSkipped(logger, reason.Value);
            }
            else
            {
                var service = scope.ServiceProvider.GetRequiredService<IWikiDocumentService>();
                var summary = await service.SyncBuiltInDocumentsAsync();
                if (summary.IsSkipped) HostProgramLog.BuiltInSyncSkipped(logger, summary.SkipReason);
                else HostProgramLog.BuiltInSyncCompleted(logger, summary);
            }
        }
        catch (Exception exception)
        {
            HostProgramLog.BuiltInSyncFailed(logger, exception);
        }
    }
}
