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
            .ForMember(a => a.VoLoName, o => o.MapFrom(d => d.LoginName))
            .ForMember(a => a.VoUsName, o => o.MapFrom(d => d.UserName))
            .ForMember(a => a.VoUsEmail, o => o.MapFrom(d => d.UserEmail))
            .ForMember(a => a.VoLoPwd, o => o.MapFrom(d => d.LoginPassword))
            .ForMember(a => a.VoReNa, o => o.MapFrom(d => d.UserRealName))
            .ForMember(a => a.VoSexDo, o => o.MapFrom(d => d.UserSex))
            .ForMember(a => a.VoAgeDo, o => o.MapFrom(d => d.UserAge))
            .ForMember(a => a.VoBiTh, o => o.MapFrom(d => d.UserBirth))
            .ForMember(a => a.VoAdRes, o => o.MapFrom(d => d.UserAddress))
            .ForMember(a => a.VoStaCo, o => o.MapFrom(d => d.StatusCode))
            .ForMember(a => a.VoCreateTime, o => o.MapFrom(d => d.CreateTime))
            .ForMember(a => a.VoUpdateTime, o => o.MapFrom(d => d.UpdateTime))
            .ForMember(a => a.VoCrModifyTime, o => o.MapFrom(d => d.CriticalModifyTime))
            .ForMember(a => a.VoLaErrTime, o => o.MapFrom(d => d.LastErrorTime))
            .ForMember(a => a.VoErrorCount, o => o.MapFrom(d => d.ErrorCount))
            .ForMember(a => a.VoIsEnable, o => o.MapFrom(d => d.IsEnable))
            .ForMember(a => a.VoIsDeleted, o => o.MapFrom(d => d.IsDeleted))
            .ForMember(a => a.VoDeId, o => o.MapFrom(d => d.DepartmentId))
            .ForMember(a => a.VoDeNa, o => o.MapFrom(d => d.DepartmentName))
            .ForMember(a => a.VoTenId, o => o.MapFrom(d => d.TenantId))
            .ForMember(a => a.VoRoIds, o => o.MapFrom(d => d.RoleIds))
            .ForMember(a => a.VoRoNas, o => o.MapFrom(d => d.RoleNames))
            .ForMember(a => a.VoDeIds, o => o.MapFrom(d => d.DepartmentIds))
            .ForMember(a => a.VoRemark, o => o.MapFrom(d => d.Remark));

        CreateMap<UserVo, User>()
            .ForMember(a => a.Id, o => o.MapFrom(d => d.Uuid))
            .ForMember(a => a.LoginName, o => o.MapFrom(d => d.VoLoName))
            .ForMember(a => a.UserName, o => o.MapFrom(d => d.VoUsName))
            .ForMember(a => a.UserEmail, o => o.MapFrom(d => d.VoUsEmail))
            .ForMember(a => a.LoginPassword, o => o.MapFrom(d => d.VoLoPwd))
            .ForMember(a => a.UserRealName, o => o.MapFrom(d => d.VoReNa))
            .ForMember(a => a.UserSex, o => o.MapFrom(d => d.VoSexDo))
            .ForMember(a => a.UserAge, o => o.MapFrom(d => d.VoAgeDo))
            .ForMember(a => a.UserBirth, o => o.MapFrom(d => d.VoBiTh))
            .ForMember(a => a.UserAddress, o => o.MapFrom(d => d.VoAdRes))
            .ForMember(a => a.StatusCode, o => o.MapFrom(d => d.VoStaCo))
            .ForMember(a => a.CreateTime, o => o.MapFrom(d => d.VoCreateTime))
            .ForMember(a => a.UpdateTime, o => o.MapFrom(d => d.VoUpdateTime))
            .ForMember(a => a.CriticalModifyTime, o => o.MapFrom(d => d.VoCrModifyTime))
            .ForMember(a => a.LastErrorTime, o => o.MapFrom(d => d.VoLaErrTime))
            .ForMember(a => a.ErrorCount, o => o.MapFrom(d => d.VoErrorCount))
            .ForMember(a => a.IsEnable, o => o.MapFrom(d => d.VoIsEnable))
            .ForMember(a => a.IsDeleted, o => o.MapFrom(d => d.VoIsDeleted))
            .ForMember(a => a.DepartmentId, o => o.MapFrom(d => d.VoDeId))
            .ForMember(a => a.DepartmentName, o => o.MapFrom(d => d.VoDeNa))
            .ForMember(a => a.TenantId, o => o.MapFrom(d => d.VoTenId))
            .ForMember(a => a.RoleIds, o => o.MapFrom(d => d.VoRoIds))
            .ForMember(a => a.RoleNames, o => o.MapFrom(d => d.VoRoNas))
            .ForMember(a => a.DepartmentIds, o => o.MapFrom(d => d.VoDeIds))
            .ForMember(a => a.Remark, o => o.MapFrom(d => d.VoRemark));

        // User → UserMentionVo（用于@提及功能的用户搜索）
        CreateMap<User, UserMentionVo>()
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.UserRealName))
            .ForMember(dest => dest.Avatar, opt => opt.MapFrom(src => (string?)null)); // 暂无头像字段

        // UserVo → UserMentionVo（用于从Service层返回的UserVo转换）
        CreateMap<UserVo, UserMentionVo>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Uuid))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.VoUsName))
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.VoReNa))
            .ForMember(dest => dest.Avatar, opt => opt.MapFrom(src => (string?)null)); // 暂无头像字段

        // User → VoCurrentUser（用于获取当前用户信息）
        CreateMap<User, VoCurrentUser>()
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.UserName))
            .ForMember(dest => dest.TenantId, opt => opt.MapFrom(src => src.TenantId))
            .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => (string?)null)) // 暂无头像字段
            .ForMember(dest => dest.AvatarThumbnailUrl, opt => opt.MapFrom(src => (string?)null)); // 暂无头像字段
    }
}
