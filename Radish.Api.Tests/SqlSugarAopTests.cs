using System;
using System.Data;
using System.IO;
using System.Reflection;
using Moq;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.Extension.AopExtension;
using Radish.Extension.SqlSugarExtension;
using Shouldly;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

public class SqlSugarAopTests
{
    [Fact(DisplayName = "PostgreSQL 参数中的本地时间应规范化为 UTC")]
    public void NormalizeDateTimeParametersForPostgreSql_ShouldConvertLocalDateTimeToUtc()
    {
        var localTime = new DateTime(2026, 5, 19, 15, 0, 0, DateTimeKind.Local);
        var parameters = new[]
        {
            new SugarParameter("@CreateTime", localTime)
        };
        var config = new ConnectionConfig { DbType = SqlSugar.DbType.PostgreSQL };

        PostgreSqlDateTimeParameterNormalizer.Normalize(config, parameters);

        parameters[0].Value.ShouldBeOfType<DateTime>();
        var normalized = (DateTime)parameters[0].Value;
        normalized.Kind.ShouldBe(DateTimeKind.Utc);
        normalized.ShouldBe(localTime.ToUniversalTime());
    }

    [Fact(DisplayName = "PostgreSQL 参数中的未指定时间应按 UTC 语义标记")]
    public void NormalizeDateTimeParametersForPostgreSql_ShouldMarkUnspecifiedDateTimeAsUtc()
    {
        var unspecifiedTime = new DateTime(2026, 5, 19, 15, 0, 0, DateTimeKind.Unspecified);
        var parameters = new[]
        {
            new SugarParameter("@CreateTime", unspecifiedTime)
        };
        var config = new ConnectionConfig { DbType = SqlSugar.DbType.PostgreSQL };

        PostgreSqlDateTimeParameterNormalizer.Normalize(config, parameters);

        parameters[0].Value.ShouldBeOfType<DateTime>();
        var normalized = (DateTime)parameters[0].Value;
        normalized.Kind.ShouldBe(DateTimeKind.Utc);
        normalized.ShouldBe(DateTime.SpecifyKind(unspecifiedTime, DateTimeKind.Utc));
    }

    [Theory(DisplayName = "ExtractTableName 应支持 INSERT/UPDATE/DELETE/SELECT 并归一化表名")]
    [InlineData("INSERT INTO `WikiDocument` (`Id`) VALUES (@Id)", "WikiDocument")]
    [InlineData("UPDATE `WikiDocument` SET `Title`=@Title WHERE `Id`=@Id", "WikiDocument")]
    [InlineData("DELETE FROM `WikiDocument` WHERE `Id`=@Id", "WikiDocument")]
    [InlineData("SELECT * FROM `WikiDocument` WHERE `Id`=@Id", "WikiDocument")]
    [InlineData("SELECT * FROM public.WikiDocument WHERE Id=@Id", "WikiDocument")]
    public void ExtractTableName_ShouldSupportCommonSqlPatterns(string sql, string expectedTableName)
    {
        var method = typeof(SqlSugarSetup).GetMethod("ExtractTableName", BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        var result = method.Invoke(null, [sql]);
        result.ShouldBe(expectedTableName);
    }

    [Fact(DisplayName = "SQLite PRAGMA 应按物理连接只初始化一次")]
    public void ApplySqlitePragmas_ShouldInitializeSameConnectionOnlyOnce()
    {
        var connection = new Mock<IDbConnection>();
        var command = new Mock<IDbCommand>();
        var connectionString = $"Data Source=pragma-{Guid.NewGuid():N}.db";
        connection.SetupGet(item => item.State).Returns(ConnectionState.Open);
        connection.SetupGet(item => item.ConnectionString).Returns(connectionString);
        connection.Setup(item => item.CreateCommand()).Returns(command.Object);
        command.Setup(item => item.ExecuteNonQuery()).Returns(0);
        command.Setup(item => item.ExecuteScalar()).Returns("wal");
        var config = new ConnectionConfig
        {
            ConfigId = $"pragma-{Guid.NewGuid():N}",
            ConnectionString = connectionString,
            DbType = SqlSugar.DbType.Sqlite
        };
        var method = typeof(SqlSugarSetup).GetMethod(
            "ApplySqlitePragmas",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        method.Invoke(null, [connection.Object, config]);
        method.Invoke(null, [connection.Object, config]);

        connection.Verify(item => item.CreateCommand(), Times.Exactly(3));
    }

    [Fact(DisplayName = "内容根目录中的 csproj 应优先决定日志项目名")]
    public void ResolveProjectName_ShouldPreferContentRootProjectFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"radish-log-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "Radish.Api.csproj"), "<Project />");

        try
        {
            var method = typeof(LogContextTool).GetMethod("ResolveProjectName", BindingFlags.NonPublic | BindingFlags.Static);
            method.ShouldNotBeNull();

            var result = method.Invoke(null, ["Radish", tempRoot, "Radish", tempRoot]);
            result.ShouldBe("Radish.Api");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact(DisplayName = "BaseDirectory 解析到具体项目名时不应退回泛化的 ApplicationName")]
    public void ResolveProjectName_ShouldPreferBaseDirectoryOverGenericApplicationName()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"radish-log-{Guid.NewGuid():N}");
        var baseDirectory = Path.Combine(tempRoot, "Radish.Api", "bin", "Debug", "net10.0");
        Directory.CreateDirectory(baseDirectory);
        File.WriteAllText(Path.Combine(tempRoot, "Radish.Api", "Radish.Api.csproj"), "<Project />");

        try
        {
            var method = typeof(LogContextTool).GetMethod("ResolveProjectName", BindingFlags.NonPublic | BindingFlags.Static);
            method.ShouldNotBeNull();

            var result = method.Invoke(null, ["Radish", null, "Radish", baseDirectory]);
            result.ShouldBe("Radish.Api");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

}
