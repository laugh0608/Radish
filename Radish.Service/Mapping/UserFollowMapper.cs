using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class UserFollowMapper
{
    // UserFollow → UserFollowVo
    [MapProperty(nameof(UserFollow.Id), nameof(UserFollowVo.VoId))]
    [MapProperty(nameof(UserFollow.FollowerUserId), nameof(UserFollowVo.VoFollowerUserId))]
    [MapProperty(nameof(UserFollow.FollowingUserId), nameof(UserFollowVo.VoFollowingUserId))]
    [MapProperty(nameof(UserFollow.FollowTime), nameof(UserFollowVo.VoFollowTime))]
    [MapProperty(nameof(UserFollow.TenantId), nameof(UserFollowVo.VoTenantId))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserFollowVo? ToUserFollowVo(UserFollow? source);

    public static partial List<UserFollowVo> ToUserFollowVoList(IEnumerable<UserFollow>? source);

}
