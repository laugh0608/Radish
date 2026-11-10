using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class WikiMapper
{
    // WikiDocumentVo → WikiDocument
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static WikiDocument? ToWikiDocument(WikiDocumentVo? source)
    {
        if (source is null) return null;
        var target = MapWikiDocumentVoToWikiDocument(source);
        target.Visibility = NormalizeVisibility(source.VoVisibility);
        target.AllowedRoles = SerializeAccessList(source.VoAllowedRoles);
        target.AllowedPermissions = SerializeAccessList(source.VoAllowedPermissions);
        return target;
    }

    [MapperIgnoreTarget(nameof(WikiDocument.TenantId))]
    [MapProperty(nameof(WikiDocumentVo.VoTitle), nameof(WikiDocument.Title))]
    [MapProperty(nameof(WikiDocumentVo.VoSlug), nameof(WikiDocument.Slug))]
    [MapperIgnoreTarget(nameof(WikiDocument.MarkdownContent))]
    [MapProperty(nameof(WikiDocumentVo.VoSummary), nameof(WikiDocument.Summary))]
    [MapProperty(nameof(WikiDocumentVo.VoCoverAttachmentId), nameof(WikiDocument.CoverAttachmentId))]
    [MapProperty(nameof(WikiDocumentVo.VoParentId), nameof(WikiDocument.ParentId))]
    [MapProperty(nameof(WikiDocumentVo.VoSort), nameof(WikiDocument.Sort))]
    [MapProperty(nameof(WikiDocumentVo.VoStatus), nameof(WikiDocument.Status))]
    [MapperIgnoreTarget(nameof(WikiDocument.Visibility))]
    [MapperIgnoreTarget(nameof(WikiDocument.AllowedRoles))]
    [MapperIgnoreTarget(nameof(WikiDocument.AllowedPermissions))]
    [MapProperty(nameof(WikiDocumentVo.VoSourceType), nameof(WikiDocument.SourceType))]
    [MapProperty(nameof(WikiDocumentVo.VoSourcePath), nameof(WikiDocument.SourcePath))]
    [MapProperty(nameof(WikiDocumentVo.VoVersion), nameof(WikiDocument.Version))]
    [MapProperty(nameof(WikiDocumentVo.VoGovernanceVersion), nameof(WikiDocument.GovernanceVersion))]
    [MapperIgnoreTarget(nameof(WikiDocument.OwnerUserId))]
    [MapperIgnoreTarget(nameof(WikiDocument.ActiveDraftId))]
    [MapProperty(nameof(WikiDocumentVo.VoPublishedAt), nameof(WikiDocument.PublishedAt))]
    [MapProperty(nameof(WikiDocumentVo.VoIsDeleted), nameof(WikiDocument.IsDeleted))]
    [MapProperty(nameof(WikiDocumentVo.VoDeletedAt), nameof(WikiDocument.DeletedAt))]
    [MapProperty(nameof(WikiDocumentVo.VoDeletedBy), nameof(WikiDocument.DeletedBy))]
    [MapProperty(nameof(WikiDocumentVo.VoCreateTime), nameof(WikiDocument.CreateTime))]
    [MapperIgnoreTarget(nameof(WikiDocument.CreateBy))]
    [MapperIgnoreTarget(nameof(WikiDocument.CreateId))]
    [MapProperty(nameof(WikiDocumentVo.VoModifyTime), nameof(WikiDocument.ModifyTime))]
    [MapperIgnoreTarget(nameof(WikiDocument.ModifyBy))]
    [MapperIgnoreTarget(nameof(WikiDocument.ModifyId))]
    [MapProperty(nameof(WikiDocumentVo.VoId), nameof(WikiDocument.Id))]
    [UserMapping(Default = false)]
    private static partial WikiDocument MapWikiDocumentVoToWikiDocument(WikiDocumentVo source);

    public static partial List<WikiDocument> ToWikiDocumentList(IEnumerable<WikiDocumentVo>? source);

    // WikiDocument → WikiDocumentDetailVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static WikiDocumentDetailVo? ToWikiDocumentDetailVo(WikiDocument? source)
    {
        if (source is null) return null;
        var target = MapWikiDocumentToWikiDocumentDetailVo(source);
        target.VoVisibility = NormalizeVisibility(source.Visibility);
        target.VoAllowedRoles = ParseAccessList(source.AllowedRoles);
        target.VoAllowedPermissions = ParseAccessList(source.AllowedPermissions);
        return target;
    }

    [MapProperty(nameof(WikiDocument.Id), nameof(WikiDocumentDetailVo.VoId))]
    [MapProperty(nameof(WikiDocument.Title), nameof(WikiDocumentDetailVo.VoTitle))]
    [MapProperty(nameof(WikiDocument.Slug), nameof(WikiDocumentDetailVo.VoSlug))]
    [MapProperty(nameof(WikiDocument.Summary), nameof(WikiDocumentDetailVo.VoSummary))]
    [MapProperty(nameof(WikiDocument.MarkdownContent), nameof(WikiDocumentDetailVo.VoMarkdownContent))]
    [MapProperty(nameof(WikiDocument.CoverAttachmentId), nameof(WikiDocumentDetailVo.VoCoverAttachmentId))]
    [MapProperty(nameof(WikiDocument.ParentId), nameof(WikiDocumentDetailVo.VoParentId))]
    [MapProperty(nameof(WikiDocument.Sort), nameof(WikiDocumentDetailVo.VoSort))]
    [MapProperty(nameof(WikiDocument.Status), nameof(WikiDocumentDetailVo.VoStatus))]
    [MapperIgnoreTarget(nameof(WikiDocumentDetailVo.VoVisibility))]
    [MapperIgnoreTarget(nameof(WikiDocumentDetailVo.VoAllowedRoles))]
    [MapperIgnoreTarget(nameof(WikiDocumentDetailVo.VoAllowedPermissions))]
    [MapProperty(nameof(WikiDocument.SourceType), nameof(WikiDocumentDetailVo.VoSourceType))]
    [MapProperty(nameof(WikiDocument.SourcePath), nameof(WikiDocumentDetailVo.VoSourcePath))]
    [MapProperty(nameof(WikiDocument.Version), nameof(WikiDocumentDetailVo.VoVersion))]
    [MapProperty(nameof(WikiDocument.GovernanceVersion), nameof(WikiDocumentDetailVo.VoGovernanceVersion))]
    [MapProperty(nameof(WikiDocument.PublishedAt), nameof(WikiDocumentDetailVo.VoPublishedAt))]
    [MapProperty(nameof(WikiDocument.IsDeleted), nameof(WikiDocumentDetailVo.VoIsDeleted))]
    [MapProperty(nameof(WikiDocument.DeletedAt), nameof(WikiDocumentDetailVo.VoDeletedAt))]
    [MapProperty(nameof(WikiDocument.DeletedBy), nameof(WikiDocumentDetailVo.VoDeletedBy))]
    [MapProperty(nameof(WikiDocument.CreateTime), nameof(WikiDocumentDetailVo.VoCreateTime))]
    [MapProperty(nameof(WikiDocument.ModifyTime), nameof(WikiDocumentDetailVo.VoModifyTime))]
    [UserMapping(Default = false)]
    private static partial WikiDocumentDetailVo MapWikiDocumentToWikiDocumentDetailVo(WikiDocument source);

    public static partial List<WikiDocumentDetailVo> ToWikiDocumentDetailVoList(IEnumerable<WikiDocument>? source);

    // WikiDocument → WikiDocumentTreeNodeVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static WikiDocumentTreeNodeVo? ToWikiDocumentTreeNodeVo(WikiDocument? source)
    {
        if (source is null) return null;
        var target = MapWikiDocumentToWikiDocumentTreeNodeVo(source);
        target.VoVisibility = NormalizeVisibility(source.Visibility);
        return target;
    }

    [MapProperty(nameof(WikiDocument.Id), nameof(WikiDocumentTreeNodeVo.VoId))]
    [MapProperty(nameof(WikiDocument.Title), nameof(WikiDocumentTreeNodeVo.VoTitle))]
    [MapProperty(nameof(WikiDocument.Slug), nameof(WikiDocumentTreeNodeVo.VoSlug))]
    [MapProperty(nameof(WikiDocument.ParentId), nameof(WikiDocumentTreeNodeVo.VoParentId))]
    [MapProperty(nameof(WikiDocument.Sort), nameof(WikiDocumentTreeNodeVo.VoSort))]
    [MapProperty(nameof(WikiDocument.Status), nameof(WikiDocumentTreeNodeVo.VoStatus))]
    [MapperIgnoreTarget(nameof(WikiDocumentTreeNodeVo.VoVisibility))]
    [MapperIgnoreTarget(nameof(WikiDocumentTreeNodeVo.VoChildren))]
    [UserMapping(Default = false)]
    private static partial WikiDocumentTreeNodeVo MapWikiDocumentToWikiDocumentTreeNodeVo(WikiDocument source);

    public static partial List<WikiDocumentTreeNodeVo> ToWikiDocumentTreeNodeVoList(IEnumerable<WikiDocument>? source);

    // WikiDocument → WikiDocumentVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static WikiDocumentVo? ToWikiDocumentVo(WikiDocument? source)
    {
        if (source is null) return null;
        var target = MapWikiDocumentToWikiDocumentVo(source);
        target.VoVisibility = NormalizeVisibility(source.Visibility);
        target.VoAllowedRoles = ParseAccessList(source.AllowedRoles);
        target.VoAllowedPermissions = ParseAccessList(source.AllowedPermissions);
        return target;
    }

    [MapProperty(nameof(WikiDocument.Id), nameof(WikiDocumentVo.VoId))]
    [MapProperty(nameof(WikiDocument.Title), nameof(WikiDocumentVo.VoTitle))]
    [MapProperty(nameof(WikiDocument.Slug), nameof(WikiDocumentVo.VoSlug))]
    [MapProperty(nameof(WikiDocument.Summary), nameof(WikiDocumentVo.VoSummary))]
    [MapProperty(nameof(WikiDocument.CoverAttachmentId), nameof(WikiDocumentVo.VoCoverAttachmentId))]
    [MapProperty(nameof(WikiDocument.ParentId), nameof(WikiDocumentVo.VoParentId))]
    [MapProperty(nameof(WikiDocument.Sort), nameof(WikiDocumentVo.VoSort))]
    [MapProperty(nameof(WikiDocument.Status), nameof(WikiDocumentVo.VoStatus))]
    [MapperIgnoreTarget(nameof(WikiDocumentVo.VoVisibility))]
    [MapperIgnoreTarget(nameof(WikiDocumentVo.VoAllowedRoles))]
    [MapperIgnoreTarget(nameof(WikiDocumentVo.VoAllowedPermissions))]
    [MapProperty(nameof(WikiDocument.SourceType), nameof(WikiDocumentVo.VoSourceType))]
    [MapProperty(nameof(WikiDocument.SourcePath), nameof(WikiDocumentVo.VoSourcePath))]
    [MapProperty(nameof(WikiDocument.Version), nameof(WikiDocumentVo.VoVersion))]
    [MapProperty(nameof(WikiDocument.GovernanceVersion), nameof(WikiDocumentVo.VoGovernanceVersion))]
    [MapProperty(nameof(WikiDocument.PublishedAt), nameof(WikiDocumentVo.VoPublishedAt))]
    [MapProperty(nameof(WikiDocument.IsDeleted), nameof(WikiDocumentVo.VoIsDeleted))]
    [MapProperty(nameof(WikiDocument.DeletedAt), nameof(WikiDocumentVo.VoDeletedAt))]
    [MapProperty(nameof(WikiDocument.DeletedBy), nameof(WikiDocumentVo.VoDeletedBy))]
    [MapProperty(nameof(WikiDocument.CreateTime), nameof(WikiDocumentVo.VoCreateTime))]
    [MapProperty(nameof(WikiDocument.ModifyTime), nameof(WikiDocumentVo.VoModifyTime))]
    [UserMapping(Default = false)]
    private static partial WikiDocumentVo MapWikiDocumentToWikiDocumentVo(WikiDocument source);

    public static partial List<WikiDocumentVo> ToWikiDocumentVoList(IEnumerable<WikiDocument>? source);

    private const char AccessListDelimiter = '|';
    private static int NormalizeVisibility(int visibility)
    {
        return visibility is >= (int)WikiDocumentVisibilityEnum.Public and <= (int)WikiDocumentVisibilityEnum.Restricted
            ? visibility
            : (int)WikiDocumentVisibilityEnum.Authenticated;
    }
    private static List<string> ParseAccessList(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return rawValue
            .Split(AccessListDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    private static string? SerializeAccessList(IEnumerable<string>? values)
    {
        if (values == null)
        {
            return null;
        }

        var normalized = values
            .Select(value => value?.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count == 0
            ? null
            : $"{AccessListDelimiter}{string.Join(AccessListDelimiter, normalized)}{AccessListDelimiter}";
    }

}
