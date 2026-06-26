using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ProductServiceTest
{
    [Fact]
    public async Task CheckCanBuyAsync_ShouldRejectLotteryTicket()
    {
        var product = CreateLotteryTicketProduct();
        var productRepository = CreateProductRepository(product);
        var categoryRepository = new Mock<IBaseRepository<ProductCategory>>(MockBehavior.Strict);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var service = new ProductService(
            mapper.Object,
            productRepository.Object,
            categoryRepository.Object,
            orderRepository.Object,
            attachmentUrlResolver.Object);

        var (canBuy, reason) = await service.CheckCanBuyAsync(9527, product.Id, 1);

        Assert.False(canBuy);
        Assert.Equal("抽奖券暂未开放，当前不可购买", reason);
    }

    [Fact]
    public async Task CheckCanBuyAsync_ShouldRejectBadgeBenefit()
    {
        var product = CreateBadgeProduct();
        var productRepository = CreateProductRepository(product);
        var categoryRepository = new Mock<IBaseRepository<ProductCategory>>(MockBehavior.Strict);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var service = new ProductService(
            mapper.Object,
            productRepository.Object,
            categoryRepository.Object,
            orderRepository.Object,
            attachmentUrlResolver.Object);

        var (canBuy, reason) = await service.CheckCanBuyAsync(9527, product.Id, 1);

        Assert.False(canBuy);
        Assert.Equal("徽章暂未开放，当前不可购买", reason);
    }

    [Fact]
    public async Task CheckCanBuyAsync_ShouldRejectMisconfiguredCoinCard()
    {
        var product = CreateMisconfiguredCoinCardProduct();
        var productRepository = CreateProductRepository(product);
        var categoryRepository = new Mock<IBaseRepository<ProductCategory>>(MockBehavior.Strict);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var service = new ProductService(
            mapper.Object,
            productRepository.Object,
            categoryRepository.Object,
            orderRepository.Object,
            attachmentUrlResolver.Object);

        var (canBuy, reason) = await service.CheckCanBuyAsync(9527, product.Id, 1);

        Assert.False(canBuy);
        Assert.Equal("商品配置不完整，请联系管理员", reason);
    }

    [Fact]
    public async Task CreateProductAsync_ShouldRejectMisconfiguredCoinCard()
    {
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Strict);
        var categoryRepository = new Mock<IBaseRepository<ProductCategory>>(MockBehavior.Strict);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var service = new ProductService(
            mapper.Object,
            productRepository.Object,
            categoryRepository.Object,
            orderRepository.Object,
            attachmentUrlResolver.Object);

        var dto = new CreateProductDto
        {
            Name = "测试红包",
            CategoryId = "effect",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.CoinCard,
            BenefitValue = null,
            IsOnSale = true,
            Price = 50,
            StockType = StockType.Limited,
            Stock = 10,
            DurationType = DurationType.Days,
            DurationDays = 5
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateProductAsync(dto, 10001, "tester"));
        Assert.Equal("萝卜币红包必须配置正整数胡萝卜数量", exception.Message);
        productRepository.Verify(r => r.AddAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task GetProductDetailAsync_ShouldHideLotteryTicketFromPublicView()
    {
        var product = CreateLotteryTicketProduct();
        var productRepository = CreateProductRepository(product);
        var categoryRepository = new Mock<IBaseRepository<ProductCategory>>(MockBehavior.Strict);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var service = new ProductService(
            mapper.Object,
            productRepository.Object,
            categoryRepository.Object,
            orderRepository.Object,
            attachmentUrlResolver.Object);

        var result = await service.GetProductDetailAsync(product.Id);

        Assert.Null(result);
        categoryRepository.Verify(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<ProductCategory, bool>>?>()), Times.Never);
    }

    [Fact]
    public async Task GetProductDetailAsync_ShouldHideBadgeBenefitFromPublicView()
    {
        var product = CreateBadgeProduct();
        var productRepository = CreateProductRepository(product);
        var categoryRepository = new Mock<IBaseRepository<ProductCategory>>(MockBehavior.Strict);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var service = new ProductService(
            mapper.Object,
            productRepository.Object,
            categoryRepository.Object,
            orderRepository.Object,
            attachmentUrlResolver.Object);

        var result = await service.GetProductDetailAsync(product.Id);

        Assert.Null(result);
        categoryRepository.Verify(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<ProductCategory, bool>>?>()), Times.Never);
    }

    [Fact]
    public async Task GetProductDetailForAdminAsync_ShouldReturnUnsupportedProductForAdmin()
    {
        var product = CreateLotteryTicketProduct();
        product.IconAttachmentId = 9001;
        product.CoverAttachmentId = 9002;
        var category = new ProductCategory
        {
            Id = product.CategoryId,
            Name = "效果道具",
            IsEnabled = true,
            CreateTime = DateTime.Now
        };
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        mapper
            .Setup(m => m.Map<ProductVo>(It.IsAny<Product>()))
            .Returns<Product>(source => new ProductVo
            {
                VoId = source.Id,
                VoName = source.Name,
                VoCategoryId = source.CategoryId,
                VoIconAttachmentId = source.IconAttachmentId,
                VoCoverAttachmentId = source.CoverAttachmentId,
                VoProductType = source.ProductType,
                VoConsumableType = source.ConsumableType,
                VoPrice = source.Price,
                VoStockType = source.StockType,
                VoStock = source.Stock,
                VoSoldCount = source.SoldCount,
                VoLimitPerUser = source.LimitPerUser,
                VoDurationType = source.DurationType,
                VoDurationDays = source.DurationDays,
                VoExpiresAt = source.ExpiresAt,
                VoSortOrder = source.SortOrder,
                VoIsOnSale = source.IsOnSale,
                VoIsEnabled = source.IsEnabled,
                VoCreateTime = source.CreateTime
            });
        var productRepository = CreateProductRepository(product);
        var categoryRepository = new Mock<IBaseRepository<ProductCategory>>(MockBehavior.Strict);
        categoryRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<ProductCategory, bool>>?>()))
            .ReturnsAsync((Expression<Func<ProductCategory, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return [category];
                }

                var predicate = expression.Compile();
                return predicate(category) ? [category] : [];
            });
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        attachmentUrlResolver
            .Setup(resolver => resolver.ResolveAttachmentUrl(9001))
            .Returns("https://cdn.example.com/product-icon.png");
        attachmentUrlResolver
            .Setup(resolver => resolver.ResolveAttachmentUrl(9002))
            .Returns("https://cdn.example.com/product-cover.png");

        var service = new ProductService(
            mapper.Object,
            productRepository.Object,
            categoryRepository.Object,
            orderRepository.Object,
            attachmentUrlResolver.Object);

        var result = await service.GetProductDetailForAdminAsync(product.Id);

        Assert.NotNull(result);
        Assert.Equal("效果道具", result!.VoCategoryName);
        Assert.Equal("https://cdn.example.com/product-icon.png", result.VoIcon);
        Assert.Equal("https://cdn.example.com/product-cover.png", result.VoCoverImage);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldRejectWhenRelatedOrdersExist()
    {
        var product = CreateMisconfiguredCoinCardProduct();
        product.Id = 100777;
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var productRepository = CreateProductRepository(product);
        var categoryRepository = new Mock<IBaseRepository<ProductCategory>>(MockBehavior.Strict);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        orderRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Order, bool>>?>()))
            .ReturnsAsync((Expression<Func<Order, bool>>? expression) =>
            {
                var completedOrder = new Order
                {
                    ProductId = product.Id,
                    Status = OrderStatus.Completed,
                    TenantId = product.TenantId,
                    IsDeleted = false
                };

                if (expression == null)
                {
                    return 1;
                }

                var predicate = expression.Compile();
                return predicate(completedOrder) ? 1 : 0;
            });
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);

        var service = new ProductService(
            mapper.Object,
            productRepository.Object,
            categoryRepository.Object,
            orderRepository.Object,
            attachmentUrlResolver.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteProductAsync(product.Id, 10001, "tester"));

        Assert.Equal("商品已有订单记录，不能删除；请下架商品以保留历史订单快照", exception.Message);
        productRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldRejectWhenExpectedVersionMismatch()
    {
        var product = CreateEditableCoinCardProduct();
        product.Version = 3;
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var productRepository = CreateProductRepository(product);
        var categoryRepository = new Mock<IBaseRepository<ProductCategory>>(MockBehavior.Strict);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);

        var service = new ProductService(
            mapper.Object,
            productRepository.Object,
            categoryRepository.Object,
            orderRepository.Object,
            attachmentUrlResolver.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateProductAsync(
            new UpdateProductDto
            {
                Id = product.Id,
                Name = "新商品名",
                CategoryId = product.CategoryId,
                ProductType = product.ProductType,
                ConsumableType = product.ConsumableType,
                BenefitValue = product.BenefitValue,
                Price = product.Price,
                StockType = product.StockType,
                Stock = product.Stock,
                DurationType = DurationType.Permanent,
                ExpectedVersion = 2
            },
            10001,
            "tester"));

        Assert.Equal("商品信息已被其他管理员修改，请刷新后重试", exception.Message);
        productRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Product, Product>>>(),
                It.IsAny<Expression<Func<Product, bool>>>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldUpdateByExpectedVersionAndIncrementVersion()
    {
        var product = CreateEditableCoinCardProduct();
        product.Version = 3;
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        mapper
            .Setup(m => m.Map(It.IsAny<UpdateProductDto>(), It.IsAny<Product>()))
            .Callback<UpdateProductDto, Product>((source, target) =>
            {
                target.Name = source.Name;
                target.CategoryId = source.CategoryId;
                target.ProductType = source.ProductType;
                target.ConsumableType = source.ConsumableType;
                target.BenefitValue = source.BenefitValue;
                target.Price = source.Price;
                target.StockType = source.StockType;
                target.Stock = source.Stock;
                target.DurationType = source.DurationType;
                target.IsOnSale = source.IsOnSale;
            })
            .Returns((UpdateProductDto _, Product target) => target);
        var productRepository = CreateProductRepository(product);
        var persistedProduct = CloneProduct(product);
        Product? patch = null;
        productRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Product, Product>>>(),
                It.IsAny<Expression<Func<Product, bool>>>()))
            .ReturnsAsync((Expression<Func<Product, Product>> updateExpression, Expression<Func<Product, bool>> whereExpression) =>
            {
                if (!whereExpression.Compile()(persistedProduct))
                {
                    return 0;
                }

                patch = updateExpression.Compile()(persistedProduct);
                return 1;
            });
        var categoryRepository = new Mock<IBaseRepository<ProductCategory>>(MockBehavior.Strict);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);

        var service = new ProductService(
            mapper.Object,
            productRepository.Object,
            categoryRepository.Object,
            orderRepository.Object,
            attachmentUrlResolver.Object);

        var result = await service.UpdateProductAsync(
            new UpdateProductDto
            {
                Id = product.Id,
                Name = "新商品名",
                CategoryId = product.CategoryId,
                ProductType = product.ProductType,
                ConsumableType = product.ConsumableType,
                BenefitValue = "20",
                Price = 60,
                StockType = StockType.Limited,
                Stock = 8,
                DurationType = DurationType.Permanent,
                IsOnSale = true,
                ExpectedVersion = 3
            },
            10001,
            "tester");

        Assert.True(result);
        Assert.NotNull(patch);
        Assert.Equal("新商品名", patch!.Name);
        Assert.Equal(4, patch.Version);
    }

    [Fact]
    public async Task PutOnSaleAsync_ShouldUseExpectedVersionAndIncrementVersion()
    {
        var product = CreateEditableCoinCardProduct();
        product.IsOnSale = false;
        product.Version = 5;
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var productRepository = CreateProductRepository(product);
        Product? patch = null;
        productRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Product, Product>>>(),
                It.IsAny<Expression<Func<Product, bool>>>()))
            .ReturnsAsync((Expression<Func<Product, Product>> updateExpression, Expression<Func<Product, bool>> whereExpression) =>
            {
                if (!whereExpression.Compile()(product))
                {
                    return 0;
                }

                patch = updateExpression.Compile()(product);
                return 1;
            });
        var categoryRepository = new Mock<IBaseRepository<ProductCategory>>(MockBehavior.Strict);
        var orderRepository = new Mock<IBaseRepository<Order>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);

        var service = new ProductService(
            mapper.Object,
            productRepository.Object,
            categoryRepository.Object,
            orderRepository.Object,
            attachmentUrlResolver.Object);

        var result = await service.PutOnSaleAsync(product.Id, 5);

        Assert.True(result);
        Assert.NotNull(patch);
        Assert.True(patch!.IsOnSale);
        Assert.Equal(6, patch.Version);
    }

    private static Product CreateLotteryTicketProduct()
    {
        return new Product
        {
            Id = 100099,
            Name = "抽奖券",
            CategoryId = "effect",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.LotteryTicket,
            IsEnabled = true,
            IsOnSale = true,
            Price = 30,
            StockType = StockType.Unlimited,
            CreateTime = DateTime.Now,
            CreateBy = "System"
        };
    }

    private static Product CreateBadgeProduct()
    {
        return new Product
        {
            Id = 100001,
            Name = "元老徽章",
            CategoryId = "badge",
            ProductType = ProductType.Benefit,
            BenefitType = BenefitType.Badge,
            BenefitValue = "badge-veteran",
            IsEnabled = true,
            IsOnSale = true,
            Price = 500,
            StockType = StockType.Unlimited,
            CreateTime = DateTime.Now,
            CreateBy = "System"
        };
    }

    private static Product CreateMisconfiguredCoinCardProduct()
    {
        return new Product
        {
            Id = 100062,
            Name = "测试红包",
            CategoryId = "effect",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.CoinCard,
            BenefitValue = null,
            IsEnabled = true,
            IsOnSale = true,
            Price = 50,
            StockType = StockType.Limited,
            Stock = 10,
            CreateTime = DateTime.Now,
            CreateBy = "System"
        };
    }

    private static Product CreateEditableCoinCardProduct()
    {
        return new Product
        {
            Id = 100063,
            Name = "测试红包",
            CategoryId = "effect",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.CoinCard,
            BenefitValue = "10",
            IsEnabled = true,
            IsOnSale = true,
            Price = 50,
            StockType = StockType.Limited,
            Stock = 10,
            DurationType = DurationType.Permanent,
            TenantId = 0,
            CreateTime = DateTime.Now,
            CreateBy = "System"
        };
    }

    private static Mock<IBaseRepository<Product>> CreateProductRepository(Product product)
    {
        var repository = new Mock<IBaseRepository<Product>>(MockBehavior.Strict);
        repository
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>?>()))
            .ReturnsAsync((Expression<Func<Product, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return product;
                }

                var predicate = expression.Compile();
                return predicate(product) ? product : null;
            });
        return repository;
    }

    private static Product CloneProduct(Product product)
    {
        return new Product
        {
            Id = product.Id,
            Name = product.Name,
            CategoryId = product.CategoryId,
            ProductType = product.ProductType,
            BenefitType = product.BenefitType,
            ConsumableType = product.ConsumableType,
            BenefitValue = product.BenefitValue,
            IsEnabled = product.IsEnabled,
            IsOnSale = product.IsOnSale,
            Price = product.Price,
            OriginalPrice = product.OriginalPrice,
            StockType = product.StockType,
            Stock = product.Stock,
            SoldCount = product.SoldCount,
            LimitPerUser = product.LimitPerUser,
            DurationType = product.DurationType,
            DurationDays = product.DurationDays,
            ExpiresAt = product.ExpiresAt,
            SortOrder = product.SortOrder,
            TenantId = product.TenantId,
            Version = product.Version,
            CreateTime = product.CreateTime,
            CreateBy = product.CreateBy,
            CreateId = product.CreateId
        };
    }
}
