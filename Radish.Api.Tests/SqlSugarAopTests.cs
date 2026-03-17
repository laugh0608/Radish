using System.Reflection;
using Radish.Common.OptionTool;
using Radish.Extension.AopExtension;
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

    private static T InvokePrivate<T>(string methodName, params object[] args)
    {
        var method = typeof(SqlSugarAop).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        var result = method.Invoke(null, args);
        result.ShouldBeOfType<T>();
        return (T)result;
    }
}
