using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public class AttachmentReferenceInspectorTest
{
    [Fact]
    public async Task GetReferencedAttachmentIdsAsync_Should_Collect_Content_And_Message_References()
    {
        var now = DateTime.Now;

        var inspector = CreateInspector(
            postRepository: CreateRepositoryMock(new List<Post>
            {
                new()
                {
                    Id = 1,
                    Content = "![cover](attachment://1001)",
                    CreateTime = now
                }
            }),
            postAnswerRepository: CreateRepositoryMock(new List<PostAnswer>
            {
                new()
                {
                    Id = 2,
                    PostId = 3,
                    AuthorId = 9527,
                    AuthorName = "tester",
                    Content = "![answer](attachment://1002#radish:display=thumbnail)",
                    CreateTime = now
                }
            }),
            channelMessageRepository: CreateRepositoryMock(new List<ChannelMessage>
            {
                new()
                {
                    Id = 4,
                    ChannelId = 5,
                    UserId = 9527,
                    UserName = "tester",
                    Type = MessageType.Image,
                    AttachmentId = 1003,
                    UserAvatarAttachmentIdSnapshot = 1004,
                    CreateTime = now
                }
            }));

        var referencedIds = await inspector.GetReferencedAttachmentIdsAsync(new long[] { 1001, 1002, 1003, 1004, 1999 });

        Assert.Equal(new HashSet<long> { 1001, 1002, 1003, 1004 }, referencedIds);
    }

    [Fact]
    public async Task GetReferencedAttachmentIdsAsync_Should_Collect_Structured_Business_References()
    {
        var inspector = CreateInspector(
            productRepository: CreateRepositoryMock(new List<Product>
            {
                new()
                {
                    Id = 100001,
                    Name = "测试商品",
                    CategoryId = "badge",
                    IconAttachmentId = 73001,
                    CoverAttachmentId = 73007
                }
            }),
            productCategoryRepository: CreateRepositoryMock(new List<ProductCategory>
            {
                new()
                {
                    Id = "badge",
                    Name = "徽章",
                    IconAttachmentId = 73000
                }
            }),
            categoryRepository: CreateRepositoryMock(new List<Category>
            {
                new()
                {
                    Id = 1,
                    Name = "论坛分类",
                    IconAttachmentId = 81001,
                    CoverAttachmentId = 81002
                }
            }),
            levelConfigRepository: CreateRepositoryMock(new List<LevelConfig>
            {
                new()
                {
                    Level = 1,
                    LevelName = "新芽",
                    IconAttachmentId = 82001,
                    BadgeAttachmentId = 82002
                }
            }),
            stickerGroupRepository: CreateRepositoryMock(new List<StickerGroup>
            {
                new()
                {
                    Id = 9,
                    Name = "贴纸组",
                    CoverAttachmentId = 83001
                }
            }),
            reactionRepository: CreateRepositoryMock(new List<Reaction>
            {
                new()
                {
                    Id = 10,
                    PostId = 11,
                    StickerAttachmentId = 84001
                }
            }),
            wikiDocumentRepository: CreateRepositoryMock(new List<WikiDocument>
            {
                new()
                {
                    Id = 12,
                    Title = "文档",
                    Slug = "doc",
                    CoverAttachmentId = 85001
                }
            }),
            browseHistoryRepository: CreateRepositoryMock(new List<UserBrowseHistory>
            {
                new()
                {
                    Id = 13,
                    UserId = 9527,
                    BusinessType = "Shop",
                    BusinessId = "100001",
                    Title = "浏览记录",
                    CoverAttachmentId = 86001
                }
            }),
            userBenefitRepository: CreateRepositoryMock(new List<UserBenefit>
            {
                new()
                {
                    Id = 14,
                    UserId = 9527,
                    BenefitType = BenefitType.Badge,
                    BenefitName = "权益",
                    BenefitIconAttachmentId = 87001
                }
            }),
            userInventoryRepository: CreateRepositoryMock(new List<UserInventory>
            {
                new()
                {
                    Id = 15,
                    UserId = 9527,
                    ItemType = ProductType.Consumable,
                    ItemId = 100061,
                    ItemName = "库存项",
                    ItemIconAttachmentId = 88001
                }
            }),
            orderRepository: CreateRepositoryMock(new List<Order>
            {
                new()
                {
                    Id = 16,
                    UserId = 9527,
                    OrderNo = "ORD-1",
                    ProductName = "订单商品",
                    ProductIconAttachmentId = 89001
                }
            }));

        var referencedIds = await inspector.GetReferencedAttachmentIdsAsync(new long[]
        {
            73000, 73001, 73007, 81001, 81002, 82001, 82002, 83001, 84001, 85001, 86001, 87001, 88001, 89001
        });

        Assert.Equal(
            new HashSet<long> { 73000, 73001, 73007, 81001, 81002, 82001, 82002, 83001, 84001, 85001, 86001, 87001, 88001, 89001 },
            referencedIds);
    }

    private static AttachmentReferenceInspector CreateInspector(
        Mock<IBaseRepository<Sticker>>? stickerRepository = null,
        Mock<IBaseRepository<Post>>? postRepository = null,
        Mock<IBaseRepository<Comment>>? commentRepository = null,
        Mock<IBaseRepository<PostAnswer>>? postAnswerRepository = null,
        Mock<IBaseRepository<ChannelMessage>>? channelMessageRepository = null,
        Mock<IBaseRepository<Product>>? productRepository = null,
        Mock<IBaseRepository<ProductCategory>>? productCategoryRepository = null,
        Mock<IBaseRepository<Category>>? categoryRepository = null,
        Mock<IBaseRepository<LevelConfig>>? levelConfigRepository = null,
        Mock<IBaseRepository<StickerGroup>>? stickerGroupRepository = null,
        Mock<IBaseRepository<Reaction>>? reactionRepository = null,
        Mock<IBaseRepository<WikiDocument>>? wikiDocumentRepository = null,
        Mock<IBaseRepository<UserBrowseHistory>>? browseHistoryRepository = null,
        Mock<IBaseRepository<UserBenefit>>? userBenefitRepository = null,
        Mock<IBaseRepository<UserInventory>>? userInventoryRepository = null,
        Mock<IBaseRepository<Order>>? orderRepository = null)
    {
        return new AttachmentReferenceInspector(
            (stickerRepository ?? CreateRepositoryMock(new List<Sticker>())).Object,
            (postRepository ?? CreateRepositoryMock(new List<Post>())).Object,
            (commentRepository ?? CreateRepositoryMock(new List<Comment>())).Object,
            (postAnswerRepository ?? CreateRepositoryMock(new List<PostAnswer>())).Object,
            (channelMessageRepository ?? CreateRepositoryMock(new List<ChannelMessage>())).Object,
            (productRepository ?? CreateRepositoryMock(new List<Product>())).Object,
            (productCategoryRepository ?? CreateRepositoryMock(new List<ProductCategory>())).Object,
            (categoryRepository ?? CreateRepositoryMock(new List<Category>())).Object,
            (levelConfigRepository ?? CreateRepositoryMock(new List<LevelConfig>())).Object,
            (stickerGroupRepository ?? CreateRepositoryMock(new List<StickerGroup>())).Object,
            (reactionRepository ?? CreateRepositoryMock(new List<Reaction>())).Object,
            (wikiDocumentRepository ?? CreateRepositoryMock(new List<WikiDocument>())).Object,
            (browseHistoryRepository ?? CreateRepositoryMock(new List<UserBrowseHistory>())).Object,
            (userBenefitRepository ?? CreateRepositoryMock(new List<UserBenefit>())).Object,
            (userInventoryRepository ?? CreateRepositoryMock(new List<UserInventory>())).Object,
            (orderRepository ?? CreateRepositoryMock(new List<Order>())).Object);
    }

    private static Mock<IBaseRepository<TEntity>> CreateRepositoryMock<TEntity>(List<TEntity> result)
        where TEntity : class, new()
    {
        var repository = new Mock<IBaseRepository<TEntity>>(MockBehavior.Strict);
        repository
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<TEntity, bool>>>()))
            .ReturnsAsync(result);
        return repository;
    }
}
