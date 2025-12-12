using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>标签服务接口</summary>
public interface ITagService : IBaseService<Tag, TagVo>
{
    /// <summary>
    /// 根据标签名称获取或创建标签
    /// </summary>
    /// <param name="tagName">标签名称</param>
    /// <returns>标签实体</returns>
    Task<Tag> GetOrCreateTagAsync(string tagName);

    /// <summary>
    /// 更新标签的帖子数量
    /// </summary>
    /// <param name="tagId">标签 Id</param>
    /// <param name="increment">增量（可为负数）</param>
    Task UpdatePostCountAsync(long tagId, int increment);

    /// <summary>
    /// 获取热门标签
    /// </summary>
    /// <param name="topCount">返回数量</param>
    /// <returns>标签列表</returns>
    Task<List<TagVo>> GetHotTagsAsync(int topCount = 20);
}
