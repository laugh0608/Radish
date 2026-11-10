using Radish.Service.Mapping;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Mapping;

public class ShopMappingTests
{
    [Fact]
    public void ProductWriteMappings_ShouldNeverChangeSaleState()
    {
        var product = new Product
        {
            Id = 1001,
            Name = "已上架商品",
            CategoryId = "effect",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.CoinCard,
            IsOnSale = true
        };

        ShopMapper.UpdateProduct(new UpdateProductDto
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
        Assert.False(ShopMapper.ToProduct(new CreateProductDto
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

        var result = ShopMapper.ToUserInventoryVo(inventory);

        Assert.Equal(sourceProductId, result.VoSourceProductId);
    }
}
