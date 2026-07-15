using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public class ShopControllerTest
{
    [Fact]
    public async Task GetProductCapabilities_ShouldReturnServerAuthorityMetadata()
    {
        var capabilities = new List<ShopProductCapabilityVo>
        {
            new()
            {
                VoProductType = ProductType.Benefit,
                VoBenefitType = BenefitType.Badge,
                VoCanSell = true,
                VoCanActivate = true
            }
        };
        var productServiceMock = new Mock<IProductService>(MockBehavior.Strict);
        productServiceMock
            .Setup(service => service.GetProductCapabilitiesAsync())
            .ReturnsAsync(capabilities);

        var controller = CreateController(productServiceMock.Object, Mock.Of<IOrderService>());
        var result = await controller.GetProductCapabilities();

        Assert.True(result.IsSuccess);
        Assert.Same(capabilities, result.ResponseData);
    }

    [Fact]
    public async Task AdminGetOrder_ShouldReturnOrderDetail()
    {
        var productServiceMock = new Mock<IProductService>(MockBehavior.Strict);
        var orderServiceMock = new Mock<IOrderService>(MockBehavior.Strict);
        orderServiceMock
            .Setup(service => service.GetOrderDetailForAdminAsync(7001))
            .ReturnsAsync(new OrderVo
            {
                VoId = 7001,
                VoOrderNo = "ORD_7001",
                VoUserId = 9527,
                VoProductId = 1001,
                VoProductName = "体验红包",
                VoProductType = ProductType.Consumable,
                VoQuantity = 1,
                VoUnitPrice = 50,
                VoTotalPrice = 50,
                VoStatus = OrderStatus.Completed,
                VoCreateTime = DateTime.Now
            });

        var controller = CreateController(productServiceMock.Object, orderServiceMock.Object);
        var result = await controller.AdminGetOrder(7001);

        Assert.True(result.IsSuccess);
        var payload = Assert.IsType<OrderVo>(result.ResponseData);
        Assert.Equal("ORD_7001", payload.VoOrderNo);
    }

    [Fact]
    public async Task AdminGetOrder_ShouldReturnStableNotFoundError_WhenOrderMissing()
    {
        var productServiceMock = new Mock<IProductService>(MockBehavior.Strict);
        var orderServiceMock = new Mock<IOrderService>(MockBehavior.Strict);
        orderServiceMock
            .Setup(service => service.GetOrderDetailForAdminAsync(7999))
            .ReturnsAsync((OrderVo?)null);

        var controller = CreateController(productServiceMock.Object, orderServiceMock.Object);
        var result = await controller.AdminGetOrder(7999);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Order.NotFound", result.Code);
        Assert.Equal("error.order.not_found", result.MessageKey);
    }

    [Fact]
    public async Task RetryGrantBenefit_ShouldPreserveStableBusinessError()
    {
        var productServiceMock = new Mock<IProductService>(MockBehavior.Strict);
        var orderServiceMock = new Mock<IOrderService>(MockBehavior.Strict);
        orderServiceMock
            .Setup(service => service.RetryGrantBenefitAsync(7002))
            .ThrowsAsync(new BusinessException(
                "只能重试发放失败的订单",
                409,
                "Order.RetryRejected",
                "error.order.retry_rejected"));

        var controller = CreateController(productServiceMock.Object, orderServiceMock.Object);
        var result = await controller.RetryGrantBenefit(7002);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Order.RetryRejected", result.Code);
        Assert.Equal("error.order.retry_rejected", result.MessageKey);
    }

    [Fact]
    public async Task AdminGetProduct_ShouldReturnNotFound_WhenProductMissing()
    {
        var productServiceMock = new Mock<IProductService>(MockBehavior.Strict);
        productServiceMock
            .Setup(service => service.GetProductDetailForAdminAsync(1001))
            .ReturnsAsync((ProductVo?)null);
        var orderServiceMock = new Mock<IOrderService>(MockBehavior.Strict);

        var controller = CreateController(productServiceMock.Object, orderServiceMock.Object);
        var result = await controller.AdminGetProduct(1001);

        Assert.False(result.IsSuccess);
        Assert.Equal("商品不存在", result.MessageInfo);
    }

    [Fact]
    public async Task DeleteProduct_ShouldReturnFailure_WhenServiceRejects()
    {
        var productServiceMock = new Mock<IProductService>(MockBehavior.Strict);
        productServiceMock
            .Setup(service => service.DeleteProductAsync(1001, 10001, "Tester"))
            .ThrowsAsync(new InvalidOperationException("存在未完成订单，不能删除商品"));
        var orderServiceMock = new Mock<IOrderService>(MockBehavior.Strict);

        var controller = CreateController(productServiceMock.Object, orderServiceMock.Object);
        var result = await controller.DeleteProduct(1001);

        Assert.False(result.IsSuccess);
        Assert.Equal("存在未完成订单，不能删除商品", result.MessageInfo);
        Assert.False(result.ResponseData);
    }

    private static ShopController CreateController(IProductService productService, IOrderService orderService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new ShopController(
            productService,
            orderService,
            Mock.Of<IUserBenefitService>(),
            Mock.Of<IUserInventoryService>(),
            Mock.Of<IUserBrowseHistoryService>(),
            currentUserAccessorMock.Object,
            CreateErrorsLocalizer());
    }

    private static IStringLocalizer<Errors> CreateErrorsLocalizer()
    {
        var localizerMock = new Mock<IStringLocalizer<Errors>>();
        localizerMock
            .Setup(localizer => localizer[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key, resourceNotFound: true));
        return localizerMock.Object;
    }
}
