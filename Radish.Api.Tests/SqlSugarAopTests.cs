using System;
using System.IO;
using System.Reflection;
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
    [Fact(DisplayName = "MarkdownContent 参数应省略正文预览")]
    public void FormatParameters_ShouldOmitMarkdownContentPreview()
    {
        var parameters = new[]
        {
            new SugarParameter("@MarkdownContent", "# 标题\n这里是正文内容"),
            new SugarParameter("@Title", "文档标题")
        };

        var formatted = InvokePrivate<string>("FormatParameters", new object[] { parameters, new SqlAopLogOptions() });

        formatted.ShouldContain("@MarkdownContent=<text len=");
        formatted.ShouldContain("omitted>");
        formatted.ShouldContain("@Title=文档标题");
        formatted.ShouldNotContain("preview=");
        formatted.ShouldNotContain("这里是正文内容");
    }

    [Fact(DisplayName = "SQL 模板中的 MarkdownContent 正文应被省略")]
    public void SanitizeSqlText_ShouldOmitInlineMarkdownContent()
    {
        const string sql = "UPDATE `WikiDocument` SET `MarkdownContent`='# 标题\n这里是正文内容',`Title`='文档标题' WHERE `Id`=1";

        var sanitized = InvokePrivate<string>("SanitizeSqlText", new object[] { sql, new SqlAopLogOptions() });

        sanitized.ShouldContain("`MarkdownContent`='<text len=");
        sanitized.ShouldContain("omitted>'");
        sanitized.ShouldContain("`Title`='文档标题'");
        sanitized.ShouldNotContain("这里是正文内容");
    }

    [Fact(DisplayName = "超长 SQL 字符串字面量应被统一省略")]
    public void SanitizeSqlText_ShouldOmitLargeStringLiteral()
    {
        var largeLiteral = new string('a', 300);
        var sql = $"INSERT INTO `Demo` (`Payload`) VALUES ('{largeLiteral}')";

        var sanitized = InvokePrivate<string>("SanitizeSqlText", new object[] { sql, new SqlAopLogOptions() });

        sanitized.ShouldContain("'<text len=300 omitted>'");
        sanitized.ShouldNotContain(largeLiteral);
    }

    [Fact(DisplayName = "配置 SkipTables 后应跳过指定表日志")]
    public void ShouldLog_ShouldRespectSkipTables()
    {
        var options = new SqlAopLogOptions
        {
            SkipTables = ["WikiDocument", "WikiDocumentRevision"]
        };

        var shouldLog = InvokePrivate<bool>("ShouldLog", new object[] { options, "System", "WikiDocumentRevision", "Update" });

        shouldLog.ShouldBeFalse();
    }

    [Fact(DisplayName = "配置关闭 Query 后不记录查询日志")]
    public void ShouldLog_ShouldRespectQuerySwitch()
    {
        var options = new SqlAopLogOptions
        {
            LogQuery = false
        };

        var shouldLog = InvokePrivate<bool>("ShouldLog", new object[] { options, "System", "AnyTable", "Query" });

        shouldLog.ShouldBeFalse();
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

    private static T InvokePrivate<T>(string methodName, params object[] args)
    {
        var method = typeof(SqlSugarAop).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        var result = method.Invoke(null, args);
        result.ShouldBeOfType<T>();
        return (T)result;
    }
}
