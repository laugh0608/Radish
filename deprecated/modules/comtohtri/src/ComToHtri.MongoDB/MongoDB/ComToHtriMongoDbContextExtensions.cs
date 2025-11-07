using Volo.Abp;
using Volo.Abp.MongoDB;

namespace ComToHtri.MongoDB;

public static class ComToHtriMongoDbContextExtensions
{
    public static void ConfigureComToHtri(
        this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));
    }
}
