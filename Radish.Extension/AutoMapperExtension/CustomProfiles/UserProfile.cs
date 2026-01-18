using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>用户实体和视图模型对象映射配置</summary>
public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserVo>()
            .ForMember(a => a.Uuid, o => o.MapFrom(d => d.Id))
            .ForMember(a => a.VoLoginName, o => o.MapFrom(d => d.LoginName))
            .ForMember(a => a.VoUserName, o => o.MapFrom(d => d.UserName))
            .ForMember(a => a.VoUserEmail, o => o.MapFrom(d => d.UserEmail))
            .ForMember(a => a.VoLoginPassword, o => o.MapFrom(d => d.LoginPassword))
            .ForMember(a => a.VoUserRealName, o => o.MapFrom(d => d.UserRealName))
            .ForMember(a => a.VoUserSex, o => o.MapFrom(d => d.UserSex))
            .ForMember(a => a.VoUserAge, o => o.MapFrom(d => d.UserAge))
            .ForMember(a => a.VoUserBirth, o => o.MapFrom(d => d.UserBirth))
            .ForMember(a => a.VoUserAddress, o => o.MapFrom(d => d.UserAddress))
            .ForMember(a => a.VoStatusCode, o => o.MapFrom(d => d.StatusCode))
            .ForMember(a => a.VoCreateTime, o => o.MapFrom(d => d.CreateTime))
            .ForMember(a => a.VoUpdateTime, o => o.MapFrom(d => d.UpdateTime))
            .ForMember(a => a.VoCriticalModifyTime, o => o.MapFrom(d => d.CriticalModifyTime))
            .ForMember(a => a.VoLastErrorTime, o => o.MapFrom(d => d.LastErrorTime))
            .ForMember(a => a.VoErrorCount, o => o.MapFrom(d => d.ErrorCount))
            .ForMember(a => a.VoIsEnable, o => o.MapFrom(d => d.IsEnable))
            .ForMember(a => a.VoIsDeleted, o => o.MapFrom(d => d.IsDeleted))
            .ForMember(a => a.VoDepartmentId, o => o.MapFrom(d => d.DepartmentId))
            .ForMember(a => a.VoDepartmentName, o => o.MapFrom(d => d.DepartmentName))
            .ForMember(a => a.VoTenantId, o => o.MapFrom(d => d.TenantId))
            .ForMember(a => a.VoRoleIds, o => o.MapFrom(d => d.RoleIds))
            .ForMember(a => a.VoRoleNames, o => o.MapFrom(d => d.RoleNames))
            .ForMember(a => a.VoDepartmentIds, o => o.MapFrom(d => d.DepartmentIds))
            .ForMember(a => a.VoRemark, o => o.MapFrom(d => d.Remark));

        CreateMap<UserVo, User>()
            .ForMember(a => a.Id, o => o.MapFrom(d => d.Uuid))
            .ForMember(a => a.LoginName, o => o.MapFrom(d => d.VoLoginName))
            .ForMember(a => a.UserName, o => o.MapFrom(d => d.VoUserName))
            .ForMember(a => a.UserEmail, o => o.MapFrom(d => d.VoUserEmail))
            .ForMember(a => a.LoginPassword, o => o.MapFrom(d => d.VoLoginPassword))
            .ForMember(a => a.UserRealName, o => o.MapFrom(d => d.VoUserRealName))
            .ForMember(a => a.UserSex, o => o.MapFrom(d => d.VoUserSex))
            .ForMember(a => a.UserAge, o => o.MapFrom(d => d.VoUserAge))
            .ForMember(a => a.UserBirth, o => o.MapFrom(d => d.VoUserBirth))
            .ForMember(a => a.UserAddress, o => o.MapFrom(d => d.VoUserAddress))
            .ForMember(a => a.StatusCode, o => o.MapFrom(d => d.VoStatusCode))
            .ForMember(a => a.CreateTime, o => o.MapFrom(d => d.VoCreateTime))
            .ForMember(a => a.UpdateTime, o => o.MapFrom(d => d.VoUpdateTime))
            .ForMember(a => a.CriticalModifyTime, o => o.MapFrom(d => d.VoCriticalModifyTime))
            .ForMember(a => a.LastErrorTime, o => o.MapFrom(d => d.VoLastErrorTime))
            .ForMember(a => a.ErrorCount, o => o.MapFrom(d => d.VoErrorCount))
            .ForMember(a => a.IsEnable, o => o.MapFrom(d => d.VoIsEnable))
            .ForMember(a => a.IsDeleted, o => o.MapFrom(d => d.VoIsDeleted))
            .ForMember(a => a.DepartmentId, o => o.MapFrom(d => d.VoDepartmentId))
            .ForMember(a => a.DepartmentName, o => o.MapFrom(d => d.VoDepartmentName))
            .ForMember(a => a.TenantId, o => o.MapFrom(d => d.VoTenantId))
            .ForMember(a => a.RoleIds, o => o.MapFrom(d => d.VoRoleIds))
            .ForMember(a => a.RoleNames, o => o.MapFrom(d => d.VoRoleNames))
            .ForMember(a => a.DepartmentIds, o => o.MapFrom(d => d.VoDepartmentIds))
            .ForMember(a => a.Remark, o => o.MapFrom(d => d.VoRemark));

        // User → UserMentionVo（用于@提及功能的用户搜索）
        CreateMap<User, UserMentionVo>()
            .ForMember(dest => dest.VoDisplayName, opt => opt.MapFrom(src => src.UserRealName))
            .ForMember(dest => dest.VoAvatar, opt => opt.MapFrom(src => (string?)null)); // 暂无头像字段

        // UserVo → UserMentionVo（用于从Service层返回的UserVo转换）
        CreateMap<UserVo, UserMentionVo>()
            .ForMember(dest => dest.VoId, opt => opt.MapFrom(src => src.Uuid))
            .ForMember(dest => dest.VoUserName, opt => opt.MapFrom(src => src.VoUserName))
            .ForMember(dest => dest.VoDisplayName, opt => opt.MapFrom(src => src.VoUserRealName))
            .ForMember(dest => dest.VoAvatar, opt => opt.MapFrom(src => (string?)null)); // 暂无头像字段

        // User → CurrentUserVo（用于获取当前用户信息）
        RecognizeDestinationPrefixes("Vo");
        CreateMap<User, CurrentUserVo>()
            .ForMember(dest => dest.VoUserId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.VoUserName, opt => opt.MapFrom(src => src.UserName))
            .ForMember(dest => dest.VoAvatarUrl, opt => opt.MapFrom(src => (string?)null)) // 暂无头像字段
            .ForMember(dest => dest.VoAvatarThumbnailUrl, opt => opt.MapFrom(src => (string?)null)); // 暂无头像字段
    }
}
