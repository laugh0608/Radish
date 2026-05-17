namespace Radish.Gateway.PublicHead;

/// <summary>公开详情页 HTML head 快照注入中间件扩展。</summary>
public static class PublicHeadSnapshotMiddlewareExtensions
{
    public static IApplicationBuilder UsePublicHeadSnapshotHtml(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PublicHeadSnapshotMiddleware>();
    }
}
