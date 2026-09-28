using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Resources;
using Radish.Common;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class ShopManagementLoggingTests
{
    private const string Secret = "SHOP_MANAGEMENT_PRIVATE_SENTINEL";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };
    public static IEnumerable<object[]> ManagementModes =>
        from candidate in new[] { false, true }
        from environment in new[] { "Production", "Development" }
        from operation in new[] { "create", "update", "delete", "on-sale", "off-sale", "remark" }
        select new object[] { candidate, environment, operation };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Queries_ShouldPreserveResultsFiltersAndQuietOutput(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        Assert.Equal(1, Assert.Single(await f.Product.GetCategoriesAsync()).VoProductCount);
        Assert.Equal(Secret, (await f.Product.GetCategoryAsync(Secret))!.VoName);
        Assert.Equal(Secret, Assert.Single((await f.Product.GetProductListAsync(Secret, ProductType.Benefit, Secret)).Data).VoName);
        Assert.Equal(Secret, (await f.Product.GetProductDetailAsync(3))!.VoCategoryName);
        Assert.Equal(Secret, Assert.Single((await f.Product.GetProductListForAdminAsync()).Data).VoName);
        Assert.Equal(Secret, (await f.Product.GetProductDetailForAdminAsync(3))!.VoCategoryName);
        Assert.Single((await f.Order.GetUserOrdersAsync(1, OrderStatus.Completed)).Data);
        Assert.Equal(Secret, (await f.Order.GetOrderDetailAsync(1, 4))!.VoOrderNo);
        Assert.Null(await f.Order.GetOrderDetailAsync(2, 4));
        Assert.Equal(Secret, (await f.Order.GetOrderByNoAsync(Secret))!.VoOrderNo);
        Assert.Equal(1, await f.Order.GetUserPurchaseCountAsync(1, 3));
        Assert.Single((await f.Order.GetOrderListForAdminAsync(1, OrderStatus.Completed, 3, Secret)).Data);
        Assert.Equal(Secret, (await f.Order.GetOrderDetailForAdminAsync(4))!.VoOrderNo);
        f.ProductRow.IsDeleted = true;
        Assert.Null(await f.Product.GetProductDetailAsync(3));
        Assert.Null(await f.Product.GetProductDetailForAdminAsync(3));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task QueryFailures_ShouldPropagateOriginalExceptionAndReachApiOnce(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var failure = new IOException(Secret);
        f.Products.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>())).ThrowsAsync(failure);
        f.Categories.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ProductCategory, bool>>>())).ThrowsAsync(failure);
        f.Categories.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<ProductCategory, bool>>>())).ThrowsAsync(failure);
        f.Orders.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>>())).ThrowsAsync(failure);
        f.Orders.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<Order, bool>>>())).ThrowsAsync(failure);
        f.Products.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<Product, bool>>>(), 1, 20,
            It.IsAny<Expression<Func<Product, object>>>(), OrderByType.Asc)).ThrowsAsync(failure);
        f.Products.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<Product, bool>>>(), 1, 20,
            It.IsAny<Expression<Func<Product, object>>>(), OrderByType.Desc,
            It.IsAny<Expression<Func<Product, object>>>(), OrderByType.Desc)).ThrowsAsync(failure);
        f.Orders.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<Order, bool>>>(), 1, 20,
            It.IsAny<Expression<Func<Order, object>>>(), OrderByType.Desc)).ThrowsAsync(failure);
        Func<Task>[] queries =
        [
            () => f.Controller.GetCategories(), () => f.Controller.GetCategory(Secret),
            () => f.Controller.GetProducts(), () => f.Controller.GetProduct(3),
            () => f.Controller.AdminGetProducts(), () => f.Controller.AdminGetProduct(3),
            () => f.Controller.GetMyOrders(), () => f.Controller.GetOrder(4),
            () => f.Order.GetOrderByNoAsync(Secret), () => f.Order.GetUserPurchaseCountAsync(1, 3),
            () => f.Controller.AdminGetOrders(), () => f.Controller.AdminGetOrder(4)
        ];
        foreach (var query in queries)
        {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(query));
            capture.AssertQuiet();
            await InvokeApiAsync(query, capture.Logger, 500);
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ProductWrites_ShouldKeepAuditVersionAndSoftDeleteQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        Assert.Equal(8, await f.Product.CreateProductAsync(CreateRequest(), 9, Secret));
        f.Products.Verify(r => r.AddAsync(It.Is<Product>(p => p.Name == Secret && p.CreateId == 9 &&
            p.CreateBy == Secret && !p.IsOnSale && p.OnSaleTime == null)), Times.Once);
        Assert.True(await f.Product.UpdateProductAsync(UpdateRequest(), 9, Secret));
        Assert.Equal(4, f.Writes[0].Version);
        Assert.Equal(Secret, f.Writes[0].ModifyBy);
        Assert.Equal(9, f.Writes[0].ModifyId);
        f.ProductRow.Version = 3;
        Assert.True(await f.Product.PutOnSaleAsync(3, 3));
        Assert.True(f.Writes[1].IsOnSale);
        Assert.Equal(4, f.Writes[1].Version);
        Assert.True(await f.Product.TakeOffSaleAsync(3, 3));
        Assert.False(f.Writes[2].IsOnSale);
        f.Orders.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<Order, bool>>>())).ReturnsAsync(0);
        Assert.True(await f.Product.DeleteProductAsync(3, 9, Secret));
        f.Products.Verify(r => r.UpdateAsync(It.Is<Product>(p => p.IsDeleted && !p.IsOnSale &&
            p.ModifyId == 9 && p.ModifyBy == Secret)), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ConflictsAndOrderRemark_ShouldKeepResponsesAndAuditQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var conflict = await f.Controller.PutOnSale(3, new ProductVersionWriteDto { ExpectedVersion = 2 });
        Assert.Equal(409, conflict.StatusCode);
        Assert.False(conflict.IsSuccess);
        f.Products.Verify(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>()), Times.Never);
        Assert.Equal(409, (await f.Controller.DeleteProduct(3)).StatusCode);
        f.Products.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Never);
        var result = await f.Controller.AdminRemarkOrder(4, new AdminRemarkOrderDto { Remark = "  " + Secret + "  " });
        Assert.True(result.IsSuccess);
        f.Orders.Verify(r => r.UpdateAsync(It.Is<Order>(o => o.AdminRemark == Secret && o.ModifyId == 1 && o.ModifyBy == Secret)), Times.Once);
        f.Orders.Setup(r => r.UpdateAsync(It.IsAny<Order>())).ReturnsAsync(false);
        Assert.Equal(409, (await f.Controller.AdminRemarkOrder(4, new AdminRemarkOrderDto { Remark = Secret })).StatusCode);
        f.Orders.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>>())).ReturnsAsync((Order?)null);
        Assert.Equal(404, (await f.Controller.AdminRemarkOrder(4, new AdminRemarkOrderDto { Remark = Secret })).StatusCode);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(ManagementModes))]
    public async Task ManagementFailures_ShouldHaveOneFinalOwnerAndKeepResponses(bool candidate, string environment, string operation)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        // Fail the real Service's repository, including create after configuration validation.
        void FailWith(Exception failure)
        {
            f.Products.Setup(r => r.AddAsync(It.IsAny<Product>())).ThrowsAsync(failure);
            f.Products.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>())).ThrowsAsync(failure);
            f.Orders.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>>())).ThrowsAsync(failure);
        }
        Func<Task<IMessageModel>> invoke = operation switch
        {
            "create" => async () => await f.Controller.CreateProduct(CreateRequest()),
            "update" => async () => await f.Controller.UpdateProduct(UpdateRequest()),
            "delete" => async () => await f.Controller.DeleteProduct(3),
            "on-sale" => async () => await f.Controller.PutOnSale(3, new ProductVersionWriteDto { ExpectedVersion = 3 }),
            "off-sale" => async () => await f.Controller.TakeOffSale(3, new ProductVersionWriteDto { ExpectedVersion = 3 }),
            "remark" => async () => await f.Controller.AdminRemarkOrder(4, new AdminRemarkOrderDto { Remark = Secret }),
            _ => throw new ArgumentException(nameof(operation))
        };
        FailWith(new InvalidOperationException(Secret));
        Assert.Equal(operation is "create" or "update" ? 400 : 409, (await invoke()).StatusCode);
        capture.AssertSingle(operation == "remark" ? "order.remark_rejected" : "product.management_rejected", "Warning");
        capture.Clear();
        FailWith(new BusinessException(Secret, 409, Secret));
        Assert.Equal(409, (await invoke()).StatusCode);
        capture.AssertQuiet();
        FailWith(new BusinessException(Secret, 503, Secret));
        Assert.Equal(503, (await invoke()).StatusCode);
        capture.AssertSingle("http.failed", "Error");
        capture.Clear();
        FailWith(new IOException(Secret));
        await InvokeApiAsync(async () => await invoke(), capture.Logger, 500);
        capture.AssertSingle("http.failed", "Error");
    }

    private static CreateProductDto CreateRequest() => new()
    {
        Name = Secret, CategoryId = Secret, ProductType = ProductType.Benefit,
        BenefitType = BenefitType.Title, BenefitValue = Secret
    };

    private static UpdateProductDto UpdateRequest() => new()
    {
        Id = 3, ExpectedVersion = 3, Name = Secret, CategoryId = Secret,
        ProductType = ProductType.Benefit, BenefitType = BenefitType.Title, BenefitValue = Secret
    };

    private static async Task InvokeApiAsync(Func<Task> action, Serilog.ILogger logger, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        app.Run(async _ => await action());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test/" + Secret;
        context.Response.Body = new MemoryStream();
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IConfiguration _previous = AppSettingsTool.Configuration;
        public Mock<IBaseRepository<Product>> Products { get; } = new();
        public Mock<IBaseRepository<ProductCategory>> Categories { get; } = new();
        public Mock<IBaseRepository<Order>> Orders { get; } = new();
        public Product ProductRow { get; } = new()
        {
            Id = 3, Name = Secret, CategoryId = Secret, BenefitValue = Secret, ProductType = ProductType.Benefit,
            BenefitType = BenefitType.Title, IsEnabled = true, IsOnSale = true, Version = 3
        };
        public Order OrderRow { get; } = new() { Id = 4, UserId = 1, ProductId = 3, OrderNo = Secret, Status = OrderStatus.Completed };
        public List<Product> Writes { get; } = [];
        public ProductService Product { get; }
        public OrderService Order { get; }
        public ShopController Controller { get; }

        public Fixture()
        {
            AppSettingsTool.Configuration = new ConfigurationBuilder().Build();
            var category = new ProductCategory { Id = Secret, Name = Secret, IsEnabled = true };
            Categories.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<ProductCategory, bool>>>()))
                .ReturnsAsync((Expression<Func<ProductCategory, bool>> p) => new[] { category }.Where(p.Compile()).ToList());
            Categories.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ProductCategory, bool>>>()))
                .ReturnsAsync((Expression<Func<ProductCategory, bool>> p) => p.Compile()(category) ? category : null);
            Products.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Product, bool>>>()))
                .ReturnsAsync((Expression<Func<Product, bool>> p) => p.Compile()(ProductRow) ? ProductRow : null);
            Products.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<Product, bool>>>()))
                .ReturnsAsync((Expression<Func<Product, bool>> p) => p.Compile()(ProductRow) ? 1 : 0);
            Products.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<Product, bool>>>(), 1, 20,
                It.IsAny<Expression<Func<Product, object>>>(), OrderByType.Asc)).ReturnsAsync((new List<Product> { ProductRow }, 1));
            Products.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<Product, bool>>>(), 1, 20,
                It.IsAny<Expression<Func<Product, object>>>(), OrderByType.Desc,
                It.IsAny<Expression<Func<Product, object>>>(), OrderByType.Desc)).ReturnsAsync((new List<Product> { ProductRow }, 1));
            Products.Setup(r => r.AddAsync(It.IsAny<Product>())).ReturnsAsync(8);
            Products.Setup(r => r.UpdateAsync(It.IsAny<Product>())).ReturnsAsync(true);
            Products.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Product, Product>>>(), It.IsAny<Expression<Func<Product, bool>>>()))
                .Callback((Expression<Func<Product, Product>> update, Expression<Func<Product, bool>> where) =>
                {
                    var before = new Product { Id = 3, Version = 3, TenantId = ProductRow.TenantId };
                    Assert.True(where.Compile()(before));
                    before.Version = 2;
                    Assert.False(where.Compile()(before));
                    Writes.Add(update.Compile()(new Product { Version = 3 }));
                }).ReturnsAsync(1);
            Orders.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Order, bool>>>()))
                .ReturnsAsync((Expression<Func<Order, bool>> p) => p.Compile()(OrderRow) ? OrderRow : null);
            Orders.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<Order, bool>>>()))
                .ReturnsAsync((Expression<Func<Order, bool>> p) => p.Compile()(OrderRow) ? 1 : 0);
            Orders.Setup(r => r.QueryPageAsync(It.IsAny<Expression<Func<Order, bool>>>(), 1, 20,
                It.IsAny<Expression<Func<Order, object>>>(), OrderByType.Desc)).ReturnsAsync((new List<Order> { OrderRow }, 1));
            Orders.Setup(r => r.UpdateAsync(It.IsAny<Order>())).ReturnsAsync(true);
            var users = new Mock<IBaseRepository<User>>();
            users.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync([new User { Id = 1, UserName = Secret }]);
            var mapper = new Mock<IMapper>();
            mapper.Setup(m => m.Map<Product>(It.IsAny<object>())).Returns((object o) =>
            {
                var dto = (CreateProductDto)o;
                return new Product { Name = dto.Name, ProductType = dto.ProductType, BenefitType = dto.BenefitType, BenefitValue = dto.BenefitValue };
            });
            mapper.Setup(m => m.Map(It.IsAny<UpdateProductDto>(), It.IsAny<Product>()))
                .Returns((UpdateProductDto dto, Product p) => { p.Name = dto.Name; return p; });
            mapper.Setup(m => m.Map<ProductVo>(It.IsAny<object>())).Returns((object o) =>
            {
                var p = (Product)o;
                return new ProductVo { VoId = p.Id, VoName = p.Name, VoCategoryId = p.CategoryId };
            });
            mapper.Setup(m => m.Map<List<ProductVo>>(It.IsAny<object>()))
                .Returns((object o) => ((IEnumerable<Product>)o).Select(p => mapper.Object.Map<ProductVo>(p)).ToList());
            mapper.Setup(m => m.Map<List<ProductListItemVo>>(It.IsAny<object>()))
                .Returns((object o) => ((IEnumerable<Product>)o).Select(p => new ProductListItemVo { VoId = p.Id, VoName = p.Name }).ToList());
            mapper.Setup(m => m.Map<ProductCategoryVo>(It.IsAny<object>())).Returns((object o) =>
            {
                var c = (ProductCategory)o;
                return new ProductCategoryVo { VoId = c.Id, VoName = c.Name };
            });
            mapper.Setup(m => m.Map<List<ProductCategoryVo>>(It.IsAny<object>()))
                .Returns((object o) => ((IEnumerable<ProductCategory>)o).Select(c => mapper.Object.Map<ProductCategoryVo>(c)).ToList());
            mapper.Setup(m => m.Map<OrderVo>(It.IsAny<object>())).Returns((object o) =>
            {
                var order = (Order)o;
                return new OrderVo { VoId = order.Id, VoOrderNo = order.OrderNo, VoUserId = order.UserId };
            });
            mapper.Setup(m => m.Map<List<OrderVo>>(It.IsAny<object>()))
                .Returns((object o) => ((IEnumerable<Order>)o).Select(order => mapper.Object.Map<OrderVo>(order)).ToList());
            mapper.Setup(m => m.Map<List<OrderListItemVo>>(It.IsAny<object>()))
                .Returns((object o) => ((IEnumerable<Order>)o).Select(order => new OrderListItemVo { VoId = order.Id, VoOrderNo = order.OrderNo }).ToList());
            Product = new ProductService(mapper.Object, Products.Object, Categories.Object, Orders.Object, Mock.Of<IAttachmentUrlResolver>());
            Order = new OrderService(mapper.Object, Orders.Object, Products.Object, users.Object, Mock.Of<IBaseRepository<CoinTransaction>>(),
                Product, Mock.Of<IUserBenefitService>(), Mock.Of<ICoinService>(), Mock.Of<IPaymentPasswordService>(), Mock.Of<IAttachmentUrlResolver>());
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = 1, UserName = Secret });
            var localizer = new Mock<IStringLocalizer<Errors>>();
            localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, true));
            Controller = new ShopController(Product, Order, Mock.Of<IUserBenefitService>(), Mock.Of<IUserInventoryService>(),
                Mock.Of<IUserBrowseHistoryService>(), current.Object, localizer.Object, Mock.Of<IProductReviewService>());
        }

        public void Dispose() => AppSettingsTool.Configuration = _previous;
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        public Serilog.Core.Logger Logger { get; }
        public string[] Lines => Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate) RuntimeLoggingConfiguration.Configure(config, new ConfigurationBuilder().Build(), environment, "api", Output, Output);
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(Output));
            Logger = config.CreateLogger(); Log.Logger = Logger;
        }
        public void AssertQuiet() => Assert.Equal("", Output.ToString());
        public void AssertSingle(string code, string level)
        {
            Assert.Single(Lines);
            Assert.Contains(code, Output.ToString());
            Assert.Contains(level, Output.ToString());
            AssertSafe();
        }
        public void AssertSafe()
        {
            Assert.DoesNotContain(Secret, Output.ToString());
            Assert.DoesNotContain("runtime.unclassified", Output.ToString());
        }
        public void Clear() => Output.GetStringBuilder().Clear();
        public void Dispose() { Log.Logger = _previous; Logger.Dispose(); Output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
