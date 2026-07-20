using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using SqlSugar;

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
            throw new BusinessException("帖子不存在", 404, "Forum.PostNotFound", "error.forum.post_not_found");
        }

        var question = await _postQuestionRepository.QueryFirstAsync(q => q.PostId == postId && !q.IsDeleted);
        if (question == null)
        {
            throw new BusinessException("当前帖子不是问答帖", 400, "Forum.NotQuestionPost", "error.forum.not_question_post");
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
            ?? throw new BusinessException("问答详情不存在", 404, "Forum.QuestionNotFound", "error.forum.question_not_found");
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
            throw new BusinessException("帖子不存在", 404, "Forum.PostNotFound", "error.forum.post_not_found");
        }

        if (post.AuthorId != operatorId)
        {
            throw new BusinessException("只有提问者可以采纳答案", 403, "Forum.AnswerAcceptForbidden", "error.forum.answer_accept_forbidden");
        }

        var question = await _postQuestionRepository.QueryFirstAsync(q => q.PostId == postId && !q.IsDeleted);
        if (question == null)
        {
            throw new BusinessException("当前帖子不是问答帖", 400, "Forum.NotQuestionPost", "error.forum.not_question_post");
        }

        if (question.IsSolved || question.AcceptedAnswerId.HasValue)
        {
            throw new BusinessException("当前问题已采纳答案", 409, "Forum.AnswerAlreadyAccepted", "error.forum.answer_already_accepted");
        }

        var answer = await _postAnswerRepository.QueryFirstAsync(a => a.Id == answerId && a.PostId == postId && !a.IsDeleted);
        if (answer == null)
        {
            throw new BusinessException("回答不存在", 404, "Forum.AnswerNotFound", "error.forum.answer_not_found");
        }

        if (answer.AuthorId == operatorId)
        {
            throw new BusinessException("不能采纳自己的回答", 400, "Forum.CannotAcceptOwnAnswer", "error.forum.cannot_accept_own_answer");
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
            ?? throw new BusinessException("问答详情不存在", 404, "Forum.QuestionNotFound", "error.forum.question_not_found");
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
    public async Task<PostLikeResultDto> ToggleLikeAsync(long userId, string userName, long postId)
    {
        var likeResult = await (_postCustomRepository ?? throw new InvalidOperationException("帖子专属仓储未注册"))
            .TogglePostLikeAsync(userId, userName, postId);

        return new PostLikeResultDto
        {
            IsLiked = likeResult.IsLiked,
            LikeCount = likeResult.LikeCount
        };
    }
}
