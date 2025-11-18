using AutoMapper;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>自定义关系映射配置</summary>
public class CustomProfile : Profile
{
    public CustomProfile()
    {
        // 识别前缀
        // RecognizePrefixes("Vo");
        // CreateMap<Role, RoleVo>();
        // Use CreateMap... Etc. here (Profile methods are the same as configuration methods)
    }
}