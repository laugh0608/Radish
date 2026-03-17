using Radish.Common.AttributeTool;
using Radish.Model;
using Radish.Model.ViewModels;

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
        bool isAdmin = false)
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

        var postOptions = _editHistoryOptions.Post;
        var historyEnabled = _editHistoryOptions.Enable && postOptions.EnableHistory;
        var historyEditCount = await _postEditHistoryRepository.QueryCountAsync(h => h.PostId == postId);
        var existingEditCount = Math.Max(post.EditCount, historyEditCount);

        if (!isAdmin || !_editHistoryOptions.AdminOverride.BypassEditCountLimit)
        {
            if (existingEditCount >= Math.Max(0, postOptions.MaxEditCount))
            {
                throw new InvalidOperationException("帖子编辑次数已达上限，无法继续编辑");
            }
        }

        var targetCategoryId = categoryId ?? post.CategoryId;
        if (targetCategoryId > 0 && targetCategoryId != post.CategoryId)
        {
            var oldCategory = await _categoryRepository.QueryByIdAsync(post.CategoryId);
            if (oldCategory != null)
            {
                oldCategory.PostCount = Math.Max(0, oldCategory.PostCount - 1);
                await _categoryRepository.UpdateAsync(oldCategory);
            }

            var newCategory = await _categoryRepository.QueryByIdAsync(targetCategoryId);
            if (newCategory != null)
            {
                newCategory.PostCount++;
                await _categoryRepository.UpdateAsync(newCategory);
            }
        }

        var safeOperatorName = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName;
        var trimmedTitle = title.Trim();
        var trimmedContent = content.Trim();
        var nextEditSequence = existingEditCount + 1;

        if (historyEnabled && nextEditSequence <= Math.Max(0, postOptions.HistorySaveEditCount))
        {
            await _postEditHistoryRepository.AddAsync(new PostEditHistory
            {
                PostId = postId,
                EditSequence = nextEditSequence,
                OldTitle = post.Title,
                NewTitle = trimmedTitle,
                OldContent = post.Content,
                NewContent = trimmedContent,
                EditorId = operatorId,
                EditorName = safeOperatorName,
                EditedAt = DateTime.Now,
                TenantId = post.TenantId,
                CreateTime = DateTime.Now,
                CreateBy = safeOperatorName,
                CreateId = operatorId
            });
        }

        post.Title = trimmedTitle;
        post.Content = trimmedContent;
        post.CategoryId = targetCategoryId;
        post.EditCount = nextEditSequence;
        post.ModifyTime = DateTime.Now;
        post.ModifyBy = safeOperatorName;
        post.ModifyId = operatorId;

        await _postRepository.UpdateAsync(post);
        await BindReferencedAttachmentsAsync(trimmedContent, BusinessType.Post, postId, operatorId, safeOperatorName, post.TenantId);
        await SyncPostTagsAsync(postId, operatorId, safeOperatorName, normalizedTagNames, allowCreateTag);

        if (historyEnabled)
        {
            await TrimPostHistoryAsync(postId, Math.Max(1, postOptions.MaxHistoryRecords));
        }
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
