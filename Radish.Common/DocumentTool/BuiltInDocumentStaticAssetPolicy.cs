using System.Collections.ObjectModel;

namespace Radish.Common.DocumentTool;

/// <summary>
/// 固定文档静态资源访问策略。
/// </summary>
public static class BuiltInDocumentStaticAssetPolicy
{
    private static readonly string[] AllowedRootDirectoriesArray = ["images"];
    private static readonly HashSet<string> AllowedExtensionsSet = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp", ".ico", ".pdf", ".txt", ".json", ".yml", ".yaml"
    };

    /// <summary>
    /// 允许通过固定文档静态资源端点访问的顶级目录。
    /// </summary>
    public static IReadOnlyCollection<string> AllowedRootDirectories { get; } = new ReadOnlyCollection<string>(AllowedRootDirectoriesArray);

    /// <summary>
    /// 允许通过固定文档静态资源端点访问的文件扩展名。
    /// </summary>
    public static IReadOnlyCollection<string> AllowedExtensions { get; } = new ReadOnlyCollection<string>(AllowedExtensionsSet.OrderBy(static extension => extension, StringComparer.OrdinalIgnoreCase).ToArray());

    /// <summary>
    /// 判断路径是否为允许暴露的固定文档静态资源。
    /// </summary>
    public static bool IsAllowedAssetPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/').Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2 || !AllowedRootDirectoriesArray.Contains(segments[0], StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = Path.GetExtension(normalizedPath);
        return !string.IsNullOrWhiteSpace(extension) && AllowedExtensionsSet.Contains(extension);
    }
}
