using Radish.IService.Base;
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
}
