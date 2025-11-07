using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace ComToHtri.MongoDB;

[ConnectionStringName(ComToHtriDbProperties.ConnectionStringName)]
public class ComToHtriMongoDbContext : AbpMongoDbContext, IComToHtriMongoDbContext
{
    /* Add mongo collections here. Example:
     * public IMongoCollection<Question> Questions => Collection<Question>();
     */

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.ConfigureComToHtri();
    }
}
