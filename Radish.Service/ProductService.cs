using System.Linq.Expressions;
using AutoMapper;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service.Base;
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
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
#pragma warning disable CS0618
    private static readonly Expression<Func<Product, bool>> SupportedPublicProductExpression = p =>
        !(p.ProductType == ProductType.Benefit && (
            p.BenefitType == BenefitType.Badge ||
            p.BenefitType == BenefitType.AvatarFrame ||
            p.BenefitType == BenefitType.Title ||
            p.BenefitType == BenefitType.Theme ||
            p.BenefitType == BenefitType.Signature ||
            p.BenefitType == BenefitType.NameColor ||
            p.BenefitType == BenefitType.LikeEffect)) &&
        !(p.ProductType == ProductType.Consumable && (
            p.ConsumableType == ConsumableType.PostPinCard ||
            p.ConsumableType == ConsumableType.PostHighlightCard ||
            p.ConsumableType == ConsumableType.DoubleExpCard ||
            p.ConsumableType == ConsumableType.LotteryTicket));
    private static readonly Expression<Func<Product, bool>> PublicVisibleProductExpression = p =>
        p.IsEnabled &&
        p.IsOnSale &&
        !p.IsDeleted &&
        !(p.ProductType == ProductType.Benefit && (
            p.BenefitType == BenefitType.Badge ||
            p.BenefitType == BenefitType.AvatarFrame ||
            p.BenefitType == BenefitType.Title ||
            p.BenefitType == BenefitType.Theme ||
            p.BenefitType == BenefitType.Signature ||
            p.BenefitType == BenefitType.NameColor ||
            p.BenefitType == BenefitType.LikeEffect)) &&
        !(p.ProductType == ProductType.Consumable && (
            p.ConsumableType == ConsumableType.PostPinCard ||
            p.ConsumableType == ConsumableType.PostHighlightCard ||
            p.ConsumableType == ConsumableType.DoubleExpCard ||
            p.ConsumableType == ConsumableType.LotteryTicket));
#pragma warning restore CS0618

    /// <summary>乐观锁冲突重试次数</summary>
    private const int MaxRetryCount = 5;

    /// <summary>重试基础延迟（毫秒）</summary>
    private const int BaseRetryDelayMs = 50;

    public ProductService(
        IMapper mapper,
        IBaseRepository<Product> productRepository,
        IBaseRepository<ProductCategory> categoryRepository,
        IBaseRepository<Order> orderRepository,
        IAttachmentUrlResolver attachmentUrlResolver)
        : base(mapper, productRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _orderRepository = orderRepository;
        _attachmentUrlResolver = attachmentUrlResolver;
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
                    PublicVisibleProductExpression.And(p => p.CategoryId == category.VoId));
            }

            FillProductCategoryUrls(categoryVos);
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
                PublicVisibleProductExpression.And(p => p.CategoryId == categoryId));

            FillProductCategoryUrl(vo);
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
            Expression<Func<Product, bool>> where = PublicVisibleProductExpression;

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
            FillProductListItemUrls(productVos);

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
            var product = await _productRepository.QueryFirstAsync(
                SupportedPublicProductExpression.And(p => p.Id == productId && p.IsEnabled && !p.IsDeleted));
            if (product == null) return null;

            var vo = Mapper.Map<ProductVo>(product);

            // 填充分类名称
            var category = await _categoryRepository.QueryFirstAsync(c => c.Id == product.CategoryId);
            vo.VoCategoryName = category?.Name;
            FillProductUrl(vo);

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
            if (quantity < 1)
            {
                return (false, "购买数量必须大于 0");
            }

            var product = await _productRepository.QueryFirstAsync(p => p.Id == productId && !p.IsDeleted);

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

            if (ShopProductAvailabilityPolicy.IsUnavailablePublicProduct(product.ProductType, product.BenefitType, product.ConsumableType))
            {
                return (false, $"{ShopProductAvailabilityPolicy.GetUnavailableProductDisplayName(product.BenefitType, product.ConsumableType)}暂未开放，当前不可购买");
            }

            var productConfigurationError = GetInvalidProductConfigurationMessage(
                product.ProductType,
                product.ConsumableType,
                product.BenefitValue);
            if (productConfigurationError != null)
            {
                Log.Warning("商品 {ProductId} 配置不完整，拒绝购买：{Reason}", productId, productConfigurationError);
                return (false, "商品配置不完整，请联系管理员");
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
                var product = await _productRepository.QueryFirstAsync(p => p.Id == productId && !p.IsDeleted);
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
                    p => p.Id == productId && p.Version == currentVersion && p.TenantId == product.TenantId);

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
    public async Task<bool> RestoreStockAsync(long productId, int quantity, StockType stockType)
    {
        try
        {
            if (stockType == StockType.Unlimited)
            {
                return true;
            }

            var product = await _productRepository.QueryFirstAsync(p => p.Id == productId && !p.IsDeleted);
            if (product == null)
            {
                return false;
            }

            var affected = await _productRepository.UpdateColumnsAsync(
                p => new Product
                {
                    Stock = p.Stock + quantity,
                    ModifyTime = DateTime.Now
                },
                p => p.Id == productId && p.TenantId == product.TenantId);

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
            var product = await _productRepository.QueryFirstAsync(p => p.Id == productId && !p.IsDeleted);
            if (product == null)
            {
                return false;
            }

            var affected = await _productRepository.UpdateColumnsAsync(
                p => new Product
                {
                    SoldCount = p.SoldCount + quantity,
                    ModifyTime = DateTime.Now
                },
                p => p.Id == productId && p.TenantId == product.TenantId);

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
            EnsureSupportedOnSaleProduct(dto.ProductType, dto.BenefitType, dto.ConsumableType, dto.IsOnSale);
            EnsureValidProductConfiguration(dto.ProductType, dto.ConsumableType, dto.BenefitValue);

            var tenantId = NormalizeTenantId(App.CurrentUser.TenantId);
            var product = Mapper.Map<Product>(dto);
            product.TenantId = tenantId;
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
            EnsureSupportedOnSaleProduct(dto.ProductType, dto.BenefitType, dto.ConsumableType, dto.IsOnSale);
            EnsureValidProductConfiguration(dto.ProductType, dto.ConsumableType, dto.BenefitValue);

            var product = await _productRepository.QueryFirstAsync(p => p.Id == dto.Id);
            if (product == null)
            {
                throw new InvalidOperationException("商品不存在");
            }

            EnsureExpectedProductVersion(product, dto.ExpectedVersion);

            Mapper.Map(dto, product);
            product.ModifyId = operatorId;
            product.ModifyBy = operatorName;
            product.ModifyTime = DateTime.Now;
            product.Version += 1;

            var affected = await _productRepository.UpdateColumnsAsync(
                p => new Product
                {
                    Name = product.Name,
                    Description = product.Description,
                    IconAttachmentId = product.IconAttachmentId,
                    CoverAttachmentId = product.CoverAttachmentId,
                    CategoryId = product.CategoryId,
                    ProductType = product.ProductType,
                    BenefitType = product.BenefitType,
                    ConsumableType = product.ConsumableType,
                    BenefitValue = product.BenefitValue,
                    Price = product.Price,
                    OriginalPrice = product.OriginalPrice,
                    StockType = product.StockType,
                    Stock = product.Stock,
                    LimitPerUser = product.LimitPerUser,
                    DurationType = product.DurationType,
                    DurationDays = product.DurationDays,
                    ExpiresAt = product.ExpiresAt,
                    SortOrder = product.SortOrder,
                    IsOnSale = product.IsOnSale,
                    Version = product.Version,
                    ModifyId = product.ModifyId,
                    ModifyBy = product.ModifyBy,
                    ModifyTime = product.ModifyTime
                },
                p => p.Id == dto.Id && p.TenantId == product.TenantId && p.Version == dto.ExpectedVersion && !p.IsDeleted);
            if (affected <= 0)
            {
                throw new InvalidOperationException("商品信息已被其他管理员修改，请刷新后重试");
            }

            Log.Information("更新商品成功：{ProductId}, 操作员={Operator}", dto.Id, operatorName);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新商品失败：{ProductId}", dto.Id);
            throw;
        }
    }

    /// <summary>上架商品</summary>
    public async Task<bool> PutOnSaleAsync(long productId, int expectedVersion)
    {
        try
        {
            var product = await _productRepository.QueryFirstAsync(p => p.Id == productId && !p.IsDeleted);
            if (product == null)
            {
                return false;
            }

            if (ShopProductAvailabilityPolicy.IsUnavailablePublicProduct(product.ProductType, product.BenefitType, product.ConsumableType))
            {
                throw new InvalidOperationException($"{ShopProductAvailabilityPolicy.GetUnavailableProductDisplayName(product.BenefitType, product.ConsumableType)}暂未开放，不能上架销售");
            }

            EnsureValidProductConfiguration(product.ProductType, product.ConsumableType, product.BenefitValue);
            EnsureExpectedProductVersion(product, expectedVersion);

            var affected = await _productRepository.UpdateColumnsAsync(
                p => new Product
                {
                    IsOnSale = true,
                    OnSaleTime = DateTime.Now,
                    Version = p.Version + 1,
                    ModifyTime = DateTime.Now
                },
                p => p.Id == productId && p.TenantId == product.TenantId && p.Version == expectedVersion && !p.IsDeleted);
            if (affected <= 0)
            {
                throw new InvalidOperationException("商品信息已被其他管理员修改，请刷新后重试");
            }

            Log.Information("商品 {ProductId} 上架成功", productId);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "上架商品 {ProductId} 失败", productId);
            throw;
        }
    }

    private static void EnsureSupportedOnSaleProduct(
        ProductType productType,
        BenefitType? benefitType,
        ConsumableType? consumableType,
        bool isOnSale)
    {
        if (!isOnSale || !ShopProductAvailabilityPolicy.IsUnavailablePublicProduct(productType, benefitType, consumableType))
        {
            return;
        }

        throw new InvalidOperationException($"{ShopProductAvailabilityPolicy.GetUnavailableProductDisplayName(benefitType, consumableType)}暂未开放，不能上架销售");
    }

    private static void EnsureValidProductConfiguration(
        ProductType productType,
        ConsumableType? consumableType,
        string? benefitValue)
    {
        var errorMessage = GetInvalidProductConfigurationMessage(productType, consumableType, benefitValue);
        if (errorMessage == null)
        {
            return;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static string? GetInvalidProductConfigurationMessage(
        ProductType productType,
        ConsumableType? consumableType,
        string? benefitValue)
    {
        if (productType != ProductType.Consumable)
        {
            return null;
        }

        return consumableType switch
        {
            ConsumableType.ExpCard when !IsPositiveIntegerConfigValue(benefitValue) => "经验卡必须配置正整数经验值",
            ConsumableType.CoinCard when !IsPositiveIntegerConfigValue(benefitValue) => "萝卜币红包必须配置正整数胡萝卜数量",
            _ => null
        };
    }

    private static bool IsPositiveIntegerConfigValue(string? benefitValue)
    {
        return !string.IsNullOrWhiteSpace(benefitValue)
            && int.TryParse(benefitValue.Trim(), out var parsedValue)
            && parsedValue > 0;
    }

    private static void EnsureExpectedProductVersion(Product product, int expectedVersion)
    {
        if (product.Version != expectedVersion)
        {
            throw new InvalidOperationException("商品信息已被其他管理员修改，请刷新后重试");
        }
    }

    /// <summary>下架商品</summary>
    public async Task<bool> TakeOffSaleAsync(long productId, int expectedVersion)
    {
        try
        {
            var product = await _productRepository.QueryFirstAsync(p => p.Id == productId && !p.IsDeleted);
            if (product == null)
            {
                return false;
            }

            EnsureExpectedProductVersion(product, expectedVersion);

            var affected = await _productRepository.UpdateColumnsAsync(
                p => new Product
                {
                    IsOnSale = false,
                    OffSaleTime = DateTime.Now,
                    Version = p.Version + 1,
                    ModifyTime = DateTime.Now
                },
                p => p.Id == productId && p.TenantId == product.TenantId && p.Version == expectedVersion && !p.IsDeleted);
            if (affected <= 0)
            {
                throw new InvalidOperationException("商品信息已被其他管理员修改，请刷新后重试");
            }

            Log.Information("商品 {ProductId} 下架成功", productId);

            return true;
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
            Expression<Func<Product, bool>> where = p => !p.IsDeleted;

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
            await FillProductCategoryNamesAsync(productVos);
            FillProductUrls(productVos);

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

    /// <summary>获取商品详情（管理后台）</summary>
    public async Task<ProductVo?> GetProductDetailForAdminAsync(long productId)
    {
        try
        {
            var product = await _productRepository.QueryFirstAsync(p => p.Id == productId && !p.IsDeleted);
            if (product == null)
            {
                return null;
            }

            var productVo = Mapper.Map<ProductVo>(product);
            await FillProductCategoryNamesAsync([productVo]);
            FillProductUrl(productVo);
            return productVo;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取商品 {ProductId} 详情（管理后台）失败", productId);
            throw;
        }
    }

    /// <summary>删除商品（管理后台）</summary>
    public async Task<bool> DeleteProductAsync(long productId, long operatorId, string operatorName)
    {
        try
        {
            var product = await _productRepository.QueryFirstAsync(p => p.Id == productId && !p.IsDeleted);
            if (product == null)
            {
                throw new InvalidOperationException("商品不存在");
            }

            var relatedOrderCount = await _orderRepository.QueryCountAsync(
                order => order.ProductId == productId
                    && !order.IsDeleted
                    && order.TenantId == product.TenantId);
            if (relatedOrderCount > 0)
            {
                throw new InvalidOperationException("商品已有订单记录，不能删除；请下架商品以保留历史订单快照");
            }

            product.IsDeleted = true;
            product.IsOnSale = false;
            product.OffSaleTime = DateTime.Now;
            product.ModifyTime = DateTime.Now;
            product.ModifyBy = string.IsNullOrWhiteSpace(operatorName) ? "Unknown" : operatorName.Trim();
            product.ModifyId = operatorId;

            var result = await _productRepository.UpdateAsync(product);
            if (result)
            {
                Log.Information("商品 {ProductId} 删除成功，操作员={OperatorName}({OperatorId})",
                    productId,
                    product.ModifyBy,
                    operatorId);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除商品 {ProductId} 失败", productId);
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

    private static long NormalizeTenantId(long tenantId)
    {
        return tenantId > 0 ? tenantId : 0;
    }

    private void FillProductCategoryUrls(List<ProductCategoryVo> categories)
    {
        foreach (var category in categories)
        {
            FillProductCategoryUrl(category);
        }
    }

    private async Task FillProductCategoryNamesAsync(IReadOnlyCollection<ProductVo> products)
    {
        if (products.Count == 0)
        {
            return;
        }

        var categoryIds = products
            .Select(product => product.VoCategoryId)
            .Where(categoryId => !string.IsNullOrWhiteSpace(categoryId))
            .Distinct()
            .ToList();
        if (categoryIds.Count == 0)
        {
            return;
        }

        var categories = await _categoryRepository.QueryAsync(category => categoryIds.Contains(category.Id));
        var categoryDict = categories.ToDictionary(category => category.Id, category => category.Name);

        foreach (var product in products)
        {
            if (categoryDict.TryGetValue(product.VoCategoryId, out var categoryName))
            {
                product.VoCategoryName = categoryName;
            }
        }
    }

    private void FillProductCategoryUrl(ProductCategoryVo category)
    {
        category.VoIcon = ResolveAttachmentUrl(category.VoIconAttachmentId);
    }

    private void FillProductUrls(List<ProductVo> products)
    {
        foreach (var product in products)
        {
            FillProductUrl(product);
        }
    }

    private void FillProductUrl(ProductVo product)
    {
        product.VoIcon = ResolveAttachmentUrl(product.VoIconAttachmentId);
        product.VoCoverImage = ResolveAttachmentUrl(product.VoCoverAttachmentId);
    }

    private void FillProductListItemUrls(List<ProductListItemVo> products)
    {
        foreach (var product in products)
        {
            product.VoIcon = ResolveAttachmentUrl(product.VoIconAttachmentId);
            product.VoCoverImage = ResolveAttachmentUrl(product.VoCoverAttachmentId);
        }
    }

    private string? ResolveAttachmentUrl(long? attachmentId)
    {
        if (!attachmentId.HasValue || attachmentId.Value <= 0)
        {
            return null;
        }

        return _attachmentUrlResolver.ResolveAttachmentUrl(attachmentId.Value);
    }

    #endregion
}
