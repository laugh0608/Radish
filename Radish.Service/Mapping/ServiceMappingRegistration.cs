using Microsoft.Extensions.DependencyInjection;
using Radish.IService;
using Radish.Model;
using Radish.Model.LogModels;
using Radish.Model.Models;
using Radish.Model.Tenants;
using Radish.Model.ViewModels;

namespace Radish.Service.Mapping;

/// <summary>为泛型 BaseService 注册可编译检查的实体映射委托。</summary>
public static class ServiceMappingRegistration
{
    public static void AddServiceMappings(this IServiceCollection services)
    {
        services.AddScoped<AttachmentMapper>();
        services.AddScoped<Func<Attachment, AttachmentVo>>(sp => sp.GetRequiredService<AttachmentMapper>().ToAttachmentVo);
        services.AddSingleton<Func<Category, CategoryVo>>(ForumMapper.ToCategoryVo);
        services.AddSingleton<Func<Comment, CommentVo>>(ForumMapper.ToCommentVo);
        services.AddSingleton<Func<CommentHighlight, CommentHighlightVo>>(ForumMapper.ToCommentHighlightVo);
        services.AddSingleton<Func<Post, PostVo>>(ForumMapper.ToPostVo);
        services.AddSingleton<Func<PostQuickReply, PostQuickReplyVo>>(ForumMapper.ToPostQuickReplyVo);
        services.AddSingleton<Func<StickerGroup, StickerGroupVo>>(ForumMapper.ToStickerGroupVo);
        services.AddSingleton<Func<Tag, TagVo>>(ForumMapper.ToTagVo);
        services.AddSingleton<Func<Channel, ChannelVo>>(ChatMapper.ToChannelVo);
        services.AddSingleton<Func<UserBalance, UserBalanceVo>>(CoinMapper.ToUserBalanceVo);
        services.AddSingleton<Func<ContentReport, ContentReportVo>>(ContentModerationMapper.ToContentReportVo);
        services.AddSingleton<Func<UserExperience, UserExperienceVo>>(ExperienceMapper.ToUserExperienceVo);
        services.AddSingleton<Func<UserTimePreference, UserTimePreferenceVo>>(UserMapper.ToUserTimePreferenceVo);
        services.AddSingleton<Func<User, UserVo>>(UserMapper.ToUserVo);
        services.AddSingleton<Func<Order, OrderVo>>(ShopMapper.ToOrderVo);
        services.AddSingleton<Func<Product, ProductVo>>(ShopMapper.ToProductVo);
        services.AddSingleton<Func<UserBenefit, UserBenefitVo>>(ShopMapper.ToUserBenefitVo);
        services.AddSingleton<Func<UserInventory, UserInventoryVo>>(ShopMapper.ToUserInventoryVo);
        services.AddSingleton<Func<PetProfile, PetProfileVo>>(PetMapper.ToPetProfileVo);
        services.AddSingleton<Func<Tenant, TenantVo>>(TenantMapper.ToTenantVo);
        services.AddSingleton<Func<BusinessTable, BusinessTableVo>>(TenantMapper.ToBusinessTableVo);
        services.AddSingleton<Func<MultiBusinessTable, MultiBusinessTableVo>>(TenantMapper.ToMultiBusinessTableVo);
        services.AddSingleton<Func<SubLibBusinessTable, SubLibBusinessTableVo>>(TenantMapper.ToSubLibBusinessTableVo);
        services.AddSingleton<Func<UserFollow, UserFollowVo>>(UserFollowMapper.ToUserFollowVo);
        services.AddSingleton<Func<WikiDocument, WikiDocumentVo>>(WikiMapper.ToWikiDocumentVo);
        services.AddSingleton<Func<AuditLog, AuditLogVo>>(AuditLogMapper.ToAuditLogVo);
    }
}
