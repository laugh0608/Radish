using System.Linq.Expressions;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Service;

public partial class PostService
{
    /// <summary>
    /// 分页获取普通论坛帖子列表
    /// </summary>
    public async Task<(List<PostVo> data, int totalCount)> GetForumPostPageAsync(
        long? categoryId = null,
        int pageIndex = 1,
        int pageSize = 20,
        string sortBy = "newest",
        string? keyword = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? tagSlug = null)
    {
        if (_postCustomRepository == null)
        {
            throw new InvalidOperationException("帖子仓储未配置，无法查询论坛帖子列表");
        }

        var tagId = await ResolvePublicTagIdAsync(tagSlug);
        if (!string.IsNullOrWhiteSpace(tagSlug) && !tagId.HasValue)
        {
            return (new List<PostVo>(), 0);
        }

        var (posts, totalCount) = await _postCustomRepository.QueryForumPostPageAsync(
            categoryId,
            tagId,
            keyword,
            startTime,
            endTime,
            pageIndex,
            pageSize,
            sortBy);

        return (Mapper.Map<List<PostVo>>(posts), totalCount);
    }

    /// <summary>
    /// 获取帖子详情（包含分类名称和标签）
    /// </summary>
    public async Task<PostVo?> GetPostDetailAsync(long postId, long? viewerUserId = null, string answerSort = "default")
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted)
        {
            return null;
        }

        var postVo = Mapper.Map<PostVo>(post);

        if (post.CategoryId > 0)
        {
            var category = await _categoryRepository.QueryByIdAsync(post.CategoryId);
            if (category != null)
            {
                postVo.VoCategoryName = category.Name;
            }
        }

        var postTags = await _postTagRepository.QueryAsync(pt => pt.PostId == postId);
        if (postTags.Any())
        {
            var tagIds = postTags.Select(pt => pt.TagId).ToList();
            var tags = await _tagService.QueryAsync(t => tagIds.Contains(t.Id) && t.IsEnabled && !t.IsDeleted);
            postVo.VoTags = string.Join(", ", tags.Select(t => t.VoName));
            postVo.VoTagSlugs = tags
                .Select(t => t.VoSlug)
                .Where(tagSlugItem => !string.IsNullOrWhiteSpace(tagSlugItem))
                .ToList();
        }

        var pollVo = await BuildPostPollVoAsync(postId, viewerUserId);
        if (pollVo != null)
        {
            postVo.VoHasPoll = true;
            postVo.VoPollTotalVoteCount = pollVo.VoTotalVoteCount;
            postVo.VoPollIsClosed = pollVo.VoIsClosed;
            postVo.VoPoll = pollVo;
        }

        var lotteryVo = await BuildPostLotteryVoAsync(postId, post.AuthorId);
        if (lotteryVo != null)
        {
            postVo.VoHasLottery = true;
            postVo.VoLotteryParticipantCount = lotteryVo.VoParticipantCount;
            postVo.VoLotteryIsDrawn = lotteryVo.VoIsDrawn;
            postVo.VoLottery = lotteryVo;
        }

        await FillPostGodCommentPreviewAsync(new List<PostVo> { postVo });

        postVo.VoQuestion = await BuildPostQuestionVoAsync(postId, answerSort);
        FillPostQuestionSummary(postVo, postVo.VoQuestion);

        return postVo;
    }

    /// <summary>
    /// 分页获取问答帖子列表
    /// </summary>
    public async Task<(List<PostVo> data, int totalCount)> GetQuestionPostPageAsync(
        long? categoryId = null,
        int pageIndex = 1,
        int pageSize = 20,
        string sortBy = "newest",
        string? keyword = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        bool? isSolved = null,
        string? tagSlug = null)
    {
        if (_postCustomRepository == null)
        {
            throw new InvalidOperationException("帖子仓储未配置，无法查询问答帖子列表");
        }

        var tagId = await ResolvePublicTagIdAsync(tagSlug);
        if (!string.IsNullOrWhiteSpace(tagSlug) && !tagId.HasValue)
        {
            return (new List<PostVo>(), 0);
        }

        var (posts, totalCount) = await _postCustomRepository.QueryQuestionPostPageAsync(
            categoryId,
            tagId,
            keyword,
            startTime,
            endTime,
            isSolved,
            pageIndex,
            pageSize,
            sortBy);

        return (Mapper.Map<List<PostVo>>(posts), totalCount);
    }

    /// <summary>
    /// 分页获取投票帖子列表
    /// </summary>
    public async Task<(List<PostVo> data, int totalCount)> GetPollPostPageAsync(
        long? categoryId = null,
        int pageIndex = 1,
        int pageSize = 20,
        string sortBy = "newest",
        string? keyword = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        bool? isClosed = null,
        string? tagSlug = null)
    {
        var utcNow = DateTime.UtcNow;
        Expression<Func<PostPoll, bool>> pollCondition = isClosed switch
        {
            true => poll => !poll.IsDeleted &&
                            (poll.IsClosed || (poll.EndTime != null && poll.EndTime <= utcNow)),
            false => poll => !poll.IsDeleted &&
                             !poll.IsClosed &&
                             (poll.EndTime == null || poll.EndTime > utcNow),
            _ => poll => !poll.IsDeleted
        };

        var polls = await _postPollRepository.QueryAsync(pollCondition);
        var pollPostIds = polls
            .Select(poll => poll.PostId)
            .Distinct()
            .ToList();

        var tagId = await ResolvePublicTagIdAsync(tagSlug);
        if (!string.IsNullOrWhiteSpace(tagSlug) && !tagId.HasValue)
        {
            return (new List<PostVo>(), 0);
        }

        if (tagId.HasValue)
        {
            var taggedPostIds = (await _postTagRepository.QueryAsync(postTag => postTag.TagId == tagId.Value))
                .Select(postTag => postTag.PostId)
                .Distinct()
                .ToHashSet();
            pollPostIds = pollPostIds
                .Where(postId => taggedPostIds.Contains(postId))
                .ToList();
        }

        if (pollPostIds.Count == 0)
        {
            return (new List<PostVo>(), 0);
        }

        var normalizedKeyword = keyword ?? string.Empty;
        var hasKeyword = !string.IsNullOrWhiteSpace(normalizedKeyword);
        var hasCategory = categoryId.HasValue;
        var categoryValue = categoryId ?? 0;
        var hasStartTime = startTime.HasValue;
        var hasEndTime = endTime.HasValue;
        var startTimeValue = startTime ?? DateTime.MinValue;
        var endTimeValue = endTime ?? DateTime.MaxValue;

        Expression<Func<Post, bool>> baseCondition = post =>
            pollPostIds.Contains(post.Id) &&
            post.IsPublished &&
            !post.IsDeleted &&
            (!hasCategory || post.CategoryId == categoryValue) &&
            (!hasKeyword || post.Title.Contains(normalizedKeyword) || post.Content.Contains(normalizedKeyword)) &&
            (!hasStartTime || post.CreateTime >= startTimeValue) &&
            (!hasEndTime || post.CreateTime <= endTimeValue);

        return sortBy switch
        {
            "deadline" => await QueryPollPostsByDeadlineAsync(baseCondition, polls, pageIndex, pageSize),
            "votes" => await QueryPollPostsByVotesAsync(baseCondition, polls, pageIndex, pageSize),
            "hottest" => await QueryPollPostsByHottestAsync(baseCondition, pageIndex, pageSize),
            "essence" => await QueryPageAsync(
                baseCondition,
                pageIndex,
                pageSize,
                post => new { post.IsTop, post.IsEssence, post.CreateTime },
                SqlSugar.OrderByType.Desc),
            _ => await QueryPageAsync(
                baseCondition,
                pageIndex,
                pageSize,
                post => new { post.IsTop, post.CreateTime },
                SqlSugar.OrderByType.Desc)
        };
    }

    /// <summary>
    /// 批量回填帖子列表所需的轻量元数据
    /// </summary>
    public async Task FillPostListMetadataAsync(List<PostVo> posts)
    {
        if (posts.Count == 0)
        {
            return;
        }

        var postIds = posts
            .Select(post => post.VoId)
            .Where(postId => postId > 0)
            .Distinct()
            .ToList();

        if (postIds.Count == 0)
        {
            return;
        }

        var categoryIds = posts
            .Where(post => post.VoCategoryId > 0)
            .Select(post => post.VoCategoryId)
            .Distinct()
            .ToList();

        Dictionary<long, string> categoryNameMap = new();
        if (categoryIds.Count > 0)
        {
            categoryNameMap = (await _categoryRepository.QueryAsync(category =>
                    categoryIds.Contains(category.Id) &&
                    category.IsEnabled &&
                    !category.IsDeleted))
                .GroupBy(category => category.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
        }

        var postTags = await _postTagRepository.QueryAsync(postTag => postIds.Contains(postTag.PostId));
        var tagIds = postTags
            .Select(postTag => postTag.TagId)
            .Distinct()
            .ToList();

        Dictionary<long, string> tagNameMap = new();
        Dictionary<long, string> tagSlugMap = new();
        if (tagIds.Count > 0)
        {
            var enabledTags = (await _tagRepository.QueryAsync(tag =>
                    tagIds.Contains(tag.Id) &&
                    tag.IsEnabled &&
                    !tag.IsDeleted))
                .GroupBy(tag => tag.Id)
                .ToDictionary(group => group.Key, group => group.First());
            tagNameMap = enabledTags.ToDictionary(entry => entry.Key, entry => entry.Value.Name);
            tagSlugMap = enabledTags.ToDictionary(entry => entry.Key, entry => entry.Value.Slug);
        }

        var tagTextMap = postTags
            .GroupBy(postTag => postTag.PostId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ",
                    group.Select(postTag => tagNameMap.GetValueOrDefault(postTag.TagId))
                        .Where(tagName => !string.IsNullOrWhiteSpace(tagName))));

        var pollMap = (await _postPollRepository.QueryAsync(poll =>
                postIds.Contains(poll.PostId) &&
                !poll.IsDeleted))
            .GroupBy(poll => poll.PostId)
            .ToDictionary(group => group.Key, group => group.First());

        var questionMap = (await _postQuestionRepository.QueryAsync(question =>
                postIds.Contains(question.PostId) &&
                !question.IsDeleted))
            .GroupBy(question => question.PostId)
            .ToDictionary(group => group.Key, group => group.First());

        Dictionary<long, PostLottery> lotteryMap = new();
        if (_postLotteryRepository != null)
        {
            lotteryMap = (await _postLotteryRepository.QueryAsync(lottery =>
                    postIds.Contains(lottery.PostId) &&
                    !lottery.IsDeleted))
                .GroupBy(lottery => lottery.PostId)
                .ToDictionary(group => group.Key, group => group.First());
        }

        var postAuthorMap = posts
            .Where(post => post.VoAuthorId > 0)
            .GroupBy(post => post.VoId)
            .ToDictionary(group => group.Key, group => group.First().VoAuthorId);
        var lotteryParticipantCountMap = await BuildLotteryParticipantCountMapAsync(postAuthorMap, lotteryMap);
        var godCommentPreviewMap = await BuildGodCommentPreviewMapAsync(postIds);

        foreach (var post in posts)
        {
            if (string.IsNullOrWhiteSpace(post.VoCategoryName) &&
                categoryNameMap.TryGetValue(post.VoCategoryId, out var categoryName))
            {
                post.VoCategoryName = categoryName;
            }

            if (tagTextMap.TryGetValue(post.VoId, out var tags))
            {
                post.VoTags = tags;
            }

            post.VoTagSlugs = postTags
                .Where(postTag => postTag.PostId == post.VoId)
                .Select(postTag => tagSlugMap.GetValueOrDefault(postTag.TagId))
                .Where(tagSlugItem => !string.IsNullOrWhiteSpace(tagSlugItem))
                .ToList()!;

            if (!pollMap.TryGetValue(post.VoId, out var poll))
            {
                post.VoHasPoll = false;
                post.VoPollTotalVoteCount = 0;
                post.VoPollIsClosed = false;
            }
            else
            {
                post.VoHasPoll = true;
                post.VoPollTotalVoteCount = poll.TotalVoteCount;
                post.VoPollIsClosed = IsPollClosed(poll);
            }

            if (!lotteryMap.TryGetValue(post.VoId, out var lottery))
            {
                post.VoHasLottery = false;
                post.VoLotteryParticipantCount = 0;
                post.VoLotteryIsDrawn = false;
            }
            else
            {
                post.VoHasLottery = true;
                post.VoLotteryParticipantCount = lotteryParticipantCountMap.GetValueOrDefault(post.VoId, lottery.ParticipantCount);
                post.VoLotteryIsDrawn = lottery.IsDrawn;
            }

            if (godCommentPreviewMap.TryGetValue(post.VoId, out var highlight))
            {
                post.VoGodCommentId = highlight.CommentId;
                post.VoGodCommentAuthorName = highlight.AuthorName;
                post.VoGodCommentContentSnapshot = highlight.ContentSnapshot;
            }
            else
            {
                post.VoGodCommentId = null;
                post.VoGodCommentAuthorName = null;
                post.VoGodCommentContentSnapshot = null;
            }

            FillPostQuestionSummary(post, questionMap.GetValueOrDefault(post.VoId));
        }
    }

    /// <summary>
    /// 批量回填帖子作者头像、问答回答头像与最近互动人
    /// </summary>
    public async Task FillPostAvatarAndInteractorsAsync(List<PostVo> posts)
    {
        if (posts.Count == 0)
        {
            return;
        }

        var postAuthorMap = posts
            .Where(post => post.VoId > 0)
            .GroupBy(post => post.VoId)
            .ToDictionary(group => group.Key, group => group.First().VoAuthorId);

        var recentComments = _commentCustomRepository == null || postAuthorMap.Count == 0
            ? []
            : await _commentCustomRepository.QueryLatestInteractorCommentsByPostIdsAsync(postAuthorMap, 3);

        var commentsByPost = recentComments
            .Where(comment => comment.AuthorId > 0)
            .GroupBy(comment => comment.PostId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(comment => comment.CreateTime)
                    .ThenByDescending(comment => comment.Id)
                    .ToList());

        var userIds = posts
            .Select(post => post.VoAuthorId)
            .Concat(posts.SelectMany(post => post.VoQuestion?.VoAnswers ?? []).Select(answer => answer.VoAuthorId))
            .Concat(recentComments.Select(comment => comment.AuthorId))
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();

        Dictionary<long, string> avatarUrlMap = new();
        if (_attachmentService != null && userIds.Count > 0)
        {
            avatarUrlMap = (await _attachmentService.GetLatestAvatarAssetMapAsync(userIds))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Value.Url))
                .ToDictionary(entry => entry.Key, entry => entry.Value.Url);
        }

        foreach (var post in posts)
        {
            if (avatarUrlMap.TryGetValue(post.VoAuthorId, out var authorAvatarUrl))
            {
                post.VoAuthorAvatarUrl = authorAvatarUrl;
            }

            if (post.VoQuestion?.VoAnswers?.Count > 0)
            {
                foreach (var answer in post.VoQuestion.VoAnswers.Where(answer => answer.VoAuthorId > 0))
                {
                    if (avatarUrlMap.TryGetValue(answer.VoAuthorId, out var answerAvatarUrl))
                    {
                        answer.VoAuthorAvatarUrl = answerAvatarUrl;
                    }
                }
            }

            if (!commentsByPost.TryGetValue(post.VoId, out var postComments))
            {
                post.VoLatestInteractors = [];
                continue;
            }

            post.VoLatestInteractors = postComments
                .Select(comment => new PostInteractorVo
                {
                    VoUserId = comment.AuthorId,
                    VoUserName = comment.AuthorName,
                    VoAvatarUrl = avatarUrlMap.GetValueOrDefault(comment.AuthorId)
                })
                .ToList();
        }
    }

    private async Task<PostPollVo?> BuildPostPollVoAsync(long postId, long? viewerUserId)
    {
        var poll = await _postPollRepository.QueryFirstAsync(p => p.PostId == postId && !p.IsDeleted);
        if (poll == null)
        {
            return null;
        }

        var pollVo = Mapper.Map<PostPollVo>(poll);
        pollVo.VoPollId = poll.Id;
        pollVo.VoIsClosed = IsPollClosed(poll);

        var options = await _postPollOptionRepository.QueryAsync(o => o.PollId == poll.Id && !o.IsDeleted);
        var orderedOptions = options
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Id)
            .ToList();

        var totalVoteCount = poll.TotalVoteCount > 0
            ? poll.TotalVoteCount
            : orderedOptions.Sum(option => option.VoteCount);

        pollVo.VoTotalVoteCount = totalVoteCount;
        pollVo.VoOptions = orderedOptions
            .Select(option =>
            {
                var optionVo = Mapper.Map<PostPollOptionVo>(option);
                optionVo.VoOptionId = option.Id;
                optionVo.VoVotePercent = totalVoteCount <= 0
                    ? 0
                    : Math.Round(option.VoteCount * 100m / totalVoteCount, 2);
                return optionVo;
            })
            .ToList();

        if (viewerUserId.HasValue && viewerUserId.Value > 0)
        {
            var vote = await _postPollVoteRepository.QueryFirstAsync(v => v.PollId == poll.Id && v.UserId == viewerUserId.Value);
            if (vote != null)
            {
                pollVo.VoHasVoted = true;
                pollVo.VoSelectedOptionId = vote.OptionId;
            }
        }

        return pollVo;
    }

    private async Task<PostLotteryVo?> BuildPostLotteryVoAsync(long postId, long postAuthorId)
    {
        if (_postLotteryRepository == null)
        {
            return null;
        }

        var lottery = await _postLotteryRepository.QueryFirstAsync(item => item.PostId == postId && !item.IsDeleted);
        if (lottery == null)
        {
            return null;
        }

        var lotteryVo = Mapper.Map<PostLotteryVo>(lottery);
        lotteryVo.VoLotteryId = lottery.Id;

        if (_commentRepository != null)
        {
            var participantComments = await GetTopLevelLotteryCommentsAsync(
                postId,
                postAuthorId,
                lottery.IsDrawn ? lottery.DrawnAt : null);
            lotteryVo.VoParticipantCount = participantComments
                .Select(comment => comment.AuthorId)
                .Distinct()
                .Count();
        }
        else
        {
            lotteryVo.VoParticipantCount = lottery.ParticipantCount;
        }

        if (_postLotteryWinnerRepository == null)
        {
            return lotteryVo;
        }

        var winners = await _postLotteryWinnerRepository.QueryAsync(winner =>
            winner.LotteryId == lottery.Id &&
            !winner.IsDeleted);

        lotteryVo.VoWinners = Mapper.Map<List<PostLotteryWinnerVo>>(winners
            .OrderBy(winner => winner.CreateTime)
            .ThenBy(winner => winner.Id)
            .ToList());

        return lotteryVo;
    }

    private async Task<(List<PostVo> data, int totalCount)> QueryPollPostsByHottestAsync(
        Expression<Func<Post, bool>> baseCondition,
        int pageIndex,
        int pageSize)
    {
        return await QueryPageAsync(
            baseCondition,
            pageIndex,
            pageSize,
            post => new
            {
                post.IsTop,
                HotScore = post.ViewCount + post.LikeCount * 2 + post.CommentCount * 3,
                post.CreateTime
            },
            SqlSugar.OrderByType.Desc);
    }

    private async Task<(List<PostVo> data, int totalCount)> QueryPollPostsByVotesAsync(
        Expression<Func<Post, bool>> baseCondition,
        List<PostPoll> polls,
        int pageIndex,
        int pageSize)
    {
        var allPosts = await QueryAsync(baseCondition);
        var totalCount = allPosts.Count;
        var pollVoteCountMap = polls
            .GroupBy(poll => poll.PostId)
            .ToDictionary(group => group.Key, group => group.First().TotalVoteCount);

        var data = allPosts
            .OrderByDescending(post => post.VoIsTop)
            .ThenByDescending(post => pollVoteCountMap.GetValueOrDefault(post.VoId))
            .ThenByDescending(post => post.VoCreateTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (data, totalCount);
    }

    private async Task<(List<PostVo> data, int totalCount)> QueryPollPostsByDeadlineAsync(
        Expression<Func<Post, bool>> baseCondition,
        List<PostPoll> polls,
        int pageIndex,
        int pageSize)
    {
        var allPosts = await QueryAsync(baseCondition);
        var totalCount = allPosts.Count;
        var pollMap = polls
            .GroupBy(poll => poll.PostId)
            .ToDictionary(group => group.Key, group => group.First());

        var data = allPosts
            .OrderByDescending(post => post.VoIsTop)
            .ThenBy(post => GetPollDeadlineGroup(pollMap.GetValueOrDefault(post.VoId)))
            .ThenBy(post => GetPollDeadlineTicksKey(pollMap.GetValueOrDefault(post.VoId)))
            .ThenByDescending(post => post.VoCreateTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (data, totalCount);
    }

    private async Task<PostQuestionVo?> BuildPostQuestionVoAsync(long postId, string answerSort = "default")
    {
        var question = await _postQuestionRepository.QueryFirstAsync(q => q.PostId == postId && !q.IsDeleted);
        if (question == null)
        {
            return null;
        }

        var questionVo = Mapper.Map<PostQuestionVo>(question);
        var answers = await _postAnswerRepository.QueryAsync(answer => answer.PostId == postId && !answer.IsDeleted);
        var normalizedAnswerSort = answerSort?.Trim().ToLowerInvariant() ?? "default";
        var orderedAnswers = normalizedAnswerSort switch
        {
            "latest" => answers
                .OrderByDescending(answer => answer.CreateTime)
                .ThenByDescending(answer => answer.Id),
            _ => answers
                .OrderByDescending(answer => answer.IsAccepted)
                .ThenBy(answer => answer.CreateTime)
                .ThenBy(answer => answer.Id)
        };

        questionVo.VoAnswers = orderedAnswers
            .Select(answer => Mapper.Map<PostAnswerVo>(answer))
            .ToList();

        return questionVo;
    }

    private static void FillPostQuestionSummary(PostVo postVo, PostQuestion? question)
    {
        if (question == null)
        {
            postVo.VoIsQuestion = false;
            postVo.VoIsSolved = false;
            postVo.VoAnswerCount = 0;
            return;
        }

        postVo.VoIsQuestion = true;
        postVo.VoIsSolved = question.IsSolved;
        postVo.VoAnswerCount = question.AnswerCount;
    }

    private static void FillPostQuestionSummary(PostVo postVo, PostQuestionVo? question)
    {
        if (question == null)
        {
            postVo.VoIsQuestion = false;
            postVo.VoIsSolved = false;
            postVo.VoAnswerCount = 0;
            return;
        }

        postVo.VoIsQuestion = true;
        postVo.VoIsSolved = question.VoIsSolved;
        postVo.VoAnswerCount = question.VoAnswerCount;
    }

    private static bool IsPollClosed(PostPoll poll)
    {
        return poll.IsClosed || (poll.EndTime.HasValue && poll.EndTime.Value <= DateTime.UtcNow);
    }

    private async Task<long?> ResolvePublicTagIdAsync(string? tagSlug)
    {
        if (string.IsNullOrWhiteSpace(tagSlug))
        {
            return null;
        }

        var normalizedSlug = tagSlug.Trim().ToLowerInvariant();
        var tag = await _tagRepository.QueryFirstAsync(item =>
            item.Slug == normalizedSlug &&
            item.IsEnabled &&
            !item.IsDeleted);

        return tag?.Id;
    }

    private static int GetPollDeadlineGroup(PostPoll? poll)
    {
        if (poll == null)
        {
            return 4;
        }

        var isClosed = IsPollClosed(poll);
        if (!isClosed && poll.EndTime.HasValue)
        {
            return 0;
        }

        if (!isClosed)
        {
            return 1;
        }

        if (poll.EndTime.HasValue)
        {
            return 2;
        }

        return 3;
    }

    private static long GetPollDeadlineTicksKey(PostPoll? poll)
    {
        if (poll == null)
        {
            return long.MaxValue;
        }

        var isClosed = IsPollClosed(poll);
        if (!isClosed && poll.EndTime.HasValue)
        {
            return poll.EndTime.Value.Ticks;
        }

        if (!isClosed)
        {
            return long.MaxValue;
        }

        if (poll.EndTime.HasValue)
        {
            return -poll.EndTime.Value.Ticks;
        }

        return long.MaxValue;
    }

    private async Task<Dictionary<long, int>> BuildLotteryParticipantCountMapAsync(
        Dictionary<long, long> postAuthorMap,
        Dictionary<long, PostLottery> lotteryMap)
    {
        if (_commentRepository == null || postAuthorMap.Count == 0 || lotteryMap.Count == 0)
        {
            return new Dictionary<long, int>();
        }

        var postIds = lotteryMap.Keys.ToList();
        var comments = await _commentRepository.QueryAsync(comment =>
            postIds.Contains(comment.PostId) &&
            comment.ParentId == null &&
            comment.AuthorId > 0 &&
            comment.IsEnabled &&
            !comment.IsDeleted);

        var participantCountMap = new Dictionary<long, int>();
        foreach (var postId in postIds)
        {
            if (!postAuthorMap.TryGetValue(postId, out var postAuthorId))
            {
                participantCountMap[postId] = lotteryMap.GetValueOrDefault(postId)?.ParticipantCount ?? 0;
                continue;
            }

            var lottery = lotteryMap.GetValueOrDefault(postId);
            var cutoffTime = lottery?.IsDrawn == true ? lottery.DrawnAt : null;
            var filteredComments = comments.Where(comment =>
                comment.PostId == postId &&
                comment.AuthorId != postAuthorId &&
                (!cutoffTime.HasValue || comment.CreateTime <= cutoffTime.Value));

            participantCountMap[postId] = filteredComments
                .Select(comment => comment.AuthorId)
                .Distinct()
                .Count();
        }

        return participantCountMap;
    }

    private async Task<List<Comment>> GetTopLevelLotteryCommentsAsync(long postId, long postAuthorId, DateTime? cutoffTimeUtc = null)
    {
        if (_commentRepository == null)
        {
            return [];
        }

        var hasCutoff = cutoffTimeUtc.HasValue;
        var cutoffValue = cutoffTimeUtc ?? DateTime.MaxValue;

        return await _commentRepository.QueryAsync(comment =>
            comment.PostId == postId &&
            comment.ParentId == null &&
            comment.AuthorId > 0 &&
            comment.AuthorId != postAuthorId &&
            comment.IsEnabled &&
            !comment.IsDeleted &&
            (!hasCutoff || comment.CreateTime <= cutoffValue));
    }

    private async Task FillPostGodCommentPreviewAsync(List<PostVo> posts)
    {
        if (posts.Count == 0)
        {
            return;
        }

        var postIds = posts
            .Select(post => post.VoId)
            .Where(postId => postId > 0)
            .Distinct()
            .ToList();

        if (postIds.Count == 0)
        {
            return;
        }

        var previewMap = await BuildGodCommentPreviewMapAsync(postIds);
        foreach (var post in posts)
        {
            if (previewMap.TryGetValue(post.VoId, out var highlight))
            {
                post.VoGodCommentId = highlight.CommentId;
                post.VoGodCommentAuthorName = highlight.AuthorName;
                post.VoGodCommentContentSnapshot = highlight.ContentSnapshot;
            }
            else
            {
                post.VoGodCommentId = null;
                post.VoGodCommentAuthorName = null;
                post.VoGodCommentContentSnapshot = null;
            }
        }
    }

    private async Task<Dictionary<long, CommentHighlight>> BuildGodCommentPreviewMapAsync(List<long> postIds)
    {
        if (_commentHighlightRepository == null || postIds.Count == 0)
        {
            return new Dictionary<long, CommentHighlight>();
        }

        var highlights = await _commentHighlightRepository.QueryAsync(highlight =>
            postIds.Contains(highlight.PostId) &&
            highlight.HighlightType == 1 &&
            highlight.IsCurrent);

        return highlights
            .GroupBy(highlight => highlight.PostId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(highlight => highlight.Rank)
                    .ThenBy(highlight => highlight.Id)
                    .First());
    }
}
