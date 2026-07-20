using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

public partial class PostService
{
    private static readonly TimeSpan MinLotteryLeadTime = TimeSpan.FromHours(1);
    private const string PostPublicIdPrefix = "pst_";
    private const int MaxPollQuestionLength = 200;
    private const int MaxPollOptionLength = 100;
    private const int MinLotteryWinnerCount = 1;
    private const int MaxLotteryWinnerCount = 20;

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
        if (string.IsNullOrWhiteSpace(post.Title))
        {
            throw CreatePublishException(
                "帖子标题不能为空",
                ForumPublishErrorCodes.TitleRequired);
        }

        if (string.IsNullOrWhiteSpace(post.Content))
        {
            throw CreatePublishException(
                "帖子内容不能为空",
                ForumPublishErrorCodes.ContentRequired);
        }

        var featureCount = (poll != null ? 1 : 0) + (lottery != null ? 1 : 0) + (isQuestion ? 1 : 0);
        if (featureCount > 1)
        {
            throw CreatePublishException(
                "问答帖、投票和抽奖暂时互斥",
                ForumPublishErrorCodes.FeatureCombinationInvalid);
        }

        var normalizedTagNames = NormalizePublishTagNamesOrThrow(tagNames);
        var contentSettings = await ValidatePublishPostContentSettingsAsync(post.Title, post.Content);
        ApplyPostSummarySettings(post, contentSettings);

        var operatorName = string.IsNullOrWhiteSpace(post.AuthorName) ? "System" : post.AuthorName;
        post.PublicId = EnsurePostPublicId(post.PublicId);

        if (poll != null)
        {
            ValidatePollDefinitionOrThrow(poll);
        }

        if (lottery != null)
        {
            ValidateLotteryDefinitionOrThrow(lottery);
        }

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

        var reliableOutboxService = _reliableOutboxService
            ?? throw new InvalidOperationException("可靠 Outbox 服务未注册");
        await reliableOutboxService.AddAsync(
            ReliableOutboxSources.Main,
            post.TenantId,
            ReliableTaskTypes.PostPublished,
            $"task:post-published:{postId}",
            "Post",
            postId.ToString(),
            new PostPublishedTaskPayload(postId, post.AuthorId),
            DateTime.UtcNow);

        return postId;
    }

    private async Task CreatePostPollAsync(Post post, long postId, CreatePollDto poll, string operatorName)
    {
        ValidatePollDefinitionOrThrow(poll);
        var question = poll.Question!.Trim();

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
        ValidateLotteryDefinitionOrThrow(lottery);

        var prizeName = lottery.PrizeName!.Trim();
        var prizeDescription = string.IsNullOrWhiteSpace(lottery.PrizeDescription)
            ? null
            : lottery.PrizeDescription.Trim();
        var drawTime = lottery.DrawTime;

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

    private static void ValidatePollDefinitionOrThrow(CreatePollDto poll)
    {
        var question = poll.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            throw CreatePublishException(
                "投票问题不能为空",
                ForumPublishErrorCodes.PollQuestionRequired);
        }

        if (question.Length > MaxPollQuestionLength)
        {
            throw CreatePublishException(
                $"投票问题不能超过 {MaxPollQuestionLength} 个字符",
                ForumPublishErrorCodes.PollQuestionTooLong);
        }

        if (poll.EndTime.HasValue && poll.EndTime.Value <= DateTime.UtcNow)
        {
            throw CreatePublishException(
                "投票截止时间必须晚于当前时间",
                ForumPublishErrorCodes.PollEndTimeInvalid);
        }
    }

    private static void ValidateLotteryDefinitionOrThrow(CreateLotteryDto lottery)
    {
        var prizeName = lottery.PrizeName?.Trim();
        if (string.IsNullOrWhiteSpace(prizeName))
        {
            throw CreatePublishException(
                "奖品名称不能为空",
                ForumPublishErrorCodes.LotteryPrizeNameRequired);
        }

        if (prizeName.Length > 100)
        {
            throw CreatePublishException(
                "奖品名称长度不能超过100个字符",
                ForumPublishErrorCodes.LotteryPrizeNameTooLong);
        }

        var prizeDescription = string.IsNullOrWhiteSpace(lottery.PrizeDescription)
            ? null
            : lottery.PrizeDescription.Trim();
        if (prizeDescription != null && prizeDescription.Length > 500)
        {
            throw CreatePublishException(
                "奖品说明长度不能超过500个字符",
                ForumPublishErrorCodes.LotteryPrizeDescriptionTooLong);
        }

        if (lottery.WinnerCount is < MinLotteryWinnerCount or > MaxLotteryWinnerCount)
        {
            throw CreatePublishException(
                $"中奖人数必须在 {MinLotteryWinnerCount} 到 {MaxLotteryWinnerCount} 之间",
                ForumPublishErrorCodes.LotteryWinnerCountInvalid);
        }

        var drawTime = lottery.DrawTime;
        if (!drawTime.HasValue)
        {
            throw CreatePublishException(
                "抽奖截止时间不能为空",
                ForumPublishErrorCodes.LotteryDrawTimeRequired);
        }

        var minimumDrawTime = DateTime.UtcNow.Add(MinLotteryLeadTime);
        if (drawTime.Value < minimumDrawTime)
        {
            throw CreatePublishException(
                "抽奖截止时间必须至少晚于发帖时间 1 小时",
                ForumPublishErrorCodes.LotteryDrawTimeTooSoon);
        }
    }

    private static List<PollOptionDto> NormalizePollOptionsOrThrow(List<PollOptionDto>? options)
    {
        if (options == null)
        {
            throw CreatePublishException(
                "投票选项不能为空",
                ForumPublishErrorCodes.PollOptionsRequired);
        }

        var providedOptions = options.OfType<PollOptionDto>().ToList();
        if (providedOptions.Count != options.Count)
        {
            throw CreatePublishException(
                "投票选项不能为空",
                ForumPublishErrorCodes.PollOptionsRequired);
        }

        var normalizedOptions = providedOptions
            .Where(option => !string.IsNullOrWhiteSpace(option.OptionText))
            .Select((option, index) => new PollOptionDto
            {
                OptionText = option.OptionText.Trim(),
                SortOrder = option.SortOrder ?? index + 1
            })
            .ToList();

        if (normalizedOptions.Count < MinPollOptionCount || normalizedOptions.Count > MaxPollOptionCount)
        {
            throw CreatePublishException(
                $"投票选项数量必须在 {MinPollOptionCount} 到 {MaxPollOptionCount} 个之间",
                ForumPublishErrorCodes.PollOptionCountInvalid);
        }

        if (normalizedOptions.Any(option => option.OptionText.Length > MaxPollOptionLength))
        {
            throw CreatePublishException(
                $"单个投票选项不能超过 {MaxPollOptionLength} 个字符",
                ForumPublishErrorCodes.PollOptionTooLong);
        }

        var distinctCount = normalizedOptions
            .Select(option => option.OptionText)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (distinctCount != normalizedOptions.Count)
        {
            throw CreatePublishException(
                "投票选项不能重复",
                ForumPublishErrorCodes.PollOptionsDuplicate);
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

    private static List<string> NormalizePublishTagNamesOrThrow(List<string>? tagNames)
    {
        var normalizedTagNames = tagNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (normalizedTagNames.Count is < 1 or > 5)
        {
            throw CreatePublishException(
                "发布帖子时标签数量必须在 1 到 5 个之间",
                ForumPublishErrorCodes.TagCountInvalid);
        }

        return normalizedTagNames;
    }

    private static string EnsurePostPublicId(string? currentPublicId)
    {
        var normalizedCurrent = currentPublicId?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedCurrent))
        {
            return normalizedCurrent;
        }

        return GeneratePostPublicId();
    }

    private static string GeneratePostPublicId()
    {
        return $"{PostPublicIdPrefix}{Guid.CreateVersion7():N}";
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
            throw CreatePublishException(
                $"标签不存在且当前用户无权限创建：{tagName}",
                ForumPublishErrorCodes.TagCreateForbidden,
                HttpStatusCodeEnum.Forbidden);
        }

        return await _tagService.GetOrCreateTagAsync(tagName);
    }

    private static BusinessException CreatePublishException(
        string fallbackMessage,
        string errorCode,
        HttpStatusCodeEnum statusCode = HttpStatusCodeEnum.BadRequest)
    {
        return new BusinessException(
            fallbackMessage,
            (int)statusCode,
            errorCode,
            ForumPublishErrorCodes.ResolveMessageKey(errorCode));
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
