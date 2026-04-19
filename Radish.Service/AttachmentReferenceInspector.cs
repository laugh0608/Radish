using System.Linq.Expressions;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;

namespace Radish.Service;

/// <summary>
/// 统一汇总附件在结构化字段、消息快照和内容 markdown 中的引用情况。
/// </summary>
public class AttachmentReferenceInspector : IAttachmentReferenceInspector
{
    private readonly IBaseRepository<Sticker> _stickerRepository;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IBaseRepository<PostAnswer> _postAnswerRepository;
    private readonly IBaseRepository<ChannelMessage> _channelMessageRepository;
    private readonly IBaseRepository<Product> _productRepository;
    private readonly IBaseRepository<ProductCategory> _productCategoryRepository;
    private readonly IBaseRepository<Category> _categoryRepository;
    private readonly IBaseRepository<LevelConfig> _levelConfigRepository;
    private readonly IBaseRepository<StickerGroup> _stickerGroupRepository;
    private readonly IBaseRepository<Reaction> _reactionRepository;
    private readonly IBaseRepository<WikiDocument> _wikiDocumentRepository;
    private readonly IBaseRepository<UserBrowseHistory> _userBrowseHistoryRepository;
    private readonly IBaseRepository<UserBenefit> _userBenefitRepository;
    private readonly IBaseRepository<UserInventory> _userInventoryRepository;
    private readonly IBaseRepository<Order> _orderRepository;

    public AttachmentReferenceInspector(
        IBaseRepository<Sticker> stickerRepository,
        IBaseRepository<Post> postRepository,
        IBaseRepository<Comment> commentRepository,
        IBaseRepository<PostAnswer> postAnswerRepository,
        IBaseRepository<ChannelMessage> channelMessageRepository,
        IBaseRepository<Product> productRepository,
        IBaseRepository<ProductCategory> productCategoryRepository,
        IBaseRepository<Category> categoryRepository,
        IBaseRepository<LevelConfig> levelConfigRepository,
        IBaseRepository<StickerGroup> stickerGroupRepository,
        IBaseRepository<Reaction> reactionRepository,
        IBaseRepository<WikiDocument> wikiDocumentRepository,
        IBaseRepository<UserBrowseHistory> userBrowseHistoryRepository,
        IBaseRepository<UserBenefit> userBenefitRepository,
        IBaseRepository<UserInventory> userInventoryRepository,
        IBaseRepository<Order> orderRepository)
    {
        _stickerRepository = stickerRepository;
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _postAnswerRepository = postAnswerRepository;
        _channelMessageRepository = channelMessageRepository;
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
        _categoryRepository = categoryRepository;
        _levelConfigRepository = levelConfigRepository;
        _stickerGroupRepository = stickerGroupRepository;
        _reactionRepository = reactionRepository;
        _wikiDocumentRepository = wikiDocumentRepository;
        _userBrowseHistoryRepository = userBrowseHistoryRepository;
        _userBenefitRepository = userBenefitRepository;
        _userInventoryRepository = userInventoryRepository;
        _orderRepository = orderRepository;
    }

    public async Task<HashSet<long>> GetReferencedAttachmentIdsAsync(IReadOnlyCollection<long> attachmentIds)
    {
        var candidateIds = attachmentIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (candidateIds.Count == 0)
        {
            return new HashSet<long>();
        }

        var referencedAttachmentIds = new HashSet<long>();

        await UnionStructuredReferencesAsync(referencedAttachmentIds, candidateIds);

        await UnionReferencedAttachmentIdsFromContentAsync(
            referencedAttachmentIds,
            _postRepository,
            static post => post.Content,
            static post => !post.IsDeleted && post.Content != null && post.Content != string.Empty);

        await UnionReferencedAttachmentIdsFromContentAsync(
            referencedAttachmentIds,
            _commentRepository,
            static comment => comment.Content,
            static comment => !comment.IsDeleted && comment.Content != null && comment.Content != string.Empty);

        await UnionReferencedAttachmentIdsFromContentAsync(
            referencedAttachmentIds,
            _postAnswerRepository,
            static answer => answer.Content,
            static answer => !answer.IsDeleted && answer.Content != null && answer.Content != string.Empty);

        return referencedAttachmentIds;
    }

    public async Task<bool> IsReferencedAsync(long attachmentId)
    {
        if (attachmentId <= 0)
        {
            return false;
        }

        var referencedAttachmentIds = await GetReferencedAttachmentIdsAsync(new[] { attachmentId });
        return referencedAttachmentIds.Contains(attachmentId);
    }

    private async Task UnionStructuredReferencesAsync(ISet<long> referencedAttachmentIds, IReadOnlyCollection<long> candidateIds)
    {
        var stickers = await _stickerRepository.QueryAsync(sticker =>
            sticker.AttachmentId.HasValue &&
            candidateIds.Contains(sticker.AttachmentId.Value) &&
            !sticker.IsDeleted);
        UnionNullableIds(referencedAttachmentIds, stickers.Select(sticker => sticker.AttachmentId));

        var messages = await _channelMessageRepository.QueryAsync(message =>
            !message.IsDeleted &&
            ((message.AttachmentId.HasValue && candidateIds.Contains(message.AttachmentId.Value)) ||
             (message.UserAvatarAttachmentIdSnapshot.HasValue && candidateIds.Contains(message.UserAvatarAttachmentIdSnapshot.Value))));
        UnionNullableIds(referencedAttachmentIds, messages.Select(message => message.AttachmentId));
        UnionNullableIds(referencedAttachmentIds, messages.Select(message => message.UserAvatarAttachmentIdSnapshot));

        var posts = await _postRepository.QueryAsync(post =>
            !post.IsDeleted &&
            post.CoverAttachmentId.HasValue &&
            candidateIds.Contains(post.CoverAttachmentId.Value));
        UnionNullableIds(referencedAttachmentIds, posts.Select(post => post.CoverAttachmentId));

        var products = await _productRepository.QueryAsync(product =>
            !product.IsDeleted &&
            ((product.IconAttachmentId.HasValue && candidateIds.Contains(product.IconAttachmentId.Value)) ||
             (product.CoverAttachmentId.HasValue && candidateIds.Contains(product.CoverAttachmentId.Value))));
        UnionNullableIds(referencedAttachmentIds, products.Select(product => product.IconAttachmentId));
        UnionNullableIds(referencedAttachmentIds, products.Select(product => product.CoverAttachmentId));

        var productCategories = await _productCategoryRepository.QueryAsync(category =>
            category.IconAttachmentId.HasValue &&
            candidateIds.Contains(category.IconAttachmentId.Value));
        UnionNullableIds(referencedAttachmentIds, productCategories.Select(category => category.IconAttachmentId));

        var forumCategories = await _categoryRepository.QueryAsync(category =>
            !category.IsDeleted &&
            ((category.IconAttachmentId.HasValue && candidateIds.Contains(category.IconAttachmentId.Value)) ||
             (category.CoverAttachmentId.HasValue && candidateIds.Contains(category.CoverAttachmentId.Value))));
        UnionNullableIds(referencedAttachmentIds, forumCategories.Select(category => category.IconAttachmentId));
        UnionNullableIds(referencedAttachmentIds, forumCategories.Select(category => category.CoverAttachmentId));

        var levelConfigs = await _levelConfigRepository.QueryAsync(levelConfig =>
            (levelConfig.IconAttachmentId.HasValue && candidateIds.Contains(levelConfig.IconAttachmentId.Value)) ||
            (levelConfig.BadgeAttachmentId.HasValue && candidateIds.Contains(levelConfig.BadgeAttachmentId.Value)));
        UnionNullableIds(referencedAttachmentIds, levelConfigs.Select(levelConfig => levelConfig.IconAttachmentId));
        UnionNullableIds(referencedAttachmentIds, levelConfigs.Select(levelConfig => levelConfig.BadgeAttachmentId));

        var stickerGroups = await _stickerGroupRepository.QueryAsync(group =>
            !group.IsDeleted &&
            group.CoverAttachmentId.HasValue &&
            candidateIds.Contains(group.CoverAttachmentId.Value));
        UnionNullableIds(referencedAttachmentIds, stickerGroups.Select(group => group.CoverAttachmentId));

        var reactions = await _reactionRepository.QueryAsync(reaction =>
            !reaction.IsDeleted &&
            reaction.StickerAttachmentId.HasValue &&
            candidateIds.Contains(reaction.StickerAttachmentId.Value));
        UnionNullableIds(referencedAttachmentIds, reactions.Select(reaction => reaction.StickerAttachmentId));

        var wikiDocuments = await _wikiDocumentRepository.QueryAsync(document =>
            !document.IsDeleted &&
            document.CoverAttachmentId.HasValue &&
            candidateIds.Contains(document.CoverAttachmentId.Value));
        UnionNullableIds(referencedAttachmentIds, wikiDocuments.Select(document => document.CoverAttachmentId));

        var browseHistories = await _userBrowseHistoryRepository.QueryAsync(item =>
            !item.IsDeleted &&
            item.CoverAttachmentId.HasValue &&
            candidateIds.Contains(item.CoverAttachmentId.Value));
        UnionNullableIds(referencedAttachmentIds, browseHistories.Select(item => item.CoverAttachmentId));

        var benefits = await _userBenefitRepository.QueryAsync(item =>
            !item.IsDeleted &&
            item.BenefitIconAttachmentId.HasValue &&
            candidateIds.Contains(item.BenefitIconAttachmentId.Value));
        UnionNullableIds(referencedAttachmentIds, benefits.Select(item => item.BenefitIconAttachmentId));

        var inventoryItems = await _userInventoryRepository.QueryAsync(item =>
            !item.IsDeleted &&
            item.ItemIconAttachmentId.HasValue &&
            candidateIds.Contains(item.ItemIconAttachmentId.Value));
        UnionNullableIds(referencedAttachmentIds, inventoryItems.Select(item => item.ItemIconAttachmentId));

        var orders = await _orderRepository.QueryAsync(order =>
            !order.IsDeleted &&
            order.ProductIconAttachmentId.HasValue &&
            candidateIds.Contains(order.ProductIconAttachmentId.Value));
        UnionNullableIds(referencedAttachmentIds, orders.Select(order => order.ProductIconAttachmentId));
    }

    private static void UnionNullableIds(ISet<long> target, IEnumerable<long?> source)
    {
        foreach (var id in source)
        {
            if (id.HasValue && id.Value > 0)
            {
                target.Add(id.Value);
            }
        }
    }

    private static async Task UnionReferencedAttachmentIdsFromContentAsync<TEntity>(
        ISet<long> referencedAttachmentIds,
        IBaseRepository<TEntity> repository,
        Func<TEntity, string?> contentSelector,
        Expression<Func<TEntity, bool>> predicate)
        where TEntity : class, new()
    {
        var records = await repository.QueryAsync(predicate);
        foreach (var record in records)
        {
            var contentAttachmentIds = AttachmentReferenceHelper.ExtractAttachmentIds(contentSelector(record));
            foreach (var attachmentId in contentAttachmentIds)
            {
                referencedAttachmentIds.Add(attachmentId);
            }
        }
    }
}
