using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class UserFollowRepositoryTest
{
    [Fact]
    public async Task QueryPairIncludingDeletedAsync_ShouldReturnSoftDeletedRelationship()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-user-follow-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);

        try
        {
            db.CodeFirst.InitTables<UserFollow>();
            await db.Insertable(new UserFollow
            {
                Id = 501,
                TenantId = 0,
                FollowerUserId = 10001,
                FollowingUserId = 20002,
                FollowTime = DateTime.UtcNow,
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
                DeletedBy = "alice",
                CreateTime = DateTime.UtcNow,
                CreateBy = "alice",
                CreateId = 10001
            }).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            var repository = new UserFollowRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

            var result = await repository.QueryPairIncludingDeletedAsync(10001, 20002, 0);

            Assert.NotNull(result);
            Assert.True(result.IsDeleted);
            Assert.Equal(501, result.Id);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static SqlSugarScope CreateClient(string path)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }
}
