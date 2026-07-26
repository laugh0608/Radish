using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;

namespace Radish.Service;

public partial class PostService
{
    /// <summary>
    /// 更新帖子及标签
    /// </summary>
    [UseTran]
    public async Task UpdatePostAsync(
        long postId,
        string title,
        string content,
        long? categoryId,
        List<string>? tagNames,
        bool allowCreateTag,
        long operatorId,
        string operatorName,
        bool isAdmin = false,
        int expectedContentRevision = 1,
        bool updateCover = false,
        long? coverAttachmentId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("帖子标题不能为空", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("帖子内容不能为空", nameof(content));
        }

        var normalizedTagNames = NormalizeTagNamesOrThrow(tagNames, nameof(tagNames), "编辑帖子时至少需要一个标签");

        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted)
        {
            throw new InvalidOperationException("帖子不存在");
        }

        if (expectedContentRevision <= 0 || post.ContentRevision != expectedContentRevision)
        {
            throw CreateRevisionConflictException();
        }

        var trimmedTitle = title.Trim();
        var trimmedContent = content.Trim();
        var targetCategoryId = categoryId ?? post.CategoryId;
        if (await IsPostEditNoChangeAsync(
                post,
                trimmedTitle,
                trimmedContent,
                targetCategoryId,
                normalizedTagNames,
                updateCover,
                coverAttachmentId))
        {
            return;
        }

        var contentSettings = await ValidatePostContentSettingsAsync(trimmedTitle, trimmedContent);

        var postOptions = _editHistoryOptions.Post;
        var existingEditCount = post.EditCount;

        if (!isAdmin || !_editHistoryOptions.AdminOverride.BypassEditCountLimit)
        {
            if (existingEditCount >= Math.Max(0, postOptions.MaxEditCount))
            {
                throw new InvalidOperationException("帖子编辑次数已达上限，无法继续编辑");
            }
        }

        if (targetCategoryId <= 0)
        {
            throw new InvalidOperationException("帖子分类不存在或不可用");
        }

        var targetCategory = await _categoryRepository.QueryByIdAsync(targetCategoryId);
        if (targetCategory == null || targetCategory.IsDeleted || !targetCategory.IsEnabled)
        {
            throw new InvalidOperationException("帖子分类不存在或不可用");
        }

        if (targetCategoryId != post.CategoryId)
        {
            var oldCategory = await _categoryRepository.QueryByIdAsync(post.CategoryId);
            if (oldCategory != null)
            {
                oldCategory.PostCount = Math.Max(0, oldCategory.PostCount - 1);
                await _categoryRepository.UpdateAsync(oldCategory);
            }

            targetCategory.PostCount++;
            await _categoryRepository.UpdateAsync(targetCategory);
        }

        var safeOperatorName = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName;
        var nextEditSequence = existingEditCount + 1;

        post.Title = trimmedTitle;
        post.Content = trimmedContent;
        ApplyPostSummarySettings(post, contentSettings);
        post.CategoryId = targetCategoryId;
        post.EditCount = nextEditSequence;
        post.ContentRevision = expectedContentRevision + 1;
        if (updateCover)
        {
            post.CoverAttachmentId = coverAttachmentId;
        }
        post.ModifyTime = DateTime.Now;
        post.ModifyBy = safeOperatorName;
        post.ModifyId = operatorId;

        var affectedRows = await _postRepository.UpdateColumnsAsync(
            current => new Post
            {
                Title = post.Title,
                Content = post.Content,
                Summary = post.Summary,
                CategoryId = post.CategoryId,
                CoverAttachmentId = post.CoverAttachmentId,
                EditCount = post.EditCount,
                ContentRevision = post.ContentRevision,
                ModifyTime = post.ModifyTime,
                ModifyBy = post.ModifyBy,
                ModifyId = post.ModifyId
            },
            current =>
                current.Id == postId &&
                !current.IsDeleted &&
                current.ContentRevision == expectedContentRevision);
        if (affectedRows != 1)
        {
            throw CreateRevisionConflictException();
        }

        await BindReferencedAttachmentsAsync(trimmedContent, BusinessType.Post, postId, operatorId, safeOperatorName, post.TenantId);
        await SyncPostTagsAsync(postId, operatorId, safeOperatorName, normalizedTagNames, allowCreateTag);
    }

    private async Task<bool> IsPostEditNoChangeAsync(
        Post post,
        string trimmedTitle,
        string trimmedContent,
        long targetCategoryId,
        List<string> normalizedTagNames,
        bool updateCover,
        long? coverAttachmentId)
    {
        if (!string.Equals(post.Title?.Trim(), trimmedTitle, StringComparison.Ordinal) ||
            !string.Equals(post.Content?.Trim(), trimmedContent, StringComparison.Ordinal) ||
            post.CategoryId != targetCategoryId ||
            (updateCover && post.CoverAttachmentId != coverAttachmentId))
        {
            return false;
        }

        var existingPostTags = await _postTagRepository.QueryAsync(pt => pt.PostId == post.Id);
        var existingTagIds = existingPostTags
            .Select(postTag => postTag.TagId)
            .Distinct()
            .ToList();
        var existingTagNames = new List<string>();
        if (existingTagIds.Count > 0)
        {
            var existingTags = await _tagRepository.QueryAsync(tag => existingTagIds.Contains(tag.Id) && !tag.IsDeleted);
            existingTagNames = existingTags
                .Select(tag => tag.Name.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var requestedTagNames = normalizedTagNames
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return existingTagNames.SequenceEqual(requestedTagNames, StringComparer.OrdinalIgnoreCase);
    }

    private static BusinessException CreateRevisionConflictException()
    {
        return new BusinessException(
            "内容已被更新，请刷新后重试",
            409,
            ForumContentRevisionErrorCodes.Conflict,
            ForumContentRevisionErrorCodes.ResolveMessageKey(ForumContentRevisionErrorCodes.Conflict));
    }

    public async Task<(List<PostEditHistoryVo> histories, int total)> GetPostEditHistoryPageAsync(long postId, int pageIndex, int pageSize)
    {
        var safePageIndex = pageIndex < 1 ? 1 : pageIndex;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var (histories, total) = await _postEditHistoryRepository.QueryPageAsync(
            h => h.PostId == postId,
            safePageIndex,
            safePageSize,
            h => h.EditSequence,
            SqlSugar.OrderByType.Desc,
            h => h.CreateTime,
            SqlSugar.OrderByType.Desc);

        return (Mapper.Map<List<PostEditHistoryVo>>(histories), total);
    }

    private async Task TrimPostHistoryAsync(long postId, int maxHistoryRecords)
    {
        var histories = await _postEditHistoryRepository.QueryWithOrderAsync(
            h => h.PostId == postId,
            h => h.EditSequence,
            SqlSugar.OrderByType.Desc);

        if (histories.Count <= maxHistoryRecords)
        {
            return;
        }

        var removeIds = histories
            .Skip(maxHistoryRecords)
            .Select(h => h.Id)
            .ToList();

#pragma warning disable CS0618
        await _postEditHistoryRepository.DeleteByIdsAsync(removeIds);
#pragma warning restore CS0618
    }
}
