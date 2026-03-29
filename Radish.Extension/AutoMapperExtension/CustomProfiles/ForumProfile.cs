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

        // Post -> PostVo (使用前缀识别 + 手动配置特殊字段)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Post, PostVo>()
            .ForMember(dest => dest.VoCategoryName, opt => opt.Ignore()) // 需要在 Service 中手动填充
            .ForMember(dest => dest.VoTags, opt => opt.Ignore())          // 需要在 Service 中手动填充
            .ForMember(dest => dest.VoQuestion, opt => opt.Ignore())      // 需要在 Service 中手动填充
            .ForMember(dest => dest.VoLottery, opt => opt.Ignore());      // 需要在 Service 中手动填充
        RecognizePrefixes("Vo");
        CreateMap<PostVo, Post>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore()); // 避免从 VO 覆盖租户 ID

        // PostQuestion -> PostQuestionVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<PostQuestion, PostQuestionVo>()
            .ForMember(dest => dest.VoAnswers, opt => opt.Ignore());

        // PostAnswer -> PostAnswerVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<PostAnswer, PostAnswerVo>()
            .ForMember(dest => dest.VoAnswerId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.VoAuthorAvatarUrl, opt => opt.Ignore());

        // PostPoll -> PostPollVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<PostPoll, PostPollVo>()
            .ForMember(dest => dest.VoPollId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.VoOptions, opt => opt.Ignore())
            .ForMember(dest => dest.VoHasVoted, opt => opt.Ignore())
            .ForMember(dest => dest.VoSelectedOptionId, opt => opt.Ignore());

        // PostPollOption -> PostPollOptionVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<PostPollOption, PostPollOptionVo>()
            .ForMember(dest => dest.VoOptionId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.VoVotePercent, opt => opt.Ignore());

        // PostLottery -> PostLotteryVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<PostLottery, PostLotteryVo>()
            .ForMember(dest => dest.VoLotteryId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.VoWinners, opt => opt.Ignore());

        // PostLotteryWinner -> PostLotteryWinnerVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<PostLotteryWinner, PostLotteryWinnerVo>();

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

        // PostEditHistory -> PostEditHistoryVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<PostEditHistory, PostEditHistoryVo>();
        RecognizePrefixes("Vo");
        CreateMap<PostEditHistoryVo, PostEditHistory>();

        // CommentEditHistory -> CommentEditHistoryVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<CommentEditHistory, CommentEditHistoryVo>();
        RecognizePrefixes("Vo");
        CreateMap<CommentEditHistoryVo, CommentEditHistory>();

        // StickerGroup -> StickerGroupVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<StickerGroup, StickerGroupVo>()
            .ForMember(dest => dest.VoStickers, opt => opt.Ignore())
            .ForMember(dest => dest.VoStickerCount, opt => opt.Ignore())
            .ForMember(dest => dest.VoCoverImageUrl, opt => opt.Ignore());
        RecognizePrefixes("Vo");
        CreateMap<StickerGroupVo, StickerGroup>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore());

        // Sticker -> StickerVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Sticker, StickerVo>()
            .ForMember(dest => dest.VoImageUrl, opt => opt.Ignore())
            .ForMember(dest => dest.VoThumbnailUrl, opt => opt.Ignore());
        RecognizePrefixes("Vo");
        CreateMap<StickerVo, Sticker>();

        // Reaction -> ReactionSummaryVo（聚合结果中的字段允许按前缀自动映射）
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Reaction, ReactionSummaryVo>();
    }
}
