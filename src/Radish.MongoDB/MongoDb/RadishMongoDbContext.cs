using Volo.Abp.Data;
using Volo.Abp.MongoDB;
using MongoDB.Driver;
using Radish.Books;

namespace Radish.MongoDB;

[ConnectionStringName("Default")] // 配置该上下文数据库连接使用哪个连接字符串
public class RadishMongoDbContext : AbpMongoDbContext
{

    /* Add mongo collections here. Example:
     * public IMongoCollection<Question> Questions => Collection<Question>();
     */
    
    public IMongoCollection<Book> Books => Collection<Book>();

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        //builder.Entity<YourEntity>(b =>
        //{
        //    //...
        //});
    }
}
