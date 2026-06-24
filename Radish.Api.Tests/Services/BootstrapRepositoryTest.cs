using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Common;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.Models;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class BootstrapRepositoryTest
{
    [Fact]
    public async Task TryCreateFirstAdministratorAsync_ShouldSkipReservedPublicIndex()
    {
        using var harness = BootstrapRepositoryHarness.Create();
        var policy = PublicIndexReservationPolicy.FromSettings("[1000]", "{}");

        var result = await harness.Repository.TryCreateFirstAdministratorAsync(
            "Owner",
            "hash",
            "owner@radish.test",
            policy);

        Assert.Equal(BootstrapAdminCreationStatus.Created, result.Status);

        var user = harness.Db.Queryable<User>().Single(item => item.Id == result.UserId);
        Assert.Equal(1001, user.PublicIndex);
        Assert.Equal("Owner", user.UserName);
        Assert.Equal("owner@radish.test", user.UserEmail);
    }

    private sealed class BootstrapRepositoryHarness : IDisposable
    {
        private readonly string _dbPath;
        private readonly IConfiguration? _previousConfiguration;

        private BootstrapRepositoryHarness(
            string dbPath,
            SqlSugarScope db,
            IConfiguration? previousConfiguration)
        {
            _dbPath = dbPath;
            _previousConfiguration = previousConfiguration;
            Db = db;
            var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
            Repository = new BootstrapRepository(unitOfWork);
        }

        public SqlSugarScope Db { get; }

        public BootstrapRepository Repository { get; }

        public static BootstrapRepositoryHarness Create()
        {
            var previousConfiguration = AppSettingsTool.Configuration;
            AppSettingsTool.Configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MainDb"] = "Main"
                })
                .Build();

            var dbPath = Path.Combine(Path.GetTempPath(), $"radish-bootstrap-{Guid.NewGuid():N}.db");
            var db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main",
                DbType = DbType.Sqlite,
                ConnectionString = $"Data Source={dbPath}",
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });

            db.CodeFirst.InitTables<User, Role, UserRole, SystemBootstrapState>();
            return new BootstrapRepositoryHarness(dbPath, db, previousConfiguration);
        }

        public void Dispose()
        {
            Db.Dispose();
            AppSettingsTool.Configuration = _previousConfiguration!;
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }
}
