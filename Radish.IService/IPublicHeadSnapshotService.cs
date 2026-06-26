using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>公开页面 HTML head 快照服务。</summary>
public interface IPublicHeadSnapshotService
{
    /// <summary>获取公开静态路由 head 快照。</summary>
    Task<PublicHeadSnapshotVo?> GetStaticRouteSnapshotAsync(string routeKey, string publicBaseUrl);

    /// <summary>获取论坛帖子公开 head 快照。</summary>
    Task<PublicHeadSnapshotVo?> GetForumPostSnapshotAsync(string postKey, string publicBaseUrl);

    /// <summary>获取公开文档 head 快照。</summary>
    Task<PublicHeadSnapshotVo?> GetDocsSnapshotAsync(string slug, string publicBaseUrl);

    /// <summary>获取商城商品公开 head 快照。</summary>
    Task<PublicHeadSnapshotVo?> GetShopProductSnapshotAsync(long productId, string publicBaseUrl);
}
