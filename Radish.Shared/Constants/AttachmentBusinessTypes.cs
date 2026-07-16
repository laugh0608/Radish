namespace Radish.Shared.Constants;

/// <summary>
/// 附件存储目录允许使用的稳定业务类型。
/// </summary>
/// <remarks>
/// 业务类型会参与存储路径构造，因此只接受这里声明的固定值，并统一规范为标准大小写。
/// </remarks>
public static class AttachmentBusinessTypes
{
    public const string General = "General";
    public const string Post = "Post";
    public const string Comment = "Comment";
    public const string Avatar = "Avatar";
    public const string Document = "Document";
    public const string Wiki = "Wiki";
    public const string Chat = "Chat";
    public const string Sticker = "Sticker";
    public const string StickerCover = "StickerCover";
    public const string SiteFavicon = "SiteFavicon";
    public const string CategoryIcon = "CategoryIcon";
    public const string CategoryCover = "CategoryCover";
    public const string ProductIcon = "ProductIcon";
    public const string ProductCover = "ProductCover";

    private static readonly IReadOnlyDictionary<string, string> CanonicalValues =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [General] = General,
            [Post] = Post,
            [Comment] = Comment,
            [Avatar] = Avatar,
            [Document] = Document,
            [Wiki] = Wiki,
            [Chat] = Chat,
            [Sticker] = Sticker,
            [StickerCover] = StickerCover,
            [SiteFavicon] = SiteFavicon,
            [CategoryIcon] = CategoryIcon,
            [CategoryCover] = CategoryCover,
            [ProductIcon] = ProductIcon,
            [ProductCover] = ProductCover
        };

    private static readonly IReadOnlySet<string> ImageOnlyValues = new HashSet<string>(
        [
            Avatar,
            Sticker,
            StickerCover,
            SiteFavicon,
            CategoryIcon,
            CategoryCover,
            ProductIcon,
            ProductCover
        ],
        StringComparer.Ordinal);

    /// <summary>
    /// 将外部输入规范为允许的稳定业务类型。
    /// </summary>
    public static bool TryNormalize(string? value, out string normalizedValue)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? General : value.Trim();
        return CanonicalValues.TryGetValue(candidate, out normalizedValue!);
    }

    /// <summary>
    /// 判断业务类型是否只允许服务端已识别的栅格图片。
    /// </summary>
    public static bool RequiresImage(string businessType)
    {
        return ImageOnlyValues.Contains(businessType);
    }
}
