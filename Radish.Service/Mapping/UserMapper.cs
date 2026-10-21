using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class UserMapper
{
    // UserTimePreference → UserTimePreferenceVo
    [MapProperty(nameof(UserTimePreference.UserId), nameof(UserTimePreferenceVo.VoUserId))]
    [MapProperty(nameof(UserTimePreference.TimeZoneId), nameof(UserTimePreferenceVo.VoTimeZoneId))]
    [MapperIgnoreTarget(nameof(UserTimePreferenceVo.VoIsCustomized))]
    [MapperIgnoreTarget(nameof(UserTimePreferenceVo.VoSystemDefaultTimeZoneId))]
    [MapperIgnoreTarget(nameof(UserTimePreferenceVo.VoDisplayFormat))]
    [MapProperty(nameof(UserTimePreference.ModifyTime), nameof(UserTimePreferenceVo.VoModifyTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserTimePreferenceVo? ToUserTimePreferenceVo(UserTimePreference? source);

    public static partial List<UserTimePreferenceVo> ToUserTimePreferenceVoList(IEnumerable<UserTimePreference>? source);

    // User → CurrentUserVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static CurrentUserVo? ToCurrentUserVo(User? source)
    {
        if (source is null) return null;
        var target = MapUserToCurrentUserVo(source);
        target.VoUserName = User.NormalizeDisplayName(source.UserName, source.Id);
        target.VoDisplayName = User.NormalizeDisplayName(source.UserName, source.Id);
        target.VoDisplayHandle = User.BuildDisplayHandle(source.UserName, source.PublicIndex, source.Id);
        return target;
    }

    [MapProperty(nameof(User.Id), nameof(CurrentUserVo.VoUserId))]
    [MapperIgnoreTarget(nameof(CurrentUserVo.VoUserName))]
    [MapperIgnoreTarget(nameof(CurrentUserVo.VoDisplayName))]
    [MapperIgnoreTarget(nameof(CurrentUserVo.VoDisplayHandle))]
    [MapProperty(nameof(User.PublicId), nameof(CurrentUserVo.VoPublicId))]
    [MapProperty(nameof(User.PublicIndex), nameof(CurrentUserVo.VoPublicIndex))]
    [MapperIgnoreTarget(nameof(CurrentUserVo.VoNickname))]
    [MapProperty(nameof(User.TenantId), nameof(CurrentUserVo.VoTenantId))]
    [MapperIgnoreTarget(nameof(CurrentUserVo.VoAvatarUrl))]
    [MapperIgnoreTarget(nameof(CurrentUserVo.VoAvatarThumbnailUrl))]
    [MapperIgnoreTarget(nameof(CurrentUserVo.VoRoles))]
    [MapperIgnoreTarget(nameof(CurrentUserVo.VoPermissions))]
    [UserMapping(Default = false)]
    private static partial CurrentUserVo MapUserToCurrentUserVo(User source);

    public static partial List<CurrentUserVo> ToCurrentUserVoList(IEnumerable<User>? source);

    // User → UserMentionVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static UserMentionVo? ToUserMentionVo(User? source)
    {
        if (source is null) return null;
        var target = MapUserToUserMentionVo(source);
        target.VoUserName = User.NormalizeDisplayName(source.UserName, source.Id);
        target.VoDisplayName = User.NormalizeDisplayName(source.UserName, source.Id);
        target.VoDisplayHandle = User.BuildDisplayHandle(source.UserName, source.PublicIndex, source.Id);
        target.VoAvatar = (string?)null;
        return target;
    }

    [MapProperty(nameof(User.Id), nameof(UserMentionVo.VoId))]
    [MapProperty(nameof(User.PublicId), nameof(UserMentionVo.VoPublicId))]
    [MapProperty(nameof(User.PublicIndex), nameof(UserMentionVo.VoPublicIndex))]
    [MapperIgnoreTarget(nameof(UserMentionVo.VoUserName))]
    [MapperIgnoreTarget(nameof(UserMentionVo.VoDisplayName))]
    [MapperIgnoreTarget(nameof(UserMentionVo.VoDisplayHandle))]
    [MapperIgnoreTarget(nameof(UserMentionVo.VoAvatar))]
    [UserMapping(Default = false)]
    private static partial UserMentionVo MapUserToUserMentionVo(User source);

    public static partial List<UserMentionVo> ToUserMentionVoList(IEnumerable<User>? source);

    // User → UserVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static UserVo? ToUserVo(User? source)
    {
        if (source is null) return null;
        var target = MapUserToUserVo(source);
        target.VoDisplayName = User.NormalizeDisplayName(source.UserName, source.Id);
        target.VoDisplayHandle = User.BuildDisplayHandle(source.UserName, source.PublicIndex, source.Id);
        target.VoRoleIds = source.RoleIds?.ToList() ?? [];
        target.VoRoleNames = source.RoleNames?.ToList() ?? [];
        target.VoDepartmentIds = source.DepartmentIds?.ToList() ?? [];
        return target;
    }

    [MapProperty(nameof(User.Id), nameof(UserVo.Uuid))]
    [MapProperty(nameof(User.PublicId), nameof(UserVo.VoPublicId))]
    [MapProperty(nameof(User.PublicIndex), nameof(UserVo.VoPublicIndex))]
    [MapperIgnoreTarget(nameof(UserVo.VoDisplayName))]
    [MapperIgnoreTarget(nameof(UserVo.VoDisplayHandle))]
    [MapProperty(nameof(User.UserName), nameof(UserVo.VoUserName))]
    [MapProperty(nameof(User.UserEmail), nameof(UserVo.VoUserEmail))]
    [MapProperty(nameof(User.UserSex), nameof(UserVo.VoUserSex))]
    [MapProperty(nameof(User.UserAge), nameof(UserVo.VoUserAge))]
    [MapProperty(nameof(User.UserBirth), nameof(UserVo.VoUserBirth))]
    [MapProperty(nameof(User.UserAddress), nameof(UserVo.VoUserAddress))]
    [MapProperty(nameof(User.StatusCode), nameof(UserVo.VoStatusCode))]
    [MapProperty(nameof(User.CreateTime), nameof(UserVo.VoCreateTime))]
    [MapProperty(nameof(User.UpdateTime), nameof(UserVo.VoUpdateTime))]
    [MapProperty(nameof(User.CriticalModifyTime), nameof(UserVo.VoCriticalModifyTime))]
    [MapProperty(nameof(User.LastErrorTime), nameof(UserVo.VoLastErrorTime))]
    [MapProperty(nameof(User.ErrorCount), nameof(UserVo.VoErrorCount))]
    [MapProperty(nameof(User.IsEnable), nameof(UserVo.VoIsEnable))]
    [MapProperty(nameof(User.IsDeleted), nameof(UserVo.VoIsDeleted))]
    [MapProperty(nameof(User.DepartmentId), nameof(UserVo.VoDepartmentId))]
    [MapProperty(nameof(User.DepartmentName), nameof(UserVo.VoDepartmentName))]
    [MapProperty(nameof(User.TenantId), nameof(UserVo.VoTenantId))]
    [MapperIgnoreTarget(nameof(UserVo.VoAvatarUrl))]
    [MapperIgnoreTarget(nameof(UserVo.VoAvatarThumbnailUrl))]
    [MapperIgnoreTarget(nameof(UserVo.VoRoleIds))]
    [MapperIgnoreTarget(nameof(UserVo.VoRoleNames))]
    [MapperIgnoreTarget(nameof(UserVo.VoDepartmentIds))]
    [MapProperty(nameof(User.Remark), nameof(UserVo.VoRemark))]
    [UserMapping(Default = false)]
    private static partial UserVo MapUserToUserVo(User source);

    public static partial List<UserVo> ToUserVoList(IEnumerable<User>? source);

    // UserBrowseHistory → UserBrowseHistoryVo
    [MapProperty(nameof(UserBrowseHistory.Id), nameof(UserBrowseHistoryVo.VoId))]
    [MapProperty(nameof(UserBrowseHistory.TargetType), nameof(UserBrowseHistoryVo.VoTargetType))]
    [MapProperty(nameof(UserBrowseHistory.TargetId), nameof(UserBrowseHistoryVo.VoTargetId))]
    [MapProperty(nameof(UserBrowseHistory.TargetSlug), nameof(UserBrowseHistoryVo.VoTargetSlug))]
    [MapProperty(nameof(UserBrowseHistory.Title), nameof(UserBrowseHistoryVo.VoTitle))]
    [MapProperty(nameof(UserBrowseHistory.Summary), nameof(UserBrowseHistoryVo.VoSummary))]
    [MapProperty(nameof(UserBrowseHistory.CoverAttachmentId), nameof(UserBrowseHistoryVo.VoCoverAttachmentId))]
    [MapperIgnoreTarget(nameof(UserBrowseHistoryVo.VoCoverImage))]
    [MapProperty(nameof(UserBrowseHistory.RoutePath), nameof(UserBrowseHistoryVo.VoRoutePath))]
    [MapProperty(nameof(UserBrowseHistory.ViewCount), nameof(UserBrowseHistoryVo.VoViewCount))]
    [MapProperty(nameof(UserBrowseHistory.LastViewTime), nameof(UserBrowseHistoryVo.VoLastViewTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserBrowseHistoryVo? ToUserBrowseHistoryVo(UserBrowseHistory? source);

    public static partial List<UserBrowseHistoryVo> ToUserBrowseHistoryVoList(IEnumerable<UserBrowseHistory>? source);

    // UserVo → User
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static User? ToUser(UserVo? source)
    {
        if (source is null) return null;
        var target = MapUserVoToUser(source);
        target.UserName = !string.IsNullOrWhiteSpace(source.VoDisplayName) ? source.VoDisplayName : source.VoUserName;
        target.RoleIds = source.VoRoleIds?.ToList() ?? [];
        target.RoleNames = source.VoRoleNames?.ToList() ?? [];
        target.DepartmentIds = source.VoDepartmentIds?.ToList() ?? [];
        return target;
    }

    [MapProperty(nameof(UserVo.VoPublicId), nameof(User.PublicId))]
    [MapProperty(nameof(UserVo.VoPublicIndex), nameof(User.PublicIndex))]
    [MapperIgnoreTarget(nameof(User.UserName))]
    [MapperIgnoreTarget(nameof(User.DisplayName))]
    [MapProperty(nameof(UserVo.VoUserEmail), nameof(User.UserEmail))]
    [MapperIgnoreTarget(nameof(User.LoginPassword))]
    [MapProperty(nameof(UserVo.VoUserSex), nameof(User.UserSex))]
    [MapProperty(nameof(UserVo.VoUserAge), nameof(User.UserAge))]
    [MapProperty(nameof(UserVo.VoUserBirth), nameof(User.UserBirth))]
    [MapProperty(nameof(UserVo.VoUserAddress), nameof(User.UserAddress))]
    [MapProperty(nameof(UserVo.VoStatusCode), nameof(User.StatusCode))]
    [MapProperty(nameof(UserVo.VoCreateTime), nameof(User.CreateTime))]
    [MapProperty(nameof(UserVo.VoUpdateTime), nameof(User.UpdateTime))]
    [MapProperty(nameof(UserVo.VoCriticalModifyTime), nameof(User.CriticalModifyTime))]
    [MapProperty(nameof(UserVo.VoLastErrorTime), nameof(User.LastErrorTime))]
    [MapProperty(nameof(UserVo.VoErrorCount), nameof(User.ErrorCount))]
    [MapProperty(nameof(UserVo.VoIsEnable), nameof(User.IsEnable))]
    [MapProperty(nameof(UserVo.VoIsDeleted), nameof(User.IsDeleted))]
    [MapProperty(nameof(UserVo.VoDepartmentId), nameof(User.DepartmentId))]
    [MapProperty(nameof(UserVo.VoDepartmentName), nameof(User.DepartmentName))]
    [MapProperty(nameof(UserVo.VoTenantId), nameof(User.TenantId))]
    [MapperIgnoreTarget(nameof(User.RoleIds))]
    [MapperIgnoreTarget(nameof(User.RoleNames))]
    [MapperIgnoreTarget(nameof(User.DepartmentIds))]
    [MapProperty(nameof(UserVo.VoRemark), nameof(User.Remark))]
    [MapProperty(nameof(UserVo.Uuid), nameof(User.Id))]
    [UserMapping(Default = false)]
    private static partial User MapUserVoToUser(UserVo source);

    public static partial List<User> ToUserList(IEnumerable<UserVo>? source);

    // UserVo → UserMentionVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static UserMentionVo? ToUserMentionVo(UserVo? source)
    {
        if (source is null) return null;
        var target = MapUserVoToUserMentionVo(source);
        target.VoAvatar = (string?)null;
        return target;
    }

    [MapProperty(nameof(UserVo.Uuid), nameof(UserMentionVo.VoId))]
    [MapProperty(nameof(UserVo.VoDisplayName), nameof(UserMentionVo.VoUserName))]
    [MapperIgnoreTarget(nameof(UserMentionVo.VoAvatar))]
    [UserMapping(Default = false)]
    private static partial UserMentionVo MapUserVoToUserMentionVo(UserVo source);

    public static partial List<UserMentionVo> ToUserMentionVoList(IEnumerable<UserVo>? source);

}
