using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using Radish.Api.Services;
using Radish.Auth.OpenIddict;
using System.Threading.Tasks;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ClientApplicationQueryServiceTest
{
    [Fact]
    public async Task QueryAsync_Should_Filter_Search_Order_And_Page_In_Database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AuthOpenIddictDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AuthOpenIddictDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        dbContext.Set<OpenIddictEntityFrameworkCoreApplication>().AddRange(
            new OpenIddictEntityFrameworkCoreApplication
            {
                Id = "2",
                ClientId = "beta-client",
                DisplayName = "Beta",
                Properties = "{\"IsDeleted\":\"false\"}"
            },
            new OpenIddictEntityFrameworkCoreApplication
            {
                Id = "1",
                ClientId = "alpha-client",
                DisplayName = "Alpha",
                Properties = "{\"IsDeleted\":\"false\"}"
            },
            new OpenIddictEntityFrameworkCoreApplication
            {
                Id = "3",
                ClientId = "deleted-client",
                DisplayName = "Deleted",
                Properties = "{\"IsDeleted\":\"true\"}"
            });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new ClientApplicationQueryService(dbContext);
        var firstPage = await service.QueryAsync(
            1,
            1,
            "CLIENT",
            TestContext.Current.CancellationToken);
        var secondPage = await service.QueryAsync(
            2,
            1,
            "client",
            TestContext.Current.CancellationToken);

        Assert.Equal(2, firstPage.Total);
        Assert.Equal(new[] { "1" }, firstPage.ApplicationIds);
        Assert.Equal(new[] { "2" }, secondPage.ApplicationIds);
    }
}
