namespace Radish.Common.CoreTool;

/// <summary>
/// 规范化物理路径后的同根/子路径判定。
/// </summary>
public static class PathContainmentTool
{
    public static bool IsSameOrDescendant(string rootPath, string candidatePath)
    {
        var root = Path.GetFullPath(rootPath);
        var candidate = Path.GetFullPath(candidatePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(root, candidate, comparison))
        {
            return true;
        }

        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : $"{root}{Path.DirectorySeparatorChar}";
        return candidate.StartsWith(rootWithSeparator, comparison);
    }
}
