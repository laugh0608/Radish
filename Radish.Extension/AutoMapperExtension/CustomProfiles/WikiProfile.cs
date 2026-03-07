using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>Wiki 实体与视图模型映射</summary>
public class WikiProfile : Profile
{
    public WikiProfile()
    {
        RecognizeDestinationPrefixes("Vo");
        CreateMap<WikiDocument, WikiDocumentVo>();
        CreateMap<WikiDocument, WikiDocumentDetailVo>();
        CreateMap<WikiDocument, WikiDocumentTreeNodeVo>()
            .ForMember(dest => dest.VoChildren, opt => opt.Ignore());

        RecognizePrefixes("Vo");
        CreateMap<WikiDocumentVo, WikiDocument>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore());
    }
}
