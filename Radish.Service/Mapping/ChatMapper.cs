using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class ChatMapper
{
    // Channel → ChannelVo
    [MapProperty(nameof(Channel.Id), nameof(ChannelVo.VoId))]
    [MapProperty(nameof(Channel.CategoryId), nameof(ChannelVo.VoCategoryId))]
    [MapProperty(nameof(Channel.Name), nameof(ChannelVo.VoName))]
    [MapProperty(nameof(Channel.Slug), nameof(ChannelVo.VoSlug))]
    [MapProperty(nameof(Channel.Description), nameof(ChannelVo.VoDescription))]
    [MapProperty(nameof(Channel.IconEmoji), nameof(ChannelVo.VoIconEmoji))]
    [MapProperty(nameof(Channel.Type), nameof(ChannelVo.VoType))]
    [MapProperty(nameof(Channel.Sort), nameof(ChannelVo.VoSort))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoUnreadCount))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoHasMention))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoLastMessage))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoConversationKind))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoPeerUserId))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoPeerPublicId))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoPeerDisplayName))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoPeerAvatarUrl))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoDirectRequestStatus))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoCanSend))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoCanReact))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoCanPinMessages))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoCanAccept))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoCanDecline))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoCanBlock))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoCanUnblock))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoIsBlockedByCurrentUser))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoIsArchived))]
    [MapperIgnoreTarget(nameof(ChannelVo.VoIsPeerAvailable))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial ChannelVo? ToChannelVo(Channel? source);

    public static partial List<ChannelVo> ToChannelVoList(IEnumerable<Channel>? source);

    // ChannelMessage → ChannelMessageVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static ChannelMessageVo? ToChannelMessageVo(ChannelMessage? source)
    {
        if (source is null) return null;
        var target = MapChannelMessageToChannelMessageVo(source);
        target.VoContent = source.IsDeleted ? null : source.Content;
        return target;
    }

    [MapProperty(nameof(ChannelMessage.Id), nameof(ChannelMessageVo.VoId))]
    [MapProperty(nameof(ChannelMessage.ClientRequestId), nameof(ChannelMessageVo.VoClientRequestId))]
    [MapProperty(nameof(ChannelMessage.ChannelId), nameof(ChannelMessageVo.VoChannelId))]
    [MapProperty(nameof(ChannelMessage.UserId), nameof(ChannelMessageVo.VoUserId))]
    [MapProperty(nameof(ChannelMessage.UserName), nameof(ChannelMessageVo.VoUserName))]
    [MapperIgnoreTarget(nameof(ChannelMessageVo.VoUserAvatarAttachmentId))]
    [MapperIgnoreTarget(nameof(ChannelMessageVo.VoUserAvatarUrl))]
    [MapProperty(nameof(ChannelMessage.Type), nameof(ChannelMessageVo.VoType))]
    [MapperIgnoreTarget(nameof(ChannelMessageVo.VoContent))]
    [MapProperty(nameof(ChannelMessage.ReplyToId), nameof(ChannelMessageVo.VoReplyToId))]
    [MapperIgnoreTarget(nameof(ChannelMessageVo.VoReplyTo))]
    [MapProperty(nameof(ChannelMessage.AttachmentId), nameof(ChannelMessageVo.VoAttachmentId))]
    [MapperIgnoreTarget(nameof(ChannelMessageVo.VoImageUrl))]
    [MapperIgnoreTarget(nameof(ChannelMessageVo.VoImageThumbnailUrl))]
    [MapProperty(nameof(ChannelMessage.IsDeleted), nameof(ChannelMessageVo.VoIsRecalled))]
    [MapProperty(nameof(ChannelMessage.CreateTime), nameof(ChannelMessageVo.VoCreateTime))]
    [UserMapping(Default = false)]
    private static partial ChannelMessageVo MapChannelMessageToChannelMessageVo(ChannelMessage source);

    public static partial List<ChannelMessageVo> ToChannelMessageVoList(IEnumerable<ChannelMessage>? source);

    // ChannelMessageVo → ChannelMessage
    [MapProperty(nameof(ChannelMessageVo.VoChannelId), nameof(ChannelMessage.ChannelId))]
    [MapProperty(nameof(ChannelMessageVo.VoUserId), nameof(ChannelMessage.UserId))]
    [MapProperty(nameof(ChannelMessageVo.VoUserName), nameof(ChannelMessage.UserName))]
    [MapProperty(nameof(ChannelMessageVo.VoClientRequestId), nameof(ChannelMessage.ClientRequestId))]
    [MapperIgnoreTarget(nameof(ChannelMessage.UserAvatarAttachmentIdSnapshot))]
    [MapProperty(nameof(ChannelMessageVo.VoType), nameof(ChannelMessage.Type))]
    [MapProperty(nameof(ChannelMessageVo.VoContent), nameof(ChannelMessage.Content))]
    [MapperIgnoreTarget(nameof(ChannelMessage.SearchText))]
    [MapProperty(nameof(ChannelMessageVo.VoReplyToId), nameof(ChannelMessage.ReplyToId))]
    [MapProperty(nameof(ChannelMessageVo.VoAttachmentId), nameof(ChannelMessage.AttachmentId))]
    [MapperIgnoreTarget(nameof(ChannelMessage.ReactionRevision))]
    [MapperIgnoreTarget(nameof(ChannelMessage.TenantId))]
    [MapProperty(nameof(ChannelMessageVo.VoCreateTime), nameof(ChannelMessage.CreateTime))]
    [MapperIgnoreTarget(nameof(ChannelMessage.IsDeleted))]
    [MapperIgnoreTarget(nameof(ChannelMessage.ModerationTargetActionId))]
    [MapperIgnoreTarget(nameof(ChannelMessage.DeletedAt))]
    [MapperIgnoreTarget(nameof(ChannelMessage.DeletedBy))]
    [MapProperty(nameof(ChannelMessageVo.VoId), nameof(ChannelMessage.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial ChannelMessage? ToChannelMessage(ChannelMessageVo? source);

    public static partial List<ChannelMessage> ToChannelMessageList(IEnumerable<ChannelMessageVo>? source);

    // ChannelVo → Channel
    [MapProperty(nameof(ChannelVo.VoCategoryId), nameof(Channel.CategoryId))]
    [MapProperty(nameof(ChannelVo.VoName), nameof(Channel.Name))]
    [MapProperty(nameof(ChannelVo.VoSlug), nameof(Channel.Slug))]
    [MapProperty(nameof(ChannelVo.VoDescription), nameof(Channel.Description))]
    [MapProperty(nameof(ChannelVo.VoIconEmoji), nameof(Channel.IconEmoji))]
    [MapProperty(nameof(ChannelVo.VoType), nameof(Channel.Type))]
    [MapperIgnoreTarget(nameof(Channel.DiscoverVisibility))]
    [MapperIgnoreTarget(nameof(Channel.DiscoverVisibilityVersion))]
    [MapperIgnoreTarget(nameof(Channel.IsEnabled))]
    [MapProperty(nameof(ChannelVo.VoSort), nameof(Channel.Sort))]
    [MapperIgnoreTarget(nameof(Channel.LastMessageId))]
    [MapperIgnoreTarget(nameof(Channel.LastMessageTime))]
    [MapperIgnoreTarget(nameof(Channel.PinRevision))]
    [MapperIgnoreTarget(nameof(Channel.TenantId))]
    [MapperIgnoreTarget(nameof(Channel.IsDeleted))]
    [MapperIgnoreTarget(nameof(Channel.DeletedAt))]
    [MapperIgnoreTarget(nameof(Channel.DeletedBy))]
    [MapperIgnoreTarget(nameof(Channel.CreateTime))]
    [MapperIgnoreTarget(nameof(Channel.CreateBy))]
    [MapperIgnoreTarget(nameof(Channel.CreateId))]
    [MapperIgnoreTarget(nameof(Channel.ModifyTime))]
    [MapperIgnoreTarget(nameof(Channel.ModifyBy))]
    [MapperIgnoreTarget(nameof(Channel.ModifyId))]
    [MapProperty(nameof(ChannelVo.VoId), nameof(Channel.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial Channel? ToChannel(ChannelVo? source);

    public static partial List<Channel> ToChannelList(IEnumerable<ChannelVo>? source);

}
