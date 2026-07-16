using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Shared.Constants;

namespace Radish.Api.Authorization;

/// <summary>
/// 普通与分片上传共用的附件业务类型白名单及后台权限策略。
/// </summary>
internal static class AttachmentUploadAuthorization
{
    public static async Task<AttachmentUploadAccessResult> EvaluateAsync(
        CurrentUser currentUser,
        IUserService userService,
        string? businessType)
    {
        if (!AttachmentBusinessTypes.TryNormalize(businessType, out var normalizedBusinessType))
        {
            return AttachmentUploadAccessResult.Unsupported();
        }

        var requiredPermissions = GetRequiredPermissions(normalizedBusinessType);
        if (requiredPermissions.Length == 0 || currentUser.IsSystemOrAdmin())
        {
            return AttachmentUploadAccessResult.Allowed(normalizedBusinessType);
        }

        if (currentUser.Roles.Count == 0)
        {
            return AttachmentUploadAccessResult.Forbidden(normalizedBusinessType);
        }

        var permissionKeys = await userService.GetPermissionKeysByRolesAsync(currentUser.Roles);
        return requiredPermissions.Any(requiredPermission =>
            permissionKeys.Contains(requiredPermission, StringComparer.OrdinalIgnoreCase))
            ? AttachmentUploadAccessResult.Allowed(normalizedBusinessType)
            : AttachmentUploadAccessResult.Forbidden(normalizedBusinessType);
    }

    private static string[] GetRequiredPermissions(string businessType)
    {
        return businessType switch
        {
            AttachmentBusinessTypes.Sticker =>
            [
                ConsolePermissions.StickersCreate,
                ConsolePermissions.StickersEdit,
                ConsolePermissions.StickersBatchUpload
            ],
            AttachmentBusinessTypes.StickerCover =>
            [
                ConsolePermissions.StickersCreate,
                ConsolePermissions.StickersEdit
            ],
            AttachmentBusinessTypes.CategoryIcon or AttachmentBusinessTypes.CategoryCover =>
            [
                ConsolePermissions.CategoriesCreate,
                ConsolePermissions.CategoriesEdit
            ],
            AttachmentBusinessTypes.ProductIcon or AttachmentBusinessTypes.ProductCover =>
            [
                ConsolePermissions.ProductsCreate,
                ConsolePermissions.ProductsEdit
            ],
            AttachmentBusinessTypes.SiteFavicon => [ConsolePermissions.SystemConfigEdit],
            _ => []
        };
    }
}

internal sealed record AttachmentUploadAccessResult(
    bool IsSupported,
    bool IsAuthorized,
    string? NormalizedBusinessType)
{
    public static AttachmentUploadAccessResult Unsupported() => new(false, false, null);

    public static AttachmentUploadAccessResult Forbidden(string businessType) => new(true, false, businessType);

    public static AttachmentUploadAccessResult Allowed(string businessType) => new(true, true, businessType);
}
