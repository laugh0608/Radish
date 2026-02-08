using Radish.IService.Base;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.IService;

/// <summary>商品服务接口</summary>
public interface IProductService : IBaseService<Product, ProductVo>
{
    #region 商品分类

    /// <summary>获取所有启用的商品分类</summary>
    /// <returns>分类列表</returns>
    Task<List<ProductCategoryVo>> GetCategoriesAsync();

    /// <summary>获取分类详情</summary>
    /// <param name="categoryId">分类 ID</param>
    /// <returns>分类视图模型</returns>
    Task<ProductCategoryVo?> GetCategoryAsync(string categoryId);

    #endregion

    #region 商品查询

    /// <summary>获取商品列表（前台展示）</summary>
    /// <param name="categoryId">分类 ID（可选）</param>
    /// <param name="productType">商品类型（可选）</param>
    /// <param name="keyword">搜索关键词（可选）</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>分页商品列表</returns>
    Task<PageModel<ProductListItemVo>> GetProductListAsync(
        string? categoryId = null,
        ProductType? productType = null,
        string? keyword = null,
        int pageIndex = 1,
        int pageSize = 20);

    /// <summary>获取商品详情</summary>
    /// <param name="productId">商品 ID</param>
    /// <returns>商品视图模型</returns>
    Task<ProductVo?> GetProductDetailAsync(long productId);

    /// <summary>检查用户是否可以购买商品</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="productId">商品 ID</param>
    /// <param name="quantity">购买数量</param>
    /// <returns>是否可购买及原因</returns>
    Task<(bool canBuy, string? reason)> CheckCanBuyAsync(long userId, long productId, int quantity = 1);

    #endregion

    #region 库存管理

    /// <summary>扣减库存</summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="quantity">扣减数量</param>
    /// <returns>是否成功</returns>
    Task<bool> DeductStockAsync(long productId, int quantity);

    /// <summary>恢复库存</summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="quantity">恢复数量</param>
    /// <returns>是否成功</returns>
    Task<bool> RestoreStockAsync(long productId, int quantity);

    /// <summary>增加已售数量</summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="quantity">增加数量</param>
    /// <returns>是否成功</returns>
    Task<bool> IncreaseSoldCountAsync(long productId, int quantity);

    #endregion

    #region 管理员操作

    /// <summary>创建商品</summary>
    /// <param name="dto">创建商品 DTO</param>
    /// <param name="operatorId">操作员 ID</param>
    /// <param name="operatorName">操作员名称</param>
    /// <returns>商品 ID</returns>
    Task<long> CreateProductAsync(CreateProductDto dto, long operatorId, string operatorName);

    /// <summary>更新商品</summary>
    /// <param name="dto">更新商品 DTO</param>
    /// <param name="operatorId">操作员 ID</param>
    /// <param name="operatorName">操作员名称</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateProductAsync(UpdateProductDto dto, long operatorId, string operatorName);

    /// <summary>上架商品</summary>
    /// <param name="productId">商品 ID</param>
    /// <returns>是否成功</returns>
    Task<bool> PutOnSaleAsync(long productId);

    /// <summary>下架商品</summary>
    /// <param name="productId">商品 ID</param>
    /// <returns>是否成功</returns>
    Task<bool> TakeOffSaleAsync(long productId);

    /// <summary>获取商品列表（管理后台）</summary>
    /// <param name="categoryId">分类 ID（可选）</param>
    /// <param name="productType">商品类型（可选）</param>
    /// <param name="isOnSale">是否上架（可选）</param>
    /// <param name="keyword">搜索关键词（可选）</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>分页商品列表</returns>
    Task<PageModel<ProductVo>> GetProductListForAdminAsync(
        string? categoryId = null,
        ProductType? productType = null,
        bool? isOnSale = null,
        string? keyword = null,
        int pageIndex = 1,
        int pageSize = 20);

    #endregion
}
