using AutoMapper;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>分类服务实现</summary>
public class CategoryService : BaseService<Category, CategoryVo>, ICategoryService
{
    private readonly IBaseRepository<Category> _categoryRepository;

    public CategoryService(IMapper mapper, IBaseRepository<Category> baseRepository)
        : base(mapper, baseRepository)
    {
        _categoryRepository = baseRepository;
    }

    /// <summary>
    /// 获取顶级分类列表
    /// </summary>
    public async Task<List<CategoryVo>> GetTopCategoriesAsync()
    {
        return await QueryAsync(c => c.ParentId == null && c.IsEnabled && !c.IsDeleted);
    }

    /// <summary>
    /// 获取指定分类的子分类
    /// </summary>
    public async Task<List<CategoryVo>> GetChildCategoriesAsync(long parentId)
    {
        return await QueryAsync(c => c.ParentId == parentId && c.IsEnabled && !c.IsDeleted);
    }

    /// <summary>
    /// 更新分类的帖子数量
    /// </summary>
    public async Task UpdatePostCountAsync(long categoryId, int increment)
    {
        var category = await _categoryRepository.QueryByIdAsync(categoryId);
        if (category != null)
        {
            category.PostCount = Math.Max(0, category.PostCount + increment);
            await _categoryRepository.UpdateAsync(category);
        }
    }
}
