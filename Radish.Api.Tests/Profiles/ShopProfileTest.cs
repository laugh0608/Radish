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
