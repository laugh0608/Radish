using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
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
}
