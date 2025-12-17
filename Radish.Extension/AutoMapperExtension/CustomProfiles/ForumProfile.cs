using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>论坛实体和视图模型对象映射配置</summary>
public class ForumProfile : Profile
{
    public ForumProfile()
    {
        // Category -> CategoryVo
        CreateMap<Category, CategoryVo>();
        CreateMap<CategoryVo, Category>();

        // Tag -> TagVo
        CreateMap<Tag, TagVo>();
        CreateMap<TagVo, Tag>();

        // Post -> PostVo
        CreateMap<Post, PostVo>()
            .ForMember(dest => dest.CategoryName, opt => opt.Ignore()) // 需要在 Service 中手动填充
            .ForMember(dest => dest.Tags, opt => opt.Ignore());         // 需要在 Service 中手动填充
        CreateMap<PostVo, Post>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore()); // 避免从 VO 覆盖租户 ID

        // Comment -> CommentVo
        CreateMap<Comment, CommentVo>()
            .ForMember(dest => dest.Children, opt => opt.Ignore())  // 树形结构在 Service 中构建
            .ForMember(dest => dest.IsLiked, opt => opt.Ignore());  // 点赞状态在 Service 中动态填充
        CreateMap<CommentVo, Comment>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore()); // 避免从 VO 覆盖租户 ID

        // UserCommentLike -> UserCommentLikeVo
        CreateMap<UserCommentLike, UserCommentLikeVo>();
        CreateMap<UserCommentLikeVo, UserCommentLike>();
    }
}
