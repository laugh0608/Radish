using System.Linq.Expressions;
using AutoMapper;
using Radish.Common;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>商品服务实现</summary>
public class ProductService : BaseService<Product, ProductVo>, IProductService
{
    private readonly IBaseRepository<Product> _productRepository;
    private readonly IBaseRepository<ProductCategory> _categoryRepository;
    private readonly IBaseRepository<Order> _orderRepository;

    /// <summary>乐观锁冲突重试次数</summary>
    private const int MaxRetryCount = 5;

    /// <summary>重试基础延迟（毫秒）</summary>
    private const int BaseRetryDelayMs = 50;

    public ProductService(
        IMapper mapper,
        IBaseRepository<Product> productRepository,
        IBaseRepository<ProductCategory> categoryRepository,
        IBaseRepository<Order> orderRepository)
        : base(mapper, productRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _orderRepository = orderRepository;
    }

    #region 商品分类

    /// <summary>获取所有启用的商品分类</summary>
    public async Task<List<ProductCategoryVo>> GetCategoriesAsync()
    {
        try
        {
            var categories = await _categoryRepository.QueryAsync(c => c.IsEnabled);
            var categoryVos = Mapper.Map<List<ProductCategoryVo>>(categories.OrderBy(c => c.SortOrder).ToList());

            // 统计每个分类下的商品数量
            foreach (var category in categoryVos)
            {
                category.VoProductCount = await _productRepository.QueryCountAsync(
                    p => p.CategoryId == category.VoId && p.IsEnabled && p.IsOnSale);
            }

            return categoryVos;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取商品分类列表失败");
            throw;
        }
    }

    /// <summary>获取分类详情</summary>
    public async Task<ProductCategoryVo?> GetCategoryAsync(string categoryId)
    {
        try
        {
            var category = await _categoryRepository.QueryFirstAsync(c => c.Id == categoryId);
            if (category == null) return null;

            var vo = Mapper.Map<ProductCategoryVo>(category);
            vo.VoProductCount = await _productRepository.QueryCountAsync(
                p => p.CategoryId == categoryId && p.IsEnabled && p.IsOnSale);

            return vo;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取分类 {CategoryId} 详情失败", categoryId);
            throw;
        }
    }

    #endregion

    #region 商品查询

    /// <summary>获取商品列表（前台展示）</summary>
    public async Task<PageModel<ProductListItemVo>> GetProductListAsync(
        string? categoryId = null,
        ProductType? productType = null,
        string? keyword = null,
        int pageIndex = 1,
        int pageSize = 20)
    {
        try
        {
            // 构建查询条件
            Expression<Func<Product, bool>> where = p => p.IsEnabled && p.IsOnSale;

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                where = where.And(p => p.CategoryId == categoryId);
            }

            if (productType.HasValue)
            {
                where = where.And(p => p.ProductType == productType.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                where = where.And(p => p.Name.Contains(keyword) || (p.Description != null && p.Description.Contains(keyword)));
            }

            var (products, totalCount) = await _productRepository.QueryPageAsync(
                whereExpression: where,
                pageIndex: pageIndex,
                pageSize: pageSize,
                orderByExpression: p => p.SortOrder,
                orderByType: OrderByType.Asc);

            var productVos = Mapper.Map<List<ProductListItemVo>>(products);

            return new PageModel<ProductListItemVo>
            {
                Page = pageIndex,
                PageSize = pageSize,
                DataCount = totalCount,
                PageCount = (int)Math.Ceiling((double)totalCount / pageSize),
                Data = productVos
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取商品列表失败");
            throw;
        }
    }

    /// <summary>获取商品详情</summary>
    public async Task<ProductVo?> GetProductDetailAsync(long productId)
    {
        try
        {
            var product = await _productRepository.QueryFirstAsync(p => p.Id == productId && p.IsEnabled);
            if (product == null) return null;

            var vo = Mapper.Map<ProductVo>(product);

            // 填充分类名称
            var category = await _categoryRepository.QueryFirstAsync(c => c.Id == product.CategoryId);
            vo.VoCategoryName = category?.Name;

            return vo;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取商品 {ProductId} 详情失败", productId);
            throw;
        }
    }

    /// <summary>检查用户是否可以购买商品</summary>
    public async Task<(bool canBuy, string? reason)> CheckCanBuyAsync(long userId, long productId, int quantity = 1)
    {
        try
        {
            var product = await _productRepository.QueryFirstAsync(p => p.Id == productId);

            if (product == null)
            {
                return (false, "商品不存在");
            }

            if (!product.IsEnabled)
            {
                return (false, "商品已下架");
            }

            if (!product.IsOnSale)
            {
                return (false, "商品未上架");
            }

            // 检查库存
            if (product.StockType == StockType.Limited && product.Stock < quantity)
            {
                return (false, "库存不足");
            }

            // 检查限购
            if (product.LimitPerUser > 0)
            {
                var purchasedCount = await GetUserPurchaseCountAsync(userId, productId);
                if (purchasedCount + quantity > product.LimitPerUser)
                {
                    return (false, $"该商品每人限购 {product.LimitPerUser} 件，您已购买 {purchasedCount} 件");
                }
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检查用户 {UserId} 是否可购买商品 {ProductId} 失败", userId, productId);
            throw;
        }
    }

    /// <summary>获取用户购买某商品的数量</summary>
    private async Task<int> GetUserPurchaseCountAsync(long userId, long productId)
    {
        // 只统计已完成的订单
        var count = await _orderRepository.QueryCountAsync(
            o => o.UserId == userId &&
                 o.ProductId == productId &&
                 (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid));
        return count;
    }

    #endregion

    #region 库存管理

    /// <summary>扣减库存</summary>
    public async Task<bool> DeductStockAsync(long productId, int quantity)
    {
        try
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var product = await _productRepository.QueryFirstAsync(p => p.Id == productId);
                if (product == null)
                {
                    throw new InvalidOperationException("商品不存在");
                }

                if (product.StockType == StockType.Unlimited)
                {
                    return true; // 无限库存不需要扣减
                }

                if (product.Stock < quantity)
                {
                    throw new InvalidOperationException("库存不足");
                }

                var currentVersion = product.Version;
                product.Stock -= quantity;
                product.Version++;
                product.ModifyTime = DateTime.Now;

                var affected = await _productRepository.UpdateColumnsAsync(
                    p => new Product
                    {
                        Stock = product.Stock,
                        Version = product.Version,
                        ModifyTime = product.ModifyTime
                    },
                    p => p.Id == productId && p.Version == currentVersion);

                if (affected == 0)
                {
                    throw new InvalidOperationException("乐观锁冲突，请重试");
                }

                return true;
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "扣减商品 {ProductId} 库存失败", productId);
            throw;
        }
    }

    /// <summary>恢复库存</summary>
    public async Task<bool> RestoreStockAsync(long productId, int quantity)
    {
        try
        {
            var product = await _productRepository.QueryFirstAsync(p => p.Id == productId);
            if (product == null || product.StockType == StockType.Unlimited)
            {
                return true;
            }

            var affected = await _productRepository.UpdateColumnsAsync(
                p => new Product
                {
                    Stock = p.Stock + quantity,
                    ModifyTime = DateTime.Now
                },
                p => p.Id == productId);

            return affected > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "恢复商品 {ProductId} 库存失败", productId);
            throw;
        }
    }

    /// <summary>增加已售数量</summary>
    public async Task<bool> IncreaseSoldCountAsync(long productId, int quantity)
    {
        try
        {
            var affected = await _productRepository.UpdateColumnsAsync(
                p => new Product
                {
                    SoldCount = p.SoldCount + quantity,
                    ModifyTime = DateTime.Now
                },
                p => p.Id == productId);

            return affected > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "增加商品 {ProductId} 已售数量失败", productId);
            throw;
        }
    }

    #endregion

    #region 管理员操作

    /// <summary>创建商品</summary>
    public async Task<long> CreateProductAsync(CreateProductDto dto, long operatorId, string operatorName)
    {
        try
        {
            var product = Mapper.Map<Product>(dto);
            product.CreateId = operatorId;
            product.CreateBy = operatorName;
            product.CreateTime = DateTime.Now;

            if (dto.IsOnSale)
            {
                product.OnSaleTime = DateTime.Now;
            }

            var productId = await _productRepository.AddAsync(product);
            Log.Information("创建商品成功：{ProductId}, 名称={Name}, 操作员={Operator}",
                productId, dto.Name, operatorName);

            return productId;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "创建商品失败：{Name}", dto.Name);
            throw;
        }
    }

    /// <summary>更新商品</summary>
    public async Task<bool> UpdateProductAsync(UpdateProductDto dto, long operatorId, string operatorName)
    {
        try
        {
            var product = await _productRepository.QueryFirstAsync(p => p.Id == dto.Id);
            if (product == null)
            {
                throw new InvalidOperationException("商品不存在");
            }

            Mapper.Map(dto, product);
            product.ModifyId = operatorId;
            product.ModifyBy = operatorName;
            product.ModifyTime = DateTime.Now;

            var result = await _productRepository.UpdateAsync(product);
            Log.Information("更新商品成功：{ProductId}, 操作员={Operator}", dto.Id, operatorName);

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新商品失败：{ProductId}", dto.Id);
            throw;
        }
    }

    /// <summary>上架商品</summary>
    public async Task<bool> PutOnSaleAsync(long productId)
    {
        try
        {
            var affected = await _productRepository.UpdateColumnsAsync(
                p => new Product
                {
                    IsOnSale = true,
                    OnSaleTime = DateTime.Now,
                    ModifyTime = DateTime.Now
                },
                p => p.Id == productId);

            if (affected > 0)
            {
                Log.Information("商品 {ProductId} 上架成功", productId);
            }

            return affected > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "上架商品 {ProductId} 失败", productId);
            throw;
        }
    }

    /// <summary>下架商品</summary>
    public async Task<bool> TakeOffSaleAsync(long productId)
    {
        try
        {
            var affected = await _productRepository.UpdateColumnsAsync(
                p => new Product
                {
                    IsOnSale = false,
                    OffSaleTime = DateTime.Now,
                    ModifyTime = DateTime.Now
                },
                p => p.Id == productId);

            if (affected > 0)
            {
                Log.Information("商品 {ProductId} 下架成功", productId);
            }

            return affected > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "下架商品 {ProductId} 失败", productId);
            throw;
        }
    }

    /// <summary>获取商品列表（管理后台）</summary>
    public async Task<PageModel<ProductVo>> GetProductListForAdminAsync(
        string? categoryId = null,
        ProductType? productType = null,
        bool? isOnSale = null,
        string? keyword = null,
        int pageIndex = 1,
        int pageSize = 20)
    {
        try
        {
            Expression<Func<Product, bool>> where = p => true;

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                where = where.And(p => p.CategoryId == categoryId);
            }

            if (productType.HasValue)
            {
                where = where.And(p => p.ProductType == productType.Value);
            }

            if (isOnSale.HasValue)
            {
                where = where.And(p => p.IsOnSale == isOnSale.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                where = where.And(p => p.Name.Contains(keyword));
            }

            var (products, totalCount) = await _productRepository.QueryPageAsync(
                whereExpression: where,
                pageIndex: pageIndex,
                pageSize: pageSize,
                orderByExpression: p => p.CreateTime,
                orderByType: OrderByType.Desc);

            var productVos = Mapper.Map<List<ProductVo>>(products);

            // 填充分类名称
            var categoryIds = products.Select(p => p.CategoryId).Distinct().ToList();
            var categories = await _categoryRepository.QueryAsync(c => categoryIds.Contains(c.Id));
            var categoryDict = categories.ToDictionary(c => c.Id, c => c.Name);

            foreach (var vo in productVos)
            {
                if (categoryDict.TryGetValue(vo.VoCategoryId, out var categoryName))
                {
                    vo.VoCategoryName = categoryName;
                }
            }

            return new PageModel<ProductVo>
            {
                Page = pageIndex,
                PageSize = pageSize,
                DataCount = totalCount,
                PageCount = (int)Math.Ceiling((double)totalCount / pageSize),
                Data = productVos
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取商品列表（管理后台）失败");
            throw;
        }
    }

    #endregion

    #region 私有方法

    /// <summary>带重试的执行方法</summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
    {
        for (int i = 0; i < MaxRetryCount; i++)
        {
            try
            {
                return await action();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("乐观锁冲突"))
            {
                if (i == MaxRetryCount - 1)
                {
                    Log.Warning("乐观锁冲突重试次数已达上限");
                    throw;
                }

                var delay = BaseRetryDelayMs * (int)Math.Pow(2, i);
                Log.Debug("乐观锁冲突，第 {RetryCount} 次重试，延迟 {Delay}ms", i + 1, delay);
                await Task.Delay(delay);
            }
        }

        throw new InvalidOperationException("重试次数已达上限");
    }

    #endregion
}
