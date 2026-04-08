using Radish.Common.AttributeTool;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

public partial class PostService
{
    /// <summary>
    /// 为问答帖提交回答
    /// </summary>
    [UseTran]
    public async Task<PostQuestionVo> AddAnswerAsync(long postId, string content, long authorId, string authorName, long tenantId)
    {
        var trimmedContent = content?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedContent))
        {
            throw new ArgumentException("回答内容不能为空", nameof(content));
        }

        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted || !post.IsPublished)
        {
            throw new InvalidOperationException("帖子不存在");
        }

        var question = await _postQuestionRepository.QueryFirstAsync(q => q.PostId == postId && !q.IsDeleted);
        if (question == null)
        {
            throw new InvalidOperationException("当前帖子不是问答帖");
        }

        var safeAuthorName = string.IsNullOrWhiteSpace(authorName) ? "System" : authorName;
        var answerId = await _postAnswerRepository.AddAsync(new PostAnswer
        {
            PostId = postId,
            AuthorId = authorId,
            AuthorName = safeAuthorName,
            Content = trimmedContent,
            IsAccepted = false,
            TenantId = tenantId,
            CreateBy = safeAuthorName,
            CreateId = authorId
        });

        question.AnswerCount++;
        question.ModifyTime = DateTime.Now;
        question.ModifyBy = safeAuthorName;
        question.ModifyId = authorId;
        await _postQuestionRepository.UpdateAsync(question);

        await BindReferencedAttachmentsAsync(trimmedContent, BusinessType.Comment, answerId, authorId, safeAuthorName, tenantId);

        return await BuildPostQuestionVoAsync(postId)
            ?? throw new InvalidOperationException("问答详情不存在");
    }

    /// <summary>
    /// 采纳问答帖中的回答
    /// </summary>
    [UseTran]
    public async Task<PostQuestionVo> AcceptAnswerAsync(long postId, long answerId, long operatorId, string operatorName)
    {
        if (postId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0", nameof(postId));
        }

        if (answerId <= 0)
        {
            throw new ArgumentException("回答ID必须大于0", nameof(answerId));
        }

        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted || !post.IsPublished)
        {
            throw new InvalidOperationException("帖子不存在");
        }

        if (post.AuthorId != operatorId)
        {
            throw new InvalidOperationException("只有提问者可以采纳答案");
        }

        var question = await _postQuestionRepository.QueryFirstAsync(q => q.PostId == postId && !q.IsDeleted);
        if (question == null)
        {
            throw new InvalidOperationException("当前帖子不是问答帖");
        }

        if (question.IsSolved || question.AcceptedAnswerId.HasValue)
        {
            throw new InvalidOperationException("当前问题已采纳答案");
        }

        var answer = await _postAnswerRepository.QueryFirstAsync(a => a.Id == answerId && a.PostId == postId && !a.IsDeleted);
        if (answer == null)
        {
            throw new InvalidOperationException("回答不存在");
        }

        if (answer.AuthorId == operatorId)
        {
            throw new InvalidOperationException("不能采纳自己的回答");
        }

        var safeOperatorName = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName;

        answer.IsAccepted = true;
        answer.ModifyTime = DateTime.Now;
        answer.ModifyBy = safeOperatorName;
        answer.ModifyId = operatorId;
        await _postAnswerRepository.UpdateAsync(answer);

        question.IsSolved = true;
        question.AcceptedAnswerId = answerId;
        question.ModifyTime = DateTime.Now;
        question.ModifyBy = safeOperatorName;
        question.ModifyId = operatorId;
        await _postQuestionRepository.UpdateAsync(question);

        return await BuildPostQuestionVoAsync(postId)
            ?? throw new InvalidOperationException("问答详情不存在");
    }

    /// <summary>
    /// 更新帖子浏览次数
    /// </summary>
    public async Task IncrementViewCountAsync(long postId)
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post != null)
        {
            post.ViewCount++;
            await _postRepository.UpdateAsync(post);
        }
    }

    /// <summary>
    /// 更新帖子点赞次数
    /// </summary>
    public async Task UpdateLikeCountAsync(long postId, int increment)
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post != null)
        {
            post.LikeCount = Math.Max(0, post.LikeCount + increment);
            await _postRepository.UpdateAsync(post);
        }
    }

    /// <summary>
    /// 更新帖子评论次数
    /// </summary>
    public async Task UpdateCommentCountAsync(long postId, int increment)
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post != null)
        {
            post.CommentCount = Math.Max(0, post.CommentCount + increment);
            await _postRepository.UpdateAsync(post);
        }
    }

    /// <summary>
    /// 切换帖子点赞状态（点赞/取消点赞）
    /// </summary>
    public async Task<PostLikeResultDto> ToggleLikeAsync(long userId, long postId)
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted)
        {
            throw new InvalidOperationException("帖子不存在或已被删除");
        }

        var existingLikes = await _userPostLikeRepository.QueryAsync(
            x => x.UserId == userId && x.PostId == postId && !x.IsDeleted);
        var deletedLikes = await _userPostLikeRepository.QueryAsync(
            x => x.UserId == userId && x.PostId == postId && x.IsDeleted);

        bool isLiked;
        int likeCountDelta;

        if (existingLikes.Any())
        {
            await _userPostLikeRepository.UpdateColumnsAsync(
                l => new UserPostLike { IsDeleted = true },
                l => l.Id == existingLikes.First().Id);
            isLiked = false;
            likeCountDelta = -1;
        }
        else if (deletedLikes.Any())
        {
            await _userPostLikeRepository.UpdateColumnsAsync(
                l => new UserPostLike
                {
                    IsDeleted = false,
                    LikedAt = DateTime.UtcNow
                },
                l => l.Id == deletedLikes.First().Id);
            isLiked = true;
            likeCountDelta = 1;
        }
        else
        {
            var newLike = new UserPostLike
            {
                UserId = userId,
                PostId = postId,
                LikedAt = DateTime.UtcNow
            };
            await _userPostLikeRepository.AddAsync(newLike);
            isLiked = true;
            likeCountDelta = 1;
        }

        post.LikeCount = Math.Max(0, post.LikeCount + likeCountDelta);
        await _postRepository.UpdateAsync(post);

        if (isLiked)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var rewardResult = await _coinRewardService.GrantLikeRewardAsync(
                        postId,
                        post.AuthorId,
                        userId);

                    if (rewardResult.IsSuccess)
                    {
                        Serilog.Log.Information("帖子点赞萝卜币奖励发放成功：PostId={PostId}, 作者={AuthorId}, 点赞者={LikerId}",
                            postId, post.AuthorId, userId);
                    }

                    Serilog.Log.Information("准备发放帖子点赞经验值：PostId={PostId}, 作者={AuthorId}, 点赞者={LikerId}",
                        postId, post.AuthorId, userId);

                    var receiverExpResult = await _experienceService.GrantExperienceAsync(
                        userId: post.AuthorId,
                        amount: 2,
                        expType: "RECEIVE_LIKE",
                        businessType: "Post",
                        businessId: postId,
                        remark: "帖子被点赞");

                    if (receiverExpResult)
                    {
                        Serilog.Log.Information("帖子被点赞经验值发放成功：PostId={PostId}, 作者={AuthorId}, Amount=2",
                            postId, post.AuthorId);
                    }
                    else
                    {
                        Serilog.Log.Warning("帖子被点赞经验值发放失败：PostId={PostId}, 作者={AuthorId}",
                            postId, post.AuthorId);
                    }

                    var giverExpResult = await _experienceService.GrantExperienceAsync(
                        userId: userId,
                        amount: 1,
                        expType: "GIVE_LIKE",
                        businessType: "Post",
                        businessId: postId,
                        remark: "点赞帖子");

                    if (giverExpResult)
                    {
                        Serilog.Log.Information("点赞帖子经验值发放成功：PostId={PostId}, 点赞者={LikerId}, Amount=1",
                            postId, userId);
                    }
                    else
                    {
                        Serilog.Log.Warning("点赞帖子经验值发放失败：PostId={PostId}, 点赞者={LikerId}",
                            postId, userId);
                    }

                    if (post.AuthorId != userId)
                    {
                        var shouldDedup = await _dedupService.ShouldDedupAsync(
                            post.AuthorId,
                            NotificationType.PostLiked,
                            postId);

                        if (!shouldDedup)
                        {
                            try
                            {
                                await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                                {
                                    Type = NotificationType.PostLiked,
                                    Title = "帖子被点赞",
                                    Content = $"你的帖子《{post.Title}》收到了一个赞",
                                    Priority = (int)NotificationPriority.Low,
                                    BusinessType = BusinessType.Post,
                                    BusinessId = postId,
                                    TriggerId = userId,
                                    TriggerName = null,
                                    TriggerAvatar = null,
                                    ReceiverUserIds = new List<long> { post.AuthorId },
                                    ExtData = NotificationNavigationHelper.BuildForumNavigationExtData(postId)
                                });

                                await _dedupService.RecordDedupKeyAsync(
                                    post.AuthorId,
                                    NotificationType.PostLiked,
                                    postId,
                                    windowSeconds: 300);

                                Serilog.Log.Information("帖子点赞通知发送成功：PostId={PostId}, 接收者={ReceiverId}",
                                    postId, post.AuthorId);
                            }
                            catch (Exception notifyEx)
                            {
                                Serilog.Log.Error(notifyEx, "发送帖子点赞通知失败：PostId={PostId}, 接收者={ReceiverId}",
                                    postId, post.AuthorId);
                            }
                        }
                        else
                        {
                            Serilog.Log.Debug("帖子点赞通知被去重：PostId={PostId}, 接收者={ReceiverId}",
                                postId, post.AuthorId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "发放帖子点赞奖励失败：PostId={PostId}, AuthorId={AuthorId}, LikerId={LikerId}, Message={Message}",
                        postId, post.AuthorId, userId, ex.Message);
                }
            });
        }

        return new PostLikeResultDto
        {
            IsLiked = isLiked,
            LikeCount = post.LikeCount
        };
    }
}
