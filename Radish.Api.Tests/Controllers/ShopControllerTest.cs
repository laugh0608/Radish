using System;
using System.Threading.Tasks;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public class ShopControllerTest
{
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
            currentUserAccessorMock.Object);
    }
}
