namespace Radish.Common.CoreTool;

/// <summary>
/// 应用路径工具
/// </summary>
public static class AppPathTool
{
    /// <summary>
    /// 获取解决方案根目录（包含 Radish.slnx）或应用基目录
    /// </summary>
    public static string GetSolutionRootOrBasePath()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Radish.slnx")))
        {
            currentDir = currentDir.Parent;
        }

        return currentDir?.FullName ?? AppContext.BaseDirectory;
    }

    /// <summary>
    /// 获取统一的 DataBases 目录绝对路径
    /// </summary>
    public static string GetDataBasesPath()
    {
        return Path.Combine(GetSolutionRootOrBasePath(), "DataBases");
    }
}
