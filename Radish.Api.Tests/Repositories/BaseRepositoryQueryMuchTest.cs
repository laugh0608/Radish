using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Common.CoreTool;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class BaseRepositoryQueryMuchTest
{
    [Fact]
    public async Task QueryMuchAsync_Should_Reuse_JoinAliases_For_TenantWhere_And_Select()
    {
        new ServiceCollection().ConfigureApplication();
        using SqlSugarScope database = new(new ConnectionConfig
        {
            ConfigId = "main",
            DbType = DbType.Sqlite,
            ConnectionString = "Data Source=:memory:",
            IsAutoCloseConnection = false,
            InitKeyType = InitKeyType.Attribute
        });
        database.CodeFirst.InitTables<Product, ProductCategory, User>();

        await database.Insertable(new ProductCategory
        {
            Id = "effect",
            Name = "效果"
        }).ExecuteCommandAsync(TestContext.Current.CancellationToken);
        await database.Insertable(new User
        {
            Id = 101,
            UserName = "同租户创建者",
            UserEmail = "same-tenant@radish.test",
            LoginPassword = "not-used",
            TenantId = 0,
            IsEnable = true
        }).ExecuteCommandAsync(TestContext.Current.CancellationToken);
        await database.Insertable(new User
        {
            Id = 102,
            UserName = "跨租户创建者",
            UserEmail = "cross-tenant@radish.test",
            LoginPassword = "not-used",
            TenantId = 30001,
            IsEnable = true
        }).ExecuteCommandAsync(TestContext.Current.CancellationToken);
        await database.Insertable(new Product
        {
            Id = 201,
            Name = "可见商品",
            CategoryId = "effect",
            CreateId = 101,
            TenantId = 0
        }).ExecuteCommandAsync(TestContext.Current.CancellationToken);
        await database.Insertable(new Product
        {
            Id = 202,
            Name = "跨租户商品",
            CategoryId = "effect",
            CreateId = 102,
            TenantId = 30001
        }).ExecuteCommandAsync(TestContext.Current.CancellationToken);

        UnitOfWorkManage unitOfWork = new(database, NullLogger<UnitOfWorkManage>.Instance);
        BaseRepository<Product> repository = new(unitOfWork);

        List<ProductJoinProjection> result = await repository
            .QueryMuchAsync<Product, ProductCategory, User, ProductJoinProjection>(
                (product, category, creator) => new object[]
                {
                    JoinType.Left, product.CategoryId == category.Id,
                    JoinType.Left, product.CreateId == creator.Id
                },
                (source, productCategory, owner) => new ProductJoinProjection
                {
                    ProductId = source.Id,
                    CategoryName = productCategory.Name,
                    CreatorName = owner.UserName
                },
                (item, categoryItem, userItem) =>
                    item.Id > 0 &&
                    categoryItem.IsEnabled &&
                    userItem.IsEnable);

        ProductJoinProjection projection = Assert.Single(result);
        Assert.Equal(201, projection.ProductId);
        Assert.Equal("效果", projection.CategoryName);
        Assert.Equal("同租户创建者", projection.CreatorName);
    }

    private sealed class ProductJoinProjection
    {
        public long ProductId { get; init; }
        public string CategoryName { get; init; } = string.Empty;
        public string CreatorName { get; init; } = string.Empty;
    }
}
