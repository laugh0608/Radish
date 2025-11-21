using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>用户实体和视图模型对象映射配置</summary>
public class UserProfile : Profile
{
    public UserProfile()
    {
        // Id -> Uuid
        CreateMap<User, UserVo>().ForMember(a =>
            a.Uuid, o =>
            o.MapFrom(d => d.Id));
        CreateMap<UserVo, User>().ForMember(a =>
            a.Id, o =>
            o.MapFrom(d => d.Uuid));
        // LoginName -> VoLoName
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoLoName, o =>
            o.MapFrom(d => d.LoginName));
        CreateMap<UserVo, User>().ForMember(a =>
            a.LoginName, o =>
            o.MapFrom(d => d.VoLoName));
        // UserName -> VoUsName
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoUsName, o =>
            o.MapFrom(d => d.UserName));
        CreateMap<UserVo, User>().ForMember(a =>
            a.UserName, o =>
            o.MapFrom(d => d.VoUsName));
        // UserEmail -> VoUsEmail
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoUsEmail, o =>
            o.MapFrom(d => d.UserEmail));
        CreateMap<UserVo, User>().ForMember(a =>
            a.UserEmail, o =>
            o.MapFrom(d => d.VoUsEmail));
        // LoginPassword -> VoLoPwd
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoLoPwd, o =>
            o.MapFrom(d => d.LoginPassword));
        CreateMap<UserVo, User>().ForMember(a =>
            a.LoginPassword, o =>
            o.MapFrom(d => d.VoLoPwd));
        // UserRealName -> VoReNa
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoReNa, o =>
            o.MapFrom(d => d.UserRealName));
        CreateMap<UserVo, User>().ForMember(a =>
            a.UserRealName, o =>
            o.MapFrom(d => d.VoReNa));
        // UserSex -> VoSexDo
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoSexDo, o =>
            o.MapFrom(d => d.UserSex));
        CreateMap<UserVo, User>().ForMember(a =>
            a.UserSex, o =>
            o.MapFrom(d => d.VoSexDo));
        // UserAge -> VoAgeDo
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoAgeDo, o =>
            o.MapFrom(d => d.UserAge));
        CreateMap<UserVo, User>().ForMember(a =>
            a.UserAge, o =>
            o.MapFrom(d => d.VoAgeDo));
        // UserBirth -> VoBiTh
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoBiTh, o =>
            o.MapFrom(d => d.UserBirth));
        CreateMap<UserVo, User>().ForMember(a =>
            a.UserBirth, o =>
            o.MapFrom(d => d.VoBiTh));
        // UserAddress -> VoAdRes
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoAdRes, o =>
            o.MapFrom(d => d.UserAddress));
        CreateMap<UserVo, User>().ForMember(a =>
            a.UserAddress, o =>
            o.MapFrom(d => d.VoAdRes));
        // StatusCode -> VoStaCo
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoStaCo, o =>
            o.MapFrom(d => d.StatusCode));
        CreateMap<UserVo, User>().ForMember(a =>
            a.StatusCode, o =>
            o.MapFrom(d => d.VoStaCo));
        // CreateTime -> VoCreateTime
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoCreateTime, o =>
            o.MapFrom(d => d.CreateTime));
        CreateMap<UserVo, User>().ForMember(a =>
            a.CreateTime, o =>
            o.MapFrom(d => d.VoCreateTime));
        // UpdateTime -> VoUpdateTime
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoUpdateTime, o =>
            o.MapFrom(d => d.UpdateTime));
        CreateMap<UserVo, User>().ForMember(a =>
            a.UpdateTime, o =>
            o.MapFrom(d => d.VoUpdateTime));
        // CriticalModifyTime -> VoCrModifyTime
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoCrModifyTime, o =>
            o.MapFrom(d => d.CriticalModifyTime));
        CreateMap<UserVo, User>().ForMember(a =>
            a.CriticalModifyTime, o =>
            o.MapFrom(d => d.VoCrModifyTime));
        // LastErrorTime -> VoLaErrTime
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoLaErrTime, o =>
            o.MapFrom(d => d.LastErrorTime));
        CreateMap<UserVo, User>().ForMember(a =>
            a.LastErrorTime, o =>
            o.MapFrom(d => d.VoLaErrTime));
        // ErrorCount -> VoErrorCount
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoErrorCount, o =>
            o.MapFrom(d => d.ErrorCount));
        CreateMap<UserVo, User>().ForMember(a =>
            a.ErrorCount, o =>
            o.MapFrom(d => d.VoErrorCount));
        // IsEnable -> VoIsEnable
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoIsEnable, o =>
            o.MapFrom(d => d.IsEnable));
        CreateMap<UserVo, User>().ForMember(a =>
            a.IsEnable, o =>
            o.MapFrom(d => d.VoIsEnable));
        // IsDeleted -> VoIsDeleted
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoIsDeleted, o =>
            o.MapFrom(d => d.IsDeleted));
        CreateMap<UserVo, User>().ForMember(a =>
            a.IsDeleted, o =>
            o.MapFrom(d => d.VoIsDeleted));
        // DepartmentId -> VoDeId
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoDeId, o =>
            o.MapFrom(d => d.DepartmentId));
        CreateMap<UserVo, User>().ForMember(a =>
            a.DepartmentId, o =>
            o.MapFrom(d => d.VoDeId));
        // DepartmentName -> VoDeNa
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoDeNa, o =>
            o.MapFrom(d => d.DepartmentName));
        CreateMap<UserVo, User>().ForMember(a =>
            a.DepartmentName, o =>
            o.MapFrom(d => d.VoDeNa));
        // TenantId -> VoTenId
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoTenId, o =>
            o.MapFrom(d => d.TenantId));
        CreateMap<UserVo, User>().ForMember(a =>
            a.TenantId, o =>
            o.MapFrom(d => d.VoTenId));
        // RoleIds -> VoRoIds
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoRoIds, o =>
            o.MapFrom(d => d.RoleIds));
        CreateMap<UserVo, User>().ForMember(a =>
            a.RoleIds, o =>
            o.MapFrom(d => d.VoRoIds));
        // RoleNames -> VoRoNas
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoRoNas, o =>
            o.MapFrom(d => d.RoleNames));
        CreateMap<UserVo, User>().ForMember(a =>
            a.RoleNames, o =>
            o.MapFrom(d => d.VoRoNas));
        // DepartmentIds -> VoDeIds
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoDeIds, o =>
            o.MapFrom(d => d.DepartmentIds));
        CreateMap<UserVo, User>().ForMember(a =>
            a.DepartmentIds, o =>
            o.MapFrom(d => d.VoDeIds));
        // Remark -> VoRemark
        CreateMap<User, UserVo>().ForMember(a =>
            a.VoRemark, o =>
            o.MapFrom(d => d.Remark));
        CreateMap<UserVo, User>().ForMember(a =>
            a.Remark, o =>
            o.MapFrom(d => d.VoRemark));
    }
}