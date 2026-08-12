using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Extension.AutoMapperExtension.CustomProfiles;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Profiles;

public class ShopProfileTest
{
    [Fact]
    public void ProductWriteMappings_ShouldNeverChangeSaleState()
    {
        var configuration = new MapperConfiguration(
            cfg => cfg.AddProfile<ShopProfile>(),
            NullLoggerFactory.Instance);
        var mapper = configuration.CreateMapper();
        var product = new Product
        {
            Id = 1001,
            Name = "已上架商品",
            CategoryId = "effect",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.CoinCard,
            IsOnSale = true
        };

        mapper.Map(new UpdateProductDto
        {
            Id = product.Id,
            Name = "新名称",
            CategoryId = product.CategoryId,
            ProductType = product.ProductType,
            ConsumableType = product.ConsumableType,
            ExpectedVersion = 0
        }, product);

        Assert.True(product.IsOnSale);
        Assert.Equal("新名称", product.Name);
        Assert.False(mapper.Map<Product>(new CreateProductDto
        {
            Name = "新商品",
            CategoryId = "effect",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.CoinCard
        }).IsOnSale);
    }

    [Fact]
    public void UserInventoryMapping_ShouldExposeRelatedProductId()
    {
        const long sourceProductId = 2042219067430928384;

        var configuration = new MapperConfiguration(
            cfg => cfg.AddProfile<ShopProfile>(),
            NullLoggerFactory.Instance);
        var mapper = configuration.CreateMapper();

        var inventory = new UserInventory
        {
            Id = 7001,
            UserId = 9527,
            ConsumableType = ConsumableType.CoinCard,
            ItemValue = "100",
            ItemName = "萝卜币红包",
            Quantity = 3,
            SourceProductId = sourceProductId
        };

        var result = mapper.Map<UserInventoryVo>(inventory);

        Assert.Equal(sourceProductId, result.VoSourceProductId);
    }
}
