namespace Radish.Common.OptionTool;

/// <summary>
/// 文件存储扩展名与大小限制的统一判定策略。
/// </summary>
public static class FileStoragePolicy
{
    public static bool IsAllowedExtension(FileStorageOptions options, string extension)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(extension) ||
            extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ContainsExtension(options.AllowedExtensions.Image, extension) ||
               ContainsExtension(options.AllowedExtensions.Document, extension) ||
               ContainsExtension(options.AllowedExtensions.Video, extension) ||
               ContainsExtension(options.AllowedExtensions.Audio, extension);
    }

    public static bool IsImageExtension(FileStorageOptions options, string extension)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ContainsExtension(options.AllowedExtensions.Image, extension) &&
               !extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAllowedForBusinessType(
        FileStorageOptions options,
        string extension,
        bool requiresImage)
    {
        ArgumentNullException.ThrowIfNull(options);
        return IsAllowedExtension(options, extension) &&
               (!requiresImage || IsImageExtension(options, extension));
    }

    public static long GetMaxFileSize(FileStorageOptions options, string businessType, string extension)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (businessType.Equals("Avatar", StringComparison.OrdinalIgnoreCase))
        {
            return options.MaxFileSize.Avatar;
        }

        if (ContainsExtension(options.AllowedExtensions.Image, extension))
        {
            return options.MaxFileSize.Image;
        }

        if (ContainsExtension(options.AllowedExtensions.Document, extension))
        {
            return options.MaxFileSize.Document;
        }

        if (ContainsExtension(options.AllowedExtensions.Video, extension))
        {
            return options.MaxFileSize.Video;
        }

        if (ContainsExtension(options.AllowedExtensions.Audio, extension))
        {
            return options.MaxFileSize.Audio;
        }

        return options.MaxFileSize.Document;
    }

    private static bool ContainsExtension(IEnumerable<string> extensions, string extension)
    {
        return extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
