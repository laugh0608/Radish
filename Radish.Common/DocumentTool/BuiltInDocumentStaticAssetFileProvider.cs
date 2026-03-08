using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Radish.Common.DocumentTool;

/// <summary>
/// 固定文档静态资源文件提供器，仅暴露允许的静态资源文件。
/// </summary>
public sealed class BuiltInDocumentStaticAssetFileProvider(IFileProvider innerProvider) : IFileProvider
{
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        return NotFoundDirectoryContents.Singleton;
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var normalizedSubpath = NormalizeSubpath(subpath);
        if (normalizedSubpath == null || !BuiltInDocumentStaticAssetPolicy.IsAllowedAssetPath(normalizedSubpath))
        {
            return new NotFoundFileInfo(subpath);
        }

        var fileInfo = innerProvider.GetFileInfo(normalizedSubpath);
        return fileInfo.Exists && !fileInfo.IsDirectory
            ? fileInfo
            : new NotFoundFileInfo(normalizedSubpath);
    }

    public IChangeToken Watch(string filter)
    {
        return NullChangeToken.Singleton;
    }

    private static string? NormalizeSubpath(string? subpath)
    {
        if (string.IsNullOrWhiteSpace(subpath))
        {
            return null;
        }

        var normalized = subpath.Replace('\\', '/').Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 || segments.Any(static segment => segment is "." or ".."))
        {
            return null;
        }

        return string.Join('/', segments);
    }
}
