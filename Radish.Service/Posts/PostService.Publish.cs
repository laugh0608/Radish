using Radish.Common.AttributeTool;
using Radish.Model;
using Radish.Model.DtoModels;

namespace Radish.Service;

public partial class PostService
{
    /// <summary>
    /// 发布帖子
    /// </summary>
    [UseTran]
    public async Task<long> PublishPostAsync(
        Post post,
        CreatePollDto? poll = null,
        CreateLotteryDto? lottery = null,
        bool isQuestion = false,
        List<string>? tagNames = null,
        bool allowCreateTag = true)
    {
        var featureCount = (poll != null ? 1 : 0) + (lottery != null ? 1 : 0) + (isQuestion ? 1 : 0);
        if (featureCount > 1)
        {
            throw new ArgumentException("问答帖、投票和抽奖暂时互斥");
        }

        var normalizedTagNames = NormalizeTagNamesOrThrow(tagNames, nameof(tagNames), "发布帖子时至少需要一个标签");
        var operatorName = string.IsNullOrWhiteSpace(post.AuthorName) ? "System" : post.AuthorName;

        var postId = await AddAsync(post);

        await BindReferencedAttachmentsAsync(post.Content, BusinessType.Post, postId, post.AuthorId, operatorName, post.TenantId);

        if (post.CategoryId > 0)
        {
            var category = await _categoryRepository.QueryByIdAsync(post.CategoryId);
            if (category != null)
            {
                category.PostCount++;
                await _categoryRepository.UpdateAsync(category);
            }
        }

        await SyncPostTagsAsync(postId, post.AuthorId, operatorName, normalizedTagNames, allowCreateTag);

        if (poll != null)
        {
            await CreatePostPollAsync(post, postId, poll, operatorName);
        }

        if (isQuestion)
        {
            await CreatePostQuestionAsync(post, postId, operatorName);
        }

        if (lottery != null)
        {
            await CreatePostLotteryAsync(post, postId, lottery, operatorName);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                Serilog.Log.Information("准备发放发帖经验值：PostId={PostId}, UserId={UserId}", postId, post.AuthorId);

                var grantResult = await _experienceService.GrantExperienceAsync(
                    userId: post.AuthorId,
                    amount: 20,
                    expType: "POST_CREATE",
                    businessType: "Post",
                    businessId: postId,
                    remark: "发布帖子");

                if (grantResult)
                {
                    Serilog.Log.Information("发帖经验值发放成功：PostId={PostId}, UserId={UserId}, Amount=20",
                        postId, post.AuthorId);
                }
                else
                {
                    Serilog.Log.Warning("发帖经验值发放失败：PostId={PostId}, UserId={UserId}",
                        postId, post.AuthorId);
                }

                var userPostCount = await _postRepository.QueryCountAsync(p =>
                    p.AuthorId == post.AuthorId && !p.IsDeleted);

                Serilog.Log.Information("用户帖子数量统计：UserId={UserId}, PostCount={PostCount}",
                    post.AuthorId, userPostCount);

                if (userPostCount == 1)
                {
                    Serilog.Log.Information("检测到首次发帖，准备发放额外奖励：UserId={UserId}", post.AuthorId);

                    var firstPostResult = await _experienceService.GrantExperienceAsync(
                        userId: post.AuthorId,
                        amount: 30,
                        expType: "FIRST_POST",
                        businessType: "Post",
                        businessId: postId,
                        remark: "首次发帖奖励");

                    if (firstPostResult)
                    {
                        Serilog.Log.Information("首次发帖经验值奖励发放成功：PostId={PostId}, UserId={UserId}, Amount=30",
                            postId, post.AuthorId);
                    }
                    else
                    {
                        Serilog.Log.Warning("首次发帖经验值奖励发放失败：PostId={PostId}, UserId={UserId}",
                            postId, post.AuthorId);
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "发放发帖经验值失败：PostId={PostId}, UserId={UserId}, Message={Message}, StackTrace={StackTrace}",
                    postId, post.AuthorId, ex.Message, ex.StackTrace);
            }
        });

        return postId;
    }

    private async Task CreatePostPollAsync(Post post, long postId, CreatePollDto poll, string operatorName)
    {
        var question = poll.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("投票问题不能为空", nameof(poll));
        }

        if (poll.EndTime.HasValue && poll.EndTime.Value <= DateTime.UtcNow)
        {
            throw new ArgumentException("投票截止时间必须晚于当前时间", nameof(poll));
        }

        var normalizedOptions = NormalizePollOptionsOrThrow(poll.Options);

        var postPoll = new PostPoll
        {
            PostId = postId,
            Question = question,
            EndTime = poll.EndTime,
            IsClosed = false,
            TotalVoteCount = 0,
            TenantId = post.TenantId,
            CreateBy = operatorName,
            CreateId = post.AuthorId
        };

        var pollId = await _postPollRepository.AddAsync(postPoll);

        var optionEntities = normalizedOptions
            .Select((option, index) => new PostPollOption
            {
                PollId = pollId,
                OptionText = option.OptionText,
                SortOrder = option.SortOrder ?? index + 1,
                VoteCount = 0,
                TenantId = post.TenantId,
                CreateBy = operatorName,
                CreateId = post.AuthorId
            })
            .ToList();

        await _postPollOptionRepository.AddRangeAsync(optionEntities);
    }

    private async Task CreatePostQuestionAsync(Post post, long postId, string operatorName)
    {
        await _postQuestionRepository.AddAsync(new PostQuestion
        {
            PostId = postId,
            IsSolved = false,
            AcceptedAnswerId = null,
            AnswerCount = 0,
            TenantId = post.TenantId,
            CreateBy = operatorName,
            CreateId = post.AuthorId
        });
    }

    private async Task CreatePostLotteryAsync(Post post, long postId, CreateLotteryDto lottery, string operatorName)
    {
        if (_postLotteryRepository == null)
        {
            throw new InvalidOperationException("抽奖仓储未配置");
        }

        var prizeName = lottery.PrizeName?.Trim();
        if (string.IsNullOrWhiteSpace(prizeName))
        {
            throw new ArgumentException("奖品名称不能为空", nameof(lottery));
        }

        if (prizeName.Length > 100)
        {
            throw new ArgumentException("奖品名称长度不能超过100个字符", nameof(lottery));
        }

        var prizeDescription = string.IsNullOrWhiteSpace(lottery.PrizeDescription)
            ? null
            : lottery.PrizeDescription.Trim();
        if (prizeDescription != null && prizeDescription.Length > 500)
        {
            throw new ArgumentException("奖品说明长度不能超过500个字符", nameof(lottery));
        }

        var drawTime = lottery.DrawTime;
        if (drawTime.HasValue && drawTime.Value <= DateTime.UtcNow)
        {
            throw new ArgumentException("抽奖可开奖时间必须晚于当前时间", nameof(lottery));
        }

        var winnerCount = Math.Clamp(lottery.WinnerCount, 1, 20);
        await _postLotteryRepository.AddAsync(new PostLottery
        {
            PostId = postId,
            PrizeName = prizeName,
            PrizeDescription = prizeDescription,
            DrawTime = drawTime,
            WinnerCount = winnerCount,
            ParticipantCount = 0,
            IsDrawn = false,
            TenantId = post.TenantId,
            CreateTime = DateTime.Now,
            CreateBy = operatorName,
            CreateId = post.AuthorId
        });
    }

    private static List<PollOptionDto> NormalizePollOptionsOrThrow(List<PollOptionDto>? options)
    {
        if (options == null)
        {
            throw new ArgumentException("投票选项不能为空", nameof(options));
        }

        var normalizedOptions = options
            .Where(option => !string.IsNullOrWhiteSpace(option.OptionText))
            .Select((option, index) => new PollOptionDto
            {
                OptionText = option.OptionText.Trim(),
                SortOrder = option.SortOrder ?? index + 1
            })
            .ToList();

        if (normalizedOptions.Count < MinPollOptionCount || normalizedOptions.Count > MaxPollOptionCount)
        {
            throw new ArgumentException($"投票选项数量必须在 {MinPollOptionCount} 到 {MaxPollOptionCount} 个之间", nameof(options));
        }

        var distinctCount = normalizedOptions
            .Select(option => option.OptionText)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (distinctCount != normalizedOptions.Count)
        {
            throw new ArgumentException("投票选项不能重复", nameof(options));
        }

        return normalizedOptions;
    }

    private async Task BindReferencedAttachmentsAsync(
        string? content,
        string businessType,
        long businessId,
        long operatorId,
        string operatorName,
        long tenantId)
    {
        if (_attachmentRepository == null || businessId <= 0 || operatorId <= 0)
        {
            return;
        }

        var referencedAttachmentIds = AttachmentReferenceHelper.ExtractAttachmentIds(content);
        if (referencedAttachmentIds.Count == 0)
        {
            return;
        }

        var normalizedTenantId = tenantId > 0 ? tenantId : 0;
        var attachments = await _attachmentRepository.QueryAsync(a =>
            !a.IsDeleted &&
            !a.BusinessId.HasValue &&
            a.TenantId == normalizedTenantId &&
            a.UploaderId == operatorId);

        foreach (var attachment in attachments.Where(a => AttachmentReferenceHelper.IsAttachmentReferenced(a, referencedAttachmentIds)))
        {
            attachment.BusinessType = businessType;
            attachment.BusinessId = businessId;
            attachment.ModifyTime = DateTime.Now;
            attachment.ModifyBy = operatorName;
            attachment.ModifyId = operatorId;
            await _attachmentRepository.UpdateAsync(attachment);
        }
    }

    private static List<string> NormalizeTagNamesOrThrow(List<string>? tagNames, string parameterName, string emptyMessage)
    {
        if (tagNames == null)
        {
            throw new ArgumentException(emptyMessage, parameterName);
        }

        var normalizedTagNames = tagNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedTagNames.Count is < 1 or > 5)
        {
            throw new ArgumentException("标签数量必须在 1 到 5 个之间", parameterName);
        }

        return normalizedTagNames;
    }

    private async Task<Tag> ResolveTagByNameAsync(string tagName, bool allowCreateTag)
    {
        var existingTags = await _tagRepository.QueryAsync(t => t.Name == tagName && t.IsEnabled && !t.IsDeleted);
        var tag = existingTags.FirstOrDefault();
        if (tag != null)
        {
            return tag;
        }

        if (!allowCreateTag)
        {
            throw new InvalidOperationException($"标签不存在且当前用户无权限创建：{tagName}");
        }

        return await _tagService.GetOrCreateTagAsync(tagName);
    }

    private async Task SyncPostTagsAsync(
        long postId,
        long operatorId,
        string operatorName,
        List<string> normalizedTagNames,
        bool allowCreateTag)
    {
        var existingPostTags = await _postTagRepository.QueryAsync(pt => pt.PostId == postId);
        var existingTagIdSet = existingPostTags.Select(pt => pt.TagId).ToHashSet();

        var desiredTags = new Dictionary<long, Tag>();
        foreach (var tagName in normalizedTagNames)
        {
            var tag = await ResolveTagByNameAsync(tagName, allowCreateTag);
            if (!desiredTags.ContainsKey(tag.Id))
            {
                desiredTags[tag.Id] = tag;
            }
        }

        var desiredTagIdSet = desiredTags.Keys.ToHashSet();

        var relationsToRemove = existingPostTags
            .Where(pt => !desiredTagIdSet.Contains(pt.TagId))
            .ToList();

        if (relationsToRemove.Any())
        {
            var removeCountByTagId = relationsToRemove
                .GroupBy(pt => pt.TagId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var relation in relationsToRemove)
            {
#pragma warning disable CS0618
                await _postTagRepository.DeleteByIdAsync(relation.Id);
#pragma warning restore CS0618
            }

            var removedTagIds = removeCountByTagId.Keys.ToList();
            var removedTags = await _tagRepository.QueryAsync(t => removedTagIds.Contains(t.Id) && !t.IsDeleted);
            foreach (var removedTag in removedTags)
            {
                if (!removeCountByTagId.TryGetValue(removedTag.Id, out var removeCount))
                {
                    continue;
                }

                removedTag.PostCount = Math.Max(0, removedTag.PostCount - removeCount);
                await _tagRepository.UpdateAsync(removedTag);
            }
        }

        var tagIdsToAdd = desiredTagIdSet
            .Where(tagId => !existingTagIdSet.Contains(tagId))
            .ToList();

        foreach (var tagId in tagIdsToAdd)
        {
            var tag = desiredTags[tagId];
            var postTag = new PostTag(postId, tag.Id)
            {
                CreateId = operatorId,
                CreateBy = operatorName
            };

            await _postTagRepository.AddAsync(postTag);
            tag.PostCount++;
            await _tagRepository.UpdateAsync(tag);
        }
    }
}
