using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>分类服务接口</summary>
public interface ICategoryService : IBaseService<Category, CategoryVo>
{
    /// <summary>
    /// 获取顶级分类列表
    /// </summary>
    /// <returns>顶级分类列表</returns>
    Task<List<CategoryVo>> GetTopCategoriesAsync();

    /// <summary>
    /// 获取指定分类的子分类
    /// </summary>
    /// <param name="parentId">父分类 Id</param>
    /// <returns>子分类列表</returns>
    Task<List<CategoryVo>> GetChildCategoriesAsync(long parentId);

    /// <summary>
    /// 更新分类的帖子数量
    /// </summary>
    /// <param name="categoryId">分类 Id</param>
    /// <param name="increment">增量（可为负数）</param>
    Task UpdatePostCountAsync(long categoryId, int increment);
}
