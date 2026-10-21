using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class ForumMapper
{
    // Category → CategoryVo
    [MapProperty(nameof(Category.Id), nameof(CategoryVo.VoId))]
    [MapProperty(nameof(Category.Name), nameof(CategoryVo.VoName))]
    [MapProperty(nameof(Category.Slug), nameof(CategoryVo.VoSlug))]
    [MapProperty(nameof(Category.Description), nameof(CategoryVo.VoDescription))]
    [MapProperty(nameof(Category.IconAttachmentId), nameof(CategoryVo.VoIconAttachmentId))]
    [MapperIgnoreTarget(nameof(CategoryVo.VoIcon))]
    [MapProperty(nameof(Category.CoverAttachmentId), nameof(CategoryVo.VoCoverAttachmentId))]
    [MapperIgnoreTarget(nameof(CategoryVo.VoCoverImage))]
    [MapProperty(nameof(Category.ParentId), nameof(CategoryVo.VoParentId))]
    [MapProperty(nameof(Category.Level), nameof(CategoryVo.VoLevel))]
    [MapProperty(nameof(Category.OrderSort), nameof(CategoryVo.VoOrderSort))]
    [MapProperty(nameof(Category.PostCount), nameof(CategoryVo.VoPostCount))]
    [MapProperty(nameof(Category.IsEnabled), nameof(CategoryVo.VoIsEnabled))]
    [MapProperty(nameof(Category.IsDeleted), nameof(CategoryVo.VoIsDeleted))]
    [MapProperty(nameof(Category.CreateTime), nameof(CategoryVo.VoCreateTime))]
    [MapProperty(nameof(Category.ModifyTime), nameof(CategoryVo.VoModifyTime))]
    [MapProperty(nameof(Category.CreateBy), nameof(CategoryVo.VoCreateBy))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial CategoryVo? ToCategoryVo(Category? source);

    public static partial List<CategoryVo> ToCategoryVoList(IEnumerable<Category>? source);

    // Comment → CommentVo
    [MapProperty(nameof(Comment.Id), nameof(CommentVo.VoId))]
    [MapProperty(nameof(Comment.Content), nameof(CommentVo.VoContent))]
    [MapProperty(nameof(Comment.PostId), nameof(CommentVo.VoPostId))]
    [MapperIgnoreTarget(nameof(CommentVo.VoPostPublicId))]
    [MapProperty(nameof(Comment.ParentId), nameof(CommentVo.VoParentId))]
    [MapProperty(nameof(Comment.RootId), nameof(CommentVo.VoRootId))]
    [MapProperty(nameof(Comment.ReplyToUserId), nameof(CommentVo.VoReplyToUserId))]
    [MapProperty(nameof(Comment.ReplyToCommentId), nameof(CommentVo.VoReplyToCommentId))]
    [MapProperty(nameof(Comment.ReplyToCommentSnapshot), nameof(CommentVo.VoReplyToCommentSnapshot))]
    [MapProperty(nameof(Comment.ReplyToUserName), nameof(CommentVo.VoReplyToUserName))]
    [MapProperty(nameof(Comment.Level), nameof(CommentVo.VoLevel))]
    [MapProperty(nameof(Comment.AuthorId), nameof(CommentVo.VoAuthorId))]
    [MapperIgnoreTarget(nameof(CommentVo.VoAuthorPublicId))]
    [MapProperty(nameof(Comment.AuthorName), nameof(CommentVo.VoAuthorName))]
    [MapperIgnoreTarget(nameof(CommentVo.VoAuthorAvatarUrl))]
    [MapperIgnoreTarget(nameof(CommentVo.VoAuthorAdornment))]
    [MapProperty(nameof(Comment.LikeCount), nameof(CommentVo.VoLikeCount))]
    [MapProperty(nameof(Comment.ReplyCount), nameof(CommentVo.VoReplyCount))]
    [MapProperty(nameof(Comment.ContentRevision), nameof(CommentVo.VoContentRevision))]
    [MapProperty(nameof(Comment.IsTop), nameof(CommentVo.VoIsTop))]
    [MapProperty(nameof(Comment.CreateTime), nameof(CommentVo.VoCreateTime))]
    [MapProperty(nameof(Comment.ModifyTime), nameof(CommentVo.VoModifyTime))]
    [MapperIgnoreTarget(nameof(CommentVo.VoChildren))]
    [MapperIgnoreTarget(nameof(CommentVo.VoChildrenTotal))]
    [MapperIgnoreTarget(nameof(CommentVo.VoIsLiked))]
    [MapperIgnoreTarget(nameof(CommentVo.VoIsGodComment))]
    [MapperIgnoreTarget(nameof(CommentVo.VoIsSofa))]
    [MapperIgnoreTarget(nameof(CommentVo.VoHighlightRank))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial CommentVo? ToCommentVo(Comment? source);

    public static partial List<CommentVo> ToCommentVoList(IEnumerable<Comment>? source);

    // CommentEditHistory → CommentEditHistoryVo
    [MapProperty(nameof(CommentEditHistory.Id), nameof(CommentEditHistoryVo.VoId))]
    [MapProperty(nameof(CommentEditHistory.CommentId), nameof(CommentEditHistoryVo.VoCommentId))]
    [MapProperty(nameof(CommentEditHistory.PostId), nameof(CommentEditHistoryVo.VoPostId))]
    [MapProperty(nameof(CommentEditHistory.EditSequence), nameof(CommentEditHistoryVo.VoEditSequence))]
    [MapProperty(nameof(CommentEditHistory.OldContent), nameof(CommentEditHistoryVo.VoOldContent))]
    [MapProperty(nameof(CommentEditHistory.NewContent), nameof(CommentEditHistoryVo.VoNewContent))]
    [MapProperty(nameof(CommentEditHistory.EditorId), nameof(CommentEditHistoryVo.VoEditorId))]
    [MapProperty(nameof(CommentEditHistory.EditorName), nameof(CommentEditHistoryVo.VoEditorName))]
    [MapProperty(nameof(CommentEditHistory.EditedAt), nameof(CommentEditHistoryVo.VoEditedAt))]
    [MapProperty(nameof(CommentEditHistory.CreateTime), nameof(CommentEditHistoryVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial CommentEditHistoryVo? ToCommentEditHistoryVo(CommentEditHistory? source);

    public static partial List<CommentEditHistoryVo> ToCommentEditHistoryVoList(IEnumerable<CommentEditHistory>? source);

    // CommentHighlight → CommentHighlightVo
    [MapProperty(nameof(CommentHighlight.Id), nameof(CommentHighlightVo.VoId))]
    [MapProperty(nameof(CommentHighlight.PostId), nameof(CommentHighlightVo.VoPostId))]
    [MapProperty(nameof(CommentHighlight.CommentId), nameof(CommentHighlightVo.VoCommentId))]
    [MapProperty(nameof(CommentHighlight.ParentCommentId), nameof(CommentHighlightVo.VoParentCommentId))]
    [MapProperty(nameof(CommentHighlight.HighlightType), nameof(CommentHighlightVo.VoHighlightType))]
    [MapProperty(nameof(CommentHighlight.StatDate), nameof(CommentHighlightVo.VoStatDate))]
    [MapProperty(nameof(CommentHighlight.LikeCount), nameof(CommentHighlightVo.VoLikeCount))]
    [MapProperty(nameof(CommentHighlight.Rank), nameof(CommentHighlightVo.VoRank))]
    [MapProperty(nameof(CommentHighlight.ContentSnapshot), nameof(CommentHighlightVo.VoContentSnapshot))]
    [MapProperty(nameof(CommentHighlight.AuthorId), nameof(CommentHighlightVo.VoAuthorId))]
    [MapProperty(nameof(CommentHighlight.AuthorName), nameof(CommentHighlightVo.VoAuthorName))]
    [MapProperty(nameof(CommentHighlight.IsCurrent), nameof(CommentHighlightVo.VoIsCurrent))]
    [MapProperty(nameof(CommentHighlight.CreateTime), nameof(CommentHighlightVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial CommentHighlightVo? ToCommentHighlightVo(CommentHighlight? source);

    public static partial List<CommentHighlightVo> ToCommentHighlightVoList(IEnumerable<CommentHighlight>? source);

    // Post → PostVo
    [MapProperty(nameof(Post.Id), nameof(PostVo.VoId))]
    [MapProperty(nameof(Post.Title), nameof(PostVo.VoTitle))]
    [MapProperty(nameof(Post.PublicId), nameof(PostVo.VoPublicId))]
    [MapProperty(nameof(Post.Slug), nameof(PostVo.VoSlug))]
    [MapProperty(nameof(Post.Summary), nameof(PostVo.VoSummary))]
    [MapProperty(nameof(Post.Content), nameof(PostVo.VoContent))]
    [MapProperty(nameof(Post.ContentType), nameof(PostVo.VoContentType))]
    [MapProperty(nameof(Post.CoverAttachmentId), nameof(PostVo.VoCoverAttachmentId))]
    [MapperIgnoreTarget(nameof(PostVo.VoCoverImage))]
    [MapProperty(nameof(Post.AuthorId), nameof(PostVo.VoAuthorId))]
    [MapperIgnoreTarget(nameof(PostVo.VoAuthorPublicId))]
    [MapProperty(nameof(Post.AuthorName), nameof(PostVo.VoAuthorName))]
    [MapperIgnoreTarget(nameof(PostVo.VoAuthorAvatarUrl))]
    [MapperIgnoreTarget(nameof(PostVo.VoAuthorAdornment))]
    [MapProperty(nameof(Post.CategoryId), nameof(PostVo.VoCategoryId))]
    [MapperIgnoreTarget(nameof(PostVo.VoCategoryName))]
    [MapperIgnoreTarget(nameof(PostVo.VoTags))]
    [MapperIgnoreTarget(nameof(PostVo.VoTagSlugs))]
    [MapProperty(nameof(Post.IsTop), nameof(PostVo.VoIsTop))]
    [MapProperty(nameof(Post.IsEssence), nameof(PostVo.VoIsEssence))]
    [MapProperty(nameof(Post.IsLocked), nameof(PostVo.VoIsLocked))]
    [MapProperty(nameof(Post.IsPublished), nameof(PostVo.VoIsPublished))]
    [MapProperty(nameof(Post.PublishTime), nameof(PostVo.VoPublishTime))]
    [MapProperty(nameof(Post.ViewCount), nameof(PostVo.VoViewCount))]
    [MapProperty(nameof(Post.LikeCount), nameof(PostVo.VoLikeCount))]
    [MapProperty(nameof(Post.CommentCount), nameof(PostVo.VoCommentCount))]
    [MapProperty(nameof(Post.ContentRevision), nameof(PostVo.VoContentRevision))]
    [MapProperty(nameof(Post.CollectCount), nameof(PostVo.VoCollectCount))]
    [MapperIgnoreTarget(nameof(PostVo.VoIsBookmarked))]
    [MapProperty(nameof(Post.ShareCount), nameof(PostVo.VoShareCount))]
    [MapProperty(nameof(Post.CreateTime), nameof(PostVo.VoCreateTime))]
    [MapProperty(nameof(Post.ModifyTime), nameof(PostVo.VoModifyTime))]
    [MapperIgnoreTarget(nameof(PostVo.VoLatestInteractors))]
    [MapperIgnoreTarget(nameof(PostVo.VoGodCommentId))]
    [MapperIgnoreTarget(nameof(PostVo.VoGodCommentAuthorName))]
    [MapperIgnoreTarget(nameof(PostVo.VoGodCommentContentSnapshot))]
    [MapperIgnoreTarget(nameof(PostVo.VoIsQuestion))]
    [MapperIgnoreTarget(nameof(PostVo.VoIsSolved))]
    [MapperIgnoreTarget(nameof(PostVo.VoAnswerCount))]
    [MapperIgnoreTarget(nameof(PostVo.VoQuestion))]
    [MapperIgnoreTarget(nameof(PostVo.VoHasPoll))]
    [MapperIgnoreTarget(nameof(PostVo.VoPollTotalVoteCount))]
    [MapperIgnoreTarget(nameof(PostVo.VoPollIsClosed))]
    [MapperIgnoreTarget(nameof(PostVo.VoPoll))]
    [MapperIgnoreTarget(nameof(PostVo.VoHasLottery))]
    [MapperIgnoreTarget(nameof(PostVo.VoLotteryParticipantCount))]
    [MapperIgnoreTarget(nameof(PostVo.VoLotteryIsDrawn))]
    [MapperIgnoreTarget(nameof(PostVo.VoLottery))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PostVo? ToPostVo(Post? source);

    public static partial List<PostVo> ToPostVoList(IEnumerable<Post>? source);

    // PostAnswer → PostAnswerVo
    [MapProperty(nameof(PostAnswer.Id), nameof(PostAnswerVo.VoAnswerId))]
    [MapProperty(nameof(PostAnswer.PublicId), nameof(PostAnswerVo.VoPublicId))]
    [MapProperty(nameof(PostAnswer.PostId), nameof(PostAnswerVo.VoPostId))]
    [MapProperty(nameof(PostAnswer.AuthorId), nameof(PostAnswerVo.VoAuthorId))]
    [MapperIgnoreTarget(nameof(PostAnswerVo.VoAuthorPublicId))]
    [MapProperty(nameof(PostAnswer.AuthorName), nameof(PostAnswerVo.VoAuthorName))]
    [MapperIgnoreTarget(nameof(PostAnswerVo.VoAuthorAvatarUrl))]
    [MapperIgnoreTarget(nameof(PostAnswerVo.VoAuthorAdornment))]
    [MapProperty(nameof(PostAnswer.Content), nameof(PostAnswerVo.VoContent))]
    [MapProperty(nameof(PostAnswer.IsAccepted), nameof(PostAnswerVo.VoIsAccepted))]
    [MapProperty(nameof(PostAnswer.ContentRevision), nameof(PostAnswerVo.VoContentRevision))]
    [MapProperty(nameof(PostAnswer.EditCount), nameof(PostAnswerVo.VoEditCount))]
    [MapProperty(nameof(PostAnswer.IsEnabled), nameof(PostAnswerVo.VoIsEnabled))]
    [MapperIgnoreTarget(nameof(PostAnswerVo.VoCanEdit))]
    [MapperIgnoreTarget(nameof(PostAnswerVo.VoCanDelete))]
    [MapperIgnoreTarget(nameof(PostAnswerVo.VoCanReport))]
    [MapProperty(nameof(PostAnswer.CreateTime), nameof(PostAnswerVo.VoCreateTime))]
    [MapProperty(nameof(PostAnswer.ModifyTime), nameof(PostAnswerVo.VoModifyTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PostAnswerVo? ToPostAnswerVo(PostAnswer? source);

    public static partial List<PostAnswerVo> ToPostAnswerVoList(IEnumerable<PostAnswer>? source);

    // PostEditHistory → PostEditHistoryVo
    [MapProperty(nameof(PostEditHistory.Id), nameof(PostEditHistoryVo.VoId))]
    [MapProperty(nameof(PostEditHistory.PostId), nameof(PostEditHistoryVo.VoPostId))]
    [MapProperty(nameof(PostEditHistory.EditSequence), nameof(PostEditHistoryVo.VoEditSequence))]
    [MapProperty(nameof(PostEditHistory.OldTitle), nameof(PostEditHistoryVo.VoOldTitle))]
    [MapProperty(nameof(PostEditHistory.NewTitle), nameof(PostEditHistoryVo.VoNewTitle))]
    [MapProperty(nameof(PostEditHistory.OldContent), nameof(PostEditHistoryVo.VoOldContent))]
    [MapProperty(nameof(PostEditHistory.NewContent), nameof(PostEditHistoryVo.VoNewContent))]
    [MapProperty(nameof(PostEditHistory.EditorId), nameof(PostEditHistoryVo.VoEditorId))]
    [MapProperty(nameof(PostEditHistory.EditorName), nameof(PostEditHistoryVo.VoEditorName))]
    [MapProperty(nameof(PostEditHistory.EditedAt), nameof(PostEditHistoryVo.VoEditedAt))]
    [MapProperty(nameof(PostEditHistory.CreateTime), nameof(PostEditHistoryVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PostEditHistoryVo? ToPostEditHistoryVo(PostEditHistory? source);

    public static partial List<PostEditHistoryVo> ToPostEditHistoryVoList(IEnumerable<PostEditHistory>? source);

    // PostLottery → PostLotteryVo
    [MapProperty(nameof(PostLottery.Id), nameof(PostLotteryVo.VoLotteryId))]
    [MapProperty(nameof(PostLottery.PostId), nameof(PostLotteryVo.VoPostId))]
    [MapProperty(nameof(PostLottery.PrizeName), nameof(PostLotteryVo.VoPrizeName))]
    [MapProperty(nameof(PostLottery.PrizeDescription), nameof(PostLotteryVo.VoPrizeDescription))]
    [MapProperty(nameof(PostLottery.DrawTime), nameof(PostLotteryVo.VoDrawTime))]
    [MapProperty(nameof(PostLottery.DrawnAt), nameof(PostLotteryVo.VoDrawnAt))]
    [MapProperty(nameof(PostLottery.WinnerCount), nameof(PostLotteryVo.VoWinnerCount))]
    [MapProperty(nameof(PostLottery.ParticipantCount), nameof(PostLotteryVo.VoParticipantCount))]
    [MapProperty(nameof(PostLottery.IsDrawn), nameof(PostLotteryVo.VoIsDrawn))]
    [MapperIgnoreTarget(nameof(PostLotteryVo.VoWinners))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PostLotteryVo? ToPostLotteryVo(PostLottery? source);

    public static partial List<PostLotteryVo> ToPostLotteryVoList(IEnumerable<PostLottery>? source);

    // PostLotteryWinner → PostLotteryWinnerVo
    [MapProperty(nameof(PostLotteryWinner.Id), nameof(PostLotteryWinnerVo.VoId))]
    [MapProperty(nameof(PostLotteryWinner.LotteryId), nameof(PostLotteryWinnerVo.VoLotteryId))]
    [MapProperty(nameof(PostLotteryWinner.UserId), nameof(PostLotteryWinnerVo.VoUserId))]
    [MapperIgnoreTarget(nameof(PostLotteryWinnerVo.VoUserPublicId))]
    [MapProperty(nameof(PostLotteryWinner.UserName), nameof(PostLotteryWinnerVo.VoUserName))]
    [MapProperty(nameof(PostLotteryWinner.CommentId), nameof(PostLotteryWinnerVo.VoCommentId))]
    [MapProperty(nameof(PostLotteryWinner.CommentContentSnapshot), nameof(PostLotteryWinnerVo.VoCommentContentSnapshot))]
    [MapProperty(nameof(PostLotteryWinner.DrawnAt), nameof(PostLotteryWinnerVo.VoDrawnAt))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PostLotteryWinnerVo? ToPostLotteryWinnerVo(PostLotteryWinner? source);

    public static partial List<PostLotteryWinnerVo> ToPostLotteryWinnerVoList(IEnumerable<PostLotteryWinner>? source);

    // PostPoll → PostPollVo
    [MapProperty(nameof(PostPoll.Id), nameof(PostPollVo.VoPollId))]
    [MapProperty(nameof(PostPoll.PostId), nameof(PostPollVo.VoPostId))]
    [MapProperty(nameof(PostPoll.Question), nameof(PostPollVo.VoQuestion))]
    [MapProperty(nameof(PostPoll.EndTime), nameof(PostPollVo.VoEndTime))]
    [MapProperty(nameof(PostPoll.IsClosed), nameof(PostPollVo.VoIsClosed))]
    [MapProperty(nameof(PostPoll.TotalVoteCount), nameof(PostPollVo.VoTotalVoteCount))]
    [MapperIgnoreTarget(nameof(PostPollVo.VoHasVoted))]
    [MapperIgnoreTarget(nameof(PostPollVo.VoSelectedOptionId))]
    [MapperIgnoreTarget(nameof(PostPollVo.VoOptions))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PostPollVo? ToPostPollVo(PostPoll? source);

    public static partial List<PostPollVo> ToPostPollVoList(IEnumerable<PostPoll>? source);

    // PostPollOption → PostPollOptionVo
    [MapProperty(nameof(PostPollOption.Id), nameof(PostPollOptionVo.VoOptionId))]
    [MapProperty(nameof(PostPollOption.OptionText), nameof(PostPollOptionVo.VoOptionText))]
    [MapProperty(nameof(PostPollOption.SortOrder), nameof(PostPollOptionVo.VoSortOrder))]
    [MapProperty(nameof(PostPollOption.VoteCount), nameof(PostPollOptionVo.VoVoteCount))]
    [MapperIgnoreTarget(nameof(PostPollOptionVo.VoVotePercent))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PostPollOptionVo? ToPostPollOptionVo(PostPollOption? source);

    public static partial List<PostPollOptionVo> ToPostPollOptionVoList(IEnumerable<PostPollOption>? source);

    // PostQuestion → PostQuestionVo
    [MapProperty(nameof(PostQuestion.PostId), nameof(PostQuestionVo.VoPostId))]
    [MapProperty(nameof(PostQuestion.IsSolved), nameof(PostQuestionVo.VoIsSolved))]
    [MapProperty(nameof(PostQuestion.AcceptedAnswerId), nameof(PostQuestionVo.VoAcceptedAnswerId))]
    [MapperIgnoreTarget(nameof(PostQuestionVo.VoAcceptedAnswerPublicId))]
    [MapProperty(nameof(PostQuestion.AcceptanceRevision), nameof(PostQuestionVo.VoAcceptanceRevision))]
    [MapProperty(nameof(PostQuestion.AnswerCount), nameof(PostQuestionVo.VoAnswerCount))]
    [MapperIgnoreTarget(nameof(PostQuestionVo.VoAnswers))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PostQuestionVo? ToPostQuestionVo(PostQuestion? source);

    public static partial List<PostQuestionVo> ToPostQuestionVoList(IEnumerable<PostQuestion>? source);

    // PostQuickReply → PostQuickReplyVo
    [MapProperty(nameof(PostQuickReply.Id), nameof(PostQuickReplyVo.VoId))]
    [MapProperty(nameof(PostQuickReply.PostId), nameof(PostQuickReplyVo.VoPostId))]
    [MapProperty(nameof(PostQuickReply.AuthorId), nameof(PostQuickReplyVo.VoAuthorId))]
    [MapProperty(nameof(PostQuickReply.AuthorName), nameof(PostQuickReplyVo.VoAuthorName))]
    [MapperIgnoreTarget(nameof(PostQuickReplyVo.VoAuthorAvatarUrl))]
    [MapProperty(nameof(PostQuickReply.Content), nameof(PostQuickReplyVo.VoContent))]
    [MapProperty(nameof(PostQuickReply.Status), nameof(PostQuickReplyVo.VoStatus))]
    [MapProperty(nameof(PostQuickReply.CreateTime), nameof(PostQuickReplyVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PostQuickReplyVo? ToPostQuickReplyVo(PostQuickReply? source);

    public static partial List<PostQuickReplyVo> ToPostQuickReplyVoList(IEnumerable<PostQuickReply>? source);

    // Reaction → ReactionSummaryVo
    [MapProperty(nameof(Reaction.EmojiType), nameof(ReactionSummaryVo.VoEmojiType))]
    [MapProperty(nameof(Reaction.EmojiValue), nameof(ReactionSummaryVo.VoEmojiValue))]
    [MapperIgnoreTarget(nameof(ReactionSummaryVo.VoCount))]
    [MapperIgnoreTarget(nameof(ReactionSummaryVo.VoIsReacted))]
    [MapperIgnoreTarget(nameof(ReactionSummaryVo.VoThumbnailUrl))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial ReactionSummaryVo? ToReactionSummaryVo(Reaction? source);

    public static partial List<ReactionSummaryVo> ToReactionSummaryVoList(IEnumerable<Reaction>? source);

    // Sticker → StickerVo
    [MapProperty(nameof(Sticker.Id), nameof(StickerVo.VoId))]
    [MapProperty(nameof(Sticker.GroupId), nameof(StickerVo.VoGroupId))]
    [MapProperty(nameof(Sticker.Code), nameof(StickerVo.VoCode))]
    [MapProperty(nameof(Sticker.Name), nameof(StickerVo.VoName))]
    [MapProperty(nameof(Sticker.AttachmentId), nameof(StickerVo.VoAttachmentId))]
    [MapperIgnoreTarget(nameof(StickerVo.VoImageUrl))]
    [MapperIgnoreTarget(nameof(StickerVo.VoThumbnailUrl))]
    [MapProperty(nameof(Sticker.IsAnimated), nameof(StickerVo.VoIsAnimated))]
    [MapProperty(nameof(Sticker.AllowInline), nameof(StickerVo.VoAllowInline))]
    [MapProperty(nameof(Sticker.UseCount), nameof(StickerVo.VoUseCount))]
    [MapProperty(nameof(Sticker.Sort), nameof(StickerVo.VoSort))]
    [MapProperty(nameof(Sticker.IsEnabled), nameof(StickerVo.VoIsEnabled))]
    [MapProperty(nameof(Sticker.CreateTime), nameof(StickerVo.VoCreateTime))]
    [MapProperty(nameof(Sticker.ModifyTime), nameof(StickerVo.VoModifyTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial StickerVo? ToStickerVo(Sticker? source);

    public static partial List<StickerVo> ToStickerVoList(IEnumerable<Sticker>? source);

    // StickerGroup → StickerGroupVo
    [MapProperty(nameof(StickerGroup.Id), nameof(StickerGroupVo.VoId))]
    [MapProperty(nameof(StickerGroup.Code), nameof(StickerGroupVo.VoCode))]
    [MapProperty(nameof(StickerGroup.Name), nameof(StickerGroupVo.VoName))]
    [MapProperty(nameof(StickerGroup.Description), nameof(StickerGroupVo.VoDescription))]
    [MapProperty(nameof(StickerGroup.CoverAttachmentId), nameof(StickerGroupVo.VoCoverAttachmentId))]
    [MapperIgnoreTarget(nameof(StickerGroupVo.VoCoverImageUrl))]
    [MapProperty(nameof(StickerGroup.GroupType), nameof(StickerGroupVo.VoGroupType))]
    [MapProperty(nameof(StickerGroup.IsEnabled), nameof(StickerGroupVo.VoIsEnabled))]
    [MapProperty(nameof(StickerGroup.Sort), nameof(StickerGroupVo.VoSort))]
    [MapProperty(nameof(StickerGroup.TenantId), nameof(StickerGroupVo.VoTenantId))]
    [MapperIgnoreTarget(nameof(StickerGroupVo.VoStickerCount))]
    [MapProperty(nameof(StickerGroup.CreateTime), nameof(StickerGroupVo.VoCreateTime))]
    [MapProperty(nameof(StickerGroup.ModifyTime), nameof(StickerGroupVo.VoModifyTime))]
    [MapperIgnoreTarget(nameof(StickerGroupVo.VoStickers))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial StickerGroupVo? ToStickerGroupVo(StickerGroup? source);

    public static partial List<StickerGroupVo> ToStickerGroupVoList(IEnumerable<StickerGroup>? source);

    // Tag → TagVo
    [MapProperty(nameof(Tag.Id), nameof(TagVo.VoId))]
    [MapProperty(nameof(Tag.Name), nameof(TagVo.VoName))]
    [MapProperty(nameof(Tag.Slug), nameof(TagVo.VoSlug))]
    [MapProperty(nameof(Tag.Description), nameof(TagVo.VoDescription))]
    [MapProperty(nameof(Tag.Color), nameof(TagVo.VoColor))]
    [MapProperty(nameof(Tag.SortOrder), nameof(TagVo.VoSortOrder))]
    [MapProperty(nameof(Tag.PostCount), nameof(TagVo.VoPostCount))]
    [MapProperty(nameof(Tag.IsEnabled), nameof(TagVo.VoIsEnabled))]
    [MapProperty(nameof(Tag.IsFixed), nameof(TagVo.VoIsFixed))]
    [MapProperty(nameof(Tag.IsDeleted), nameof(TagVo.VoIsDeleted))]
    [MapProperty(nameof(Tag.CreateTime), nameof(TagVo.VoCreateTime))]
    [MapProperty(nameof(Tag.ModifyTime), nameof(TagVo.VoModifyTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial TagVo? ToTagVo(Tag? source);

    public static partial List<TagVo> ToTagVoList(IEnumerable<Tag>? source);

    // UserCommentLike → UserCommentLikeVo
    [MapProperty(nameof(UserCommentLike.Id), nameof(UserCommentLikeVo.VoId))]
    [MapProperty(nameof(UserCommentLike.UserId), nameof(UserCommentLikeVo.VoUserId))]
    [MapProperty(nameof(UserCommentLike.CommentId), nameof(UserCommentLikeVo.VoCommentId))]
    [MapProperty(nameof(UserCommentLike.PostId), nameof(UserCommentLikeVo.VoPostId))]
    [MapProperty(nameof(UserCommentLike.LikedAt), nameof(UserCommentLikeVo.VoLikedAt))]
    [MapperIgnoreTarget(nameof(UserCommentLikeVo.VoUserName))]
    [MapperIgnoreTarget(nameof(UserCommentLikeVo.VoUserAvatar))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserCommentLikeVo? ToUserCommentLikeVo(UserCommentLike? source);

    public static partial List<UserCommentLikeVo> ToUserCommentLikeVoList(IEnumerable<UserCommentLike>? source);

    // CategoryVo → Category
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static Category? ToCategory(CategoryVo? source)
    {
        if (source is null) return null;
        var target = MapCategoryVoToCategory(source);
        target.Description = source.VoDescription!;
        return target;
    }

    [MapProperty(nameof(CategoryVo.VoName), nameof(Category.Name))]
    [MapProperty(nameof(CategoryVo.VoSlug), nameof(Category.Slug))]
    [MapperIgnoreTarget(nameof(Category.Description))]
    [MapProperty(nameof(CategoryVo.VoIconAttachmentId), nameof(Category.IconAttachmentId))]
    [MapProperty(nameof(CategoryVo.VoCoverAttachmentId), nameof(Category.CoverAttachmentId))]
    [MapProperty(nameof(CategoryVo.VoParentId), nameof(Category.ParentId))]
    [MapProperty(nameof(CategoryVo.VoLevel), nameof(Category.Level))]
    [MapProperty(nameof(CategoryVo.VoOrderSort), nameof(Category.OrderSort))]
    [MapProperty(nameof(CategoryVo.VoPostCount), nameof(Category.PostCount))]
    [MapProperty(nameof(CategoryVo.VoIsEnabled), nameof(Category.IsEnabled))]
    [MapProperty(nameof(CategoryVo.VoIsDeleted), nameof(Category.IsDeleted))]
    [MapProperty(nameof(CategoryVo.VoCreateTime), nameof(Category.CreateTime))]
    [MapProperty(nameof(CategoryVo.VoCreateBy), nameof(Category.CreateBy))]
    [MapperIgnoreTarget(nameof(Category.CreateId))]
    [MapProperty(nameof(CategoryVo.VoModifyTime), nameof(Category.ModifyTime))]
    [MapperIgnoreTarget(nameof(Category.ModifyBy))]
    [MapperIgnoreTarget(nameof(Category.ModifyId))]
    [MapProperty(nameof(CategoryVo.VoId), nameof(Category.Id))]
    [UserMapping(Default = false)]
    private static partial Category MapCategoryVoToCategory(CategoryVo source);

    [ObjectFactory]
    private static Category CreateCategory(CategoryVo source) => new(source.VoName);

    public static partial List<Category> ToCategoryList(IEnumerable<CategoryVo>? source);

    // CommentEditHistoryVo → CommentEditHistory
    [MapProperty(nameof(CommentEditHistoryVo.VoCommentId), nameof(CommentEditHistory.CommentId))]
    [MapProperty(nameof(CommentEditHistoryVo.VoPostId), nameof(CommentEditHistory.PostId))]
    [MapProperty(nameof(CommentEditHistoryVo.VoEditSequence), nameof(CommentEditHistory.EditSequence))]
    [MapProperty(nameof(CommentEditHistoryVo.VoOldContent), nameof(CommentEditHistory.OldContent))]
    [MapProperty(nameof(CommentEditHistoryVo.VoNewContent), nameof(CommentEditHistory.NewContent))]
    [MapProperty(nameof(CommentEditHistoryVo.VoEditorId), nameof(CommentEditHistory.EditorId))]
    [MapProperty(nameof(CommentEditHistoryVo.VoEditorName), nameof(CommentEditHistory.EditorName))]
    [MapProperty(nameof(CommentEditHistoryVo.VoEditedAt), nameof(CommentEditHistory.EditedAt))]
    [MapperIgnoreTarget(nameof(CommentEditHistory.TenantId))]
    [MapProperty(nameof(CommentEditHistoryVo.VoCreateTime), nameof(CommentEditHistory.CreateTime))]
    [MapperIgnoreTarget(nameof(CommentEditHistory.CreateBy))]
    [MapperIgnoreTarget(nameof(CommentEditHistory.CreateId))]
    [MapProperty(nameof(CommentEditHistoryVo.VoId), nameof(CommentEditHistory.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial CommentEditHistory? ToCommentEditHistory(CommentEditHistoryVo? source);

    public static partial List<CommentEditHistory> ToCommentEditHistoryList(IEnumerable<CommentEditHistoryVo>? source);

    // CommentHighlightVo → CommentHighlight
    [MapProperty(nameof(CommentHighlightVo.VoPostId), nameof(CommentHighlight.PostId))]
    [MapProperty(nameof(CommentHighlightVo.VoCommentId), nameof(CommentHighlight.CommentId))]
    [MapProperty(nameof(CommentHighlightVo.VoParentCommentId), nameof(CommentHighlight.ParentCommentId))]
    [MapProperty(nameof(CommentHighlightVo.VoHighlightType), nameof(CommentHighlight.HighlightType))]
    [MapProperty(nameof(CommentHighlightVo.VoStatDate), nameof(CommentHighlight.StatDate))]
    [MapProperty(nameof(CommentHighlightVo.VoLikeCount), nameof(CommentHighlight.LikeCount))]
    [MapProperty(nameof(CommentHighlightVo.VoRank), nameof(CommentHighlight.Rank))]
    [MapProperty(nameof(CommentHighlightVo.VoContentSnapshot), nameof(CommentHighlight.ContentSnapshot))]
    [MapProperty(nameof(CommentHighlightVo.VoAuthorId), nameof(CommentHighlight.AuthorId))]
    [MapProperty(nameof(CommentHighlightVo.VoAuthorName), nameof(CommentHighlight.AuthorName))]
    [MapProperty(nameof(CommentHighlightVo.VoIsCurrent), nameof(CommentHighlight.IsCurrent))]
    [MapperIgnoreTarget(nameof(CommentHighlight.TenantId))]
    [MapProperty(nameof(CommentHighlightVo.VoCreateTime), nameof(CommentHighlight.CreateTime))]
    [MapperIgnoreTarget(nameof(CommentHighlight.CreateBy))]
    [MapProperty(nameof(CommentHighlightVo.VoId), nameof(CommentHighlight.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial CommentHighlight? ToCommentHighlight(CommentHighlightVo? source);

    public static partial List<CommentHighlight> ToCommentHighlightList(IEnumerable<CommentHighlightVo>? source);

    // CommentVo → Comment
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static Comment? ToComment(CommentVo? source)
    {
        if (source is null) return null;
        var target = MapCommentVoToComment(source);
        target.ReplyToCommentSnapshot = source.VoReplyToCommentSnapshot!;
        target.ReplyToUserName = source.VoReplyToUserName!;
        return target;
    }

    [MapProperty(nameof(CommentVo.VoContent), nameof(Comment.Content))]
    [MapProperty(nameof(CommentVo.VoPostId), nameof(Comment.PostId))]
    [MapProperty(nameof(CommentVo.VoParentId), nameof(Comment.ParentId))]
    [MapProperty(nameof(CommentVo.VoRootId), nameof(Comment.RootId))]
    [MapProperty(nameof(CommentVo.VoReplyToUserId), nameof(Comment.ReplyToUserId))]
    [MapProperty(nameof(CommentVo.VoReplyToCommentId), nameof(Comment.ReplyToCommentId))]
    [MapperIgnoreTarget(nameof(Comment.ReplyToCommentSnapshot))]
    [MapperIgnoreTarget(nameof(Comment.ReplyToUserName))]
    [MapProperty(nameof(CommentVo.VoLevel), nameof(Comment.Level))]
    [MapperIgnoreTarget(nameof(Comment.Path))]
    [MapProperty(nameof(CommentVo.VoAuthorId), nameof(Comment.AuthorId))]
    [MapProperty(nameof(CommentVo.VoAuthorName), nameof(Comment.AuthorName))]
    [MapProperty(nameof(CommentVo.VoLikeCount), nameof(Comment.LikeCount))]
    [MapProperty(nameof(CommentVo.VoReplyCount), nameof(Comment.ReplyCount))]
    [MapperIgnoreTarget(nameof(Comment.EditCount))]
    [MapProperty(nameof(CommentVo.VoContentRevision), nameof(Comment.ContentRevision))]
    [MapProperty(nameof(CommentVo.VoIsTop), nameof(Comment.IsTop))]
    [MapperIgnoreTarget(nameof(Comment.IsEnabled))]
    [MapperIgnoreTarget(nameof(Comment.IsDeleted))]
    [MapperIgnoreTarget(nameof(Comment.ModerationTargetActionId))]
    [MapperIgnoreTarget(nameof(Comment.TenantId))]
    [MapProperty(nameof(CommentVo.VoCreateTime), nameof(Comment.CreateTime))]
    [MapperIgnoreTarget(nameof(Comment.CreateBy))]
    [MapperIgnoreTarget(nameof(Comment.CreateId))]
    [MapProperty(nameof(CommentVo.VoModifyTime), nameof(Comment.ModifyTime))]
    [MapperIgnoreTarget(nameof(Comment.ModifyBy))]
    [MapperIgnoreTarget(nameof(Comment.ModifyId))]
    [MapProperty(nameof(CommentVo.VoId), nameof(Comment.Id))]
    [UserMapping(Default = false)]
    private static partial Comment MapCommentVoToComment(CommentVo source);

    [ObjectFactory]
    private static Comment CreateComment(CommentVo source) => new(source.VoContent);

    public static partial List<Comment> ToCommentList(IEnumerable<CommentVo>? source);

    // PostEditHistoryVo → PostEditHistory
    [MapProperty(nameof(PostEditHistoryVo.VoPostId), nameof(PostEditHistory.PostId))]
    [MapProperty(nameof(PostEditHistoryVo.VoEditSequence), nameof(PostEditHistory.EditSequence))]
    [MapProperty(nameof(PostEditHistoryVo.VoOldTitle), nameof(PostEditHistory.OldTitle))]
    [MapProperty(nameof(PostEditHistoryVo.VoNewTitle), nameof(PostEditHistory.NewTitle))]
    [MapProperty(nameof(PostEditHistoryVo.VoOldContent), nameof(PostEditHistory.OldContent))]
    [MapProperty(nameof(PostEditHistoryVo.VoNewContent), nameof(PostEditHistory.NewContent))]
    [MapProperty(nameof(PostEditHistoryVo.VoEditorId), nameof(PostEditHistory.EditorId))]
    [MapProperty(nameof(PostEditHistoryVo.VoEditorName), nameof(PostEditHistory.EditorName))]
    [MapProperty(nameof(PostEditHistoryVo.VoEditedAt), nameof(PostEditHistory.EditedAt))]
    [MapperIgnoreTarget(nameof(PostEditHistory.TenantId))]
    [MapProperty(nameof(PostEditHistoryVo.VoCreateTime), nameof(PostEditHistory.CreateTime))]
    [MapperIgnoreTarget(nameof(PostEditHistory.CreateBy))]
    [MapperIgnoreTarget(nameof(PostEditHistory.CreateId))]
    [MapProperty(nameof(PostEditHistoryVo.VoId), nameof(PostEditHistory.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PostEditHistory? ToPostEditHistory(PostEditHistoryVo? source);

    public static partial List<PostEditHistory> ToPostEditHistoryList(IEnumerable<PostEditHistoryVo>? source);

    // PostQuickReplyVo → PostQuickReply
    [MapProperty(nameof(PostQuickReplyVo.VoPostId), nameof(PostQuickReply.PostId))]
    [MapProperty(nameof(PostQuickReplyVo.VoAuthorId), nameof(PostQuickReply.AuthorId))]
    [MapProperty(nameof(PostQuickReplyVo.VoAuthorName), nameof(PostQuickReply.AuthorName))]
    [MapProperty(nameof(PostQuickReplyVo.VoContent), nameof(PostQuickReply.Content))]
    [MapperIgnoreTarget(nameof(PostQuickReply.NormalizedContent))]
    [MapProperty(nameof(PostQuickReplyVo.VoStatus), nameof(PostQuickReply.Status))]
    [MapperIgnoreTarget(nameof(PostQuickReply.IsDeleted))]
    [MapperIgnoreTarget(nameof(PostQuickReply.ModerationTargetActionId))]
    [MapperIgnoreTarget(nameof(PostQuickReply.DeletedAt))]
    [MapperIgnoreTarget(nameof(PostQuickReply.DeletedBy))]
    [MapperIgnoreTarget(nameof(PostQuickReply.TenantId))]
    [MapProperty(nameof(PostQuickReplyVo.VoCreateTime), nameof(PostQuickReply.CreateTime))]
    [MapperIgnoreTarget(nameof(PostQuickReply.CreateBy))]
    [MapperIgnoreTarget(nameof(PostQuickReply.CreateId))]
    [MapperIgnoreTarget(nameof(PostQuickReply.ModifyTime))]
    [MapperIgnoreTarget(nameof(PostQuickReply.ModifyBy))]
    [MapperIgnoreTarget(nameof(PostQuickReply.ModifyId))]
    [MapProperty(nameof(PostQuickReplyVo.VoId), nameof(PostQuickReply.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PostQuickReply? ToPostQuickReply(PostQuickReplyVo? source);

    public static partial List<PostQuickReply> ToPostQuickReplyList(IEnumerable<PostQuickReplyVo>? source);

    // PostVo → Post
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static Post? ToPost(PostVo? source)
    {
        if (source is null) return null;
        var target = MapPostVoToPost(source);
        target.Summary = source.VoSummary!;
        return target;
    }

    [MapProperty(nameof(PostVo.VoTitle), nameof(Post.Title))]
    [MapProperty(nameof(PostVo.VoPublicId), nameof(Post.PublicId))]
    [MapProperty(nameof(PostVo.VoSlug), nameof(Post.Slug))]
    [MapperIgnoreTarget(nameof(Post.Summary))]
    [MapProperty(nameof(PostVo.VoContent), nameof(Post.Content))]
    [MapProperty(nameof(PostVo.VoContentType), nameof(Post.ContentType))]
    [MapProperty(nameof(PostVo.VoCoverAttachmentId), nameof(Post.CoverAttachmentId))]
    [MapProperty(nameof(PostVo.VoAuthorId), nameof(Post.AuthorId))]
    [MapProperty(nameof(PostVo.VoAuthorName), nameof(Post.AuthorName))]
    [MapProperty(nameof(PostVo.VoCategoryId), nameof(Post.CategoryId))]
    [MapProperty(nameof(PostVo.VoIsTop), nameof(Post.IsTop))]
    [MapProperty(nameof(PostVo.VoIsEssence), nameof(Post.IsEssence))]
    [MapProperty(nameof(PostVo.VoIsLocked), nameof(Post.IsLocked))]
    [MapProperty(nameof(PostVo.VoIsPublished), nameof(Post.IsPublished))]
    [MapProperty(nameof(PostVo.VoPublishTime), nameof(Post.PublishTime))]
    [MapperIgnoreTarget(nameof(Post.IsEnabled))]
    [MapperIgnoreTarget(nameof(Post.IsDeleted))]
    [MapperIgnoreTarget(nameof(Post.ModerationTargetActionId))]
    [MapperIgnoreTarget(nameof(Post.DeletedAt))]
    [MapperIgnoreTarget(nameof(Post.DeletedBy))]
    [MapProperty(nameof(PostVo.VoViewCount), nameof(Post.ViewCount))]
    [MapProperty(nameof(PostVo.VoLikeCount), nameof(Post.LikeCount))]
    [MapProperty(nameof(PostVo.VoCommentCount), nameof(Post.CommentCount))]
    [MapperIgnoreTarget(nameof(Post.EditCount))]
    [MapProperty(nameof(PostVo.VoContentRevision), nameof(Post.ContentRevision))]
    [MapProperty(nameof(PostVo.VoCollectCount), nameof(Post.CollectCount))]
    [MapProperty(nameof(PostVo.VoShareCount), nameof(Post.ShareCount))]
    [MapperIgnoreTarget(nameof(Post.TenantId))]
    [MapProperty(nameof(PostVo.VoCreateTime), nameof(Post.CreateTime))]
    [MapperIgnoreTarget(nameof(Post.CreateBy))]
    [MapperIgnoreTarget(nameof(Post.CreateId))]
    [MapProperty(nameof(PostVo.VoModifyTime), nameof(Post.ModifyTime))]
    [MapperIgnoreTarget(nameof(Post.ModifyBy))]
    [MapperIgnoreTarget(nameof(Post.ModifyId))]
    [MapProperty(nameof(PostVo.VoId), nameof(Post.Id))]
    [UserMapping(Default = false)]
    private static partial Post MapPostVoToPost(PostVo source);

    [ObjectFactory]
    private static Post CreatePost(PostVo source) => new(source.VoTitle, source.VoContent);

    public static partial List<Post> ToPostList(IEnumerable<PostVo>? source);

    // StickerGroupVo → StickerGroup
    [MapProperty(nameof(StickerGroupVo.VoCode), nameof(StickerGroup.Code))]
    [MapProperty(nameof(StickerGroupVo.VoName), nameof(StickerGroup.Name))]
    [MapProperty(nameof(StickerGroupVo.VoDescription), nameof(StickerGroup.Description))]
    [MapProperty(nameof(StickerGroupVo.VoCoverAttachmentId), nameof(StickerGroup.CoverAttachmentId))]
    [MapProperty(nameof(StickerGroupVo.VoGroupType), nameof(StickerGroup.GroupType))]
    [MapProperty(nameof(StickerGroupVo.VoIsEnabled), nameof(StickerGroup.IsEnabled))]
    [MapProperty(nameof(StickerGroupVo.VoSort), nameof(StickerGroup.Sort))]
    [MapperIgnoreTarget(nameof(StickerGroup.TenantId))]
    [MapperIgnoreTarget(nameof(StickerGroup.IsDeleted))]
    [MapperIgnoreTarget(nameof(StickerGroup.DeletedAt))]
    [MapperIgnoreTarget(nameof(StickerGroup.DeletedBy))]
    [MapProperty(nameof(StickerGroupVo.VoCreateTime), nameof(StickerGroup.CreateTime))]
    [MapperIgnoreTarget(nameof(StickerGroup.CreateBy))]
    [MapperIgnoreTarget(nameof(StickerGroup.CreateId))]
    [MapProperty(nameof(StickerGroupVo.VoModifyTime), nameof(StickerGroup.ModifyTime))]
    [MapperIgnoreTarget(nameof(StickerGroup.ModifyBy))]
    [MapperIgnoreTarget(nameof(StickerGroup.ModifyId))]
    [MapProperty(nameof(StickerGroupVo.VoId), nameof(StickerGroup.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial StickerGroup? ToStickerGroup(StickerGroupVo? source);

    public static partial List<StickerGroup> ToStickerGroupList(IEnumerable<StickerGroupVo>? source);

    // StickerVo → Sticker
    [MapProperty(nameof(StickerVo.VoGroupId), nameof(Sticker.GroupId))]
    [MapProperty(nameof(StickerVo.VoCode), nameof(Sticker.Code))]
    [MapProperty(nameof(StickerVo.VoName), nameof(Sticker.Name))]
    [MapProperty(nameof(StickerVo.VoIsAnimated), nameof(Sticker.IsAnimated))]
    [MapProperty(nameof(StickerVo.VoAllowInline), nameof(Sticker.AllowInline))]
    [MapProperty(nameof(StickerVo.VoAttachmentId), nameof(Sticker.AttachmentId))]
    [MapProperty(nameof(StickerVo.VoUseCount), nameof(Sticker.UseCount))]
    [MapProperty(nameof(StickerVo.VoSort), nameof(Sticker.Sort))]
    [MapProperty(nameof(StickerVo.VoIsEnabled), nameof(Sticker.IsEnabled))]
    [MapperIgnoreTarget(nameof(Sticker.IsDeleted))]
    [MapperIgnoreTarget(nameof(Sticker.DeletedAt))]
    [MapperIgnoreTarget(nameof(Sticker.DeletedBy))]
    [MapProperty(nameof(StickerVo.VoCreateTime), nameof(Sticker.CreateTime))]
    [MapperIgnoreTarget(nameof(Sticker.CreateBy))]
    [MapperIgnoreTarget(nameof(Sticker.CreateId))]
    [MapProperty(nameof(StickerVo.VoModifyTime), nameof(Sticker.ModifyTime))]
    [MapperIgnoreTarget(nameof(Sticker.ModifyBy))]
    [MapperIgnoreTarget(nameof(Sticker.ModifyId))]
    [MapProperty(nameof(StickerVo.VoId), nameof(Sticker.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial Sticker? ToSticker(StickerVo? source);

    public static partial List<Sticker> ToStickerList(IEnumerable<StickerVo>? source);

    // TagVo → Tag
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static Tag? ToTag(TagVo? source)
    {
        if (source is null) return null;
        var target = MapTagVoToTag(source);
        target.Description = source.VoDescription!;
        target.Color = source.VoColor!;
        return target;
    }

    [MapProperty(nameof(TagVo.VoName), nameof(Tag.Name))]
    [MapProperty(nameof(TagVo.VoSlug), nameof(Tag.Slug))]
    [MapperIgnoreTarget(nameof(Tag.Description))]
    [MapperIgnoreTarget(nameof(Tag.Color))]
    [MapProperty(nameof(TagVo.VoSortOrder), nameof(Tag.SortOrder))]
    [MapProperty(nameof(TagVo.VoPostCount), nameof(Tag.PostCount))]
    [MapProperty(nameof(TagVo.VoIsEnabled), nameof(Tag.IsEnabled))]
    [MapProperty(nameof(TagVo.VoIsFixed), nameof(Tag.IsFixed))]
    [MapProperty(nameof(TagVo.VoIsDeleted), nameof(Tag.IsDeleted))]
    [MapperIgnoreTarget(nameof(Tag.DeletedAt))]
    [MapperIgnoreTarget(nameof(Tag.DeletedBy))]
    [MapProperty(nameof(TagVo.VoCreateTime), nameof(Tag.CreateTime))]
    [MapperIgnoreTarget(nameof(Tag.CreateBy))]
    [MapperIgnoreTarget(nameof(Tag.CreateId))]
    [MapProperty(nameof(TagVo.VoModifyTime), nameof(Tag.ModifyTime))]
    [MapperIgnoreTarget(nameof(Tag.ModifyBy))]
    [MapperIgnoreTarget(nameof(Tag.ModifyId))]
    [MapProperty(nameof(TagVo.VoId), nameof(Tag.Id))]
    [UserMapping(Default = false)]
    private static partial Tag MapTagVoToTag(TagVo source);

    [ObjectFactory]
    private static Tag CreateTag(TagVo source) => new(source.VoName);

    public static partial List<Tag> ToTagList(IEnumerable<TagVo>? source);

    // UserCommentLikeVo → UserCommentLike
    [MapperIgnoreTarget(nameof(UserCommentLike.TenantId))]
    [MapProperty(nameof(UserCommentLikeVo.VoUserId), nameof(UserCommentLike.UserId))]
    [MapProperty(nameof(UserCommentLikeVo.VoCommentId), nameof(UserCommentLike.CommentId))]
    [MapProperty(nameof(UserCommentLikeVo.VoPostId), nameof(UserCommentLike.PostId))]
    [MapProperty(nameof(UserCommentLikeVo.VoLikedAt), nameof(UserCommentLike.LikedAt))]
    [MapperIgnoreTarget(nameof(UserCommentLike.IsDeleted))]
    [MapProperty(nameof(UserCommentLikeVo.VoId), nameof(UserCommentLike.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserCommentLike? ToUserCommentLike(UserCommentLikeVo? source);

    public static partial List<UserCommentLike> ToUserCommentLikeList(IEnumerable<UserCommentLikeVo>? source);

}
