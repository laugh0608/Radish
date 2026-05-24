namespace Radish.IService;

/// <summary>公开 sitemap XML 生成服务。</summary>
public interface IPublicSitemapService
{
    /// <summary>生成 sitemap index XML。</summary>
    Task<string> GetIndexXmlAsync(string publicBaseUrl);

    /// <summary>生成指定分片 sitemap XML。</summary>
    Task<string> GetSectionXmlAsync(string section, int pageIndex, string publicBaseUrl);
}
