using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>论坛实体和视图模型对象映射配置</summary>
public class ForumProfile : Profile
{
    public ForumProfile()
    {
        // Category -> CategoryVo (使用前缀识别自动映射)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Category, CategoryVo>();
        RecognizePrefixes("Vo");
        CreateMap<CategoryVo, Category>();

        // Tag -> TagVo (使用前缀识别自动映射)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Tag, TagVo>();
        RecognizePrefixes("Vo");
        CreateMap<TagVo, Tag>();

        // Post -> PostVo
        CreateMap<Post, PostVo>()
            .ForMember(dest => dest.CategoryName, opt => opt.Ignore()) // 需要在 Service 中手动填充
            .ForMember(dest => dest.Tags, opt => opt.Ignore());         // 需要在 Service 中手动填充
        CreateMap<PostVo, Post>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore()); // 避免从 VO 覆盖租户 ID

        // Comment -> CommentVo (使用前缀识别 + 手动配置特殊字段)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Comment, CommentVo>()
            .ForMember(dest => dest.VoChildren, opt => opt.Ignore())  // 树形结构在 Service 中构建
            .ForMember(dest => dest.VoIsLiked, opt => opt.Ignore());  // 点赞状态在 Service 中动态填充
        RecognizePrefixes("Vo");
        CreateMap<CommentVo, Comment>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore()); // 避免从 VO 覆盖租户 ID

        // UserCommentLike -> UserCommentLikeVo
        CreateMap<UserCommentLike, UserCommentLikeVo>();
        CreateMap<UserCommentLikeVo, UserCommentLike>();

        // CommentHighlight -> CommentHighlightVo
        CreateMap<CommentHighlight, CommentHighlightVo>();
        CreateMap<CommentHighlightVo, CommentHighlight>();
    }
}
