using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>聊天室实体和视图模型映射配置</summary>
public class ChatProfile : Profile
{
    public ChatProfile()
    {
        // Channel -> ChannelVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Channel, ChannelVo>()
            .ForMember(dest => dest.VoUnreadCount, opt => opt.Ignore())
            .ForMember(dest => dest.VoHasMention, opt => opt.Ignore())
            .ForMember(dest => dest.VoLastMessage, opt => opt.Ignore())
            .ForMember(dest => dest.VoConversationKind, opt => opt.Ignore())
            .ForMember(dest => dest.VoPeerUserId, opt => opt.Ignore())
            .ForMember(dest => dest.VoPeerPublicId, opt => opt.Ignore())
            .ForMember(dest => dest.VoPeerDisplayName, opt => opt.Ignore())
            .ForMember(dest => dest.VoPeerAvatarUrl, opt => opt.Ignore())
            .ForMember(dest => dest.VoDirectRequestStatus, opt => opt.Ignore())
            .ForMember(dest => dest.VoCanSend, opt => opt.Ignore())
            .ForMember(dest => dest.VoCanAccept, opt => opt.Ignore())
            .ForMember(dest => dest.VoCanDecline, opt => opt.Ignore())
            .ForMember(dest => dest.VoCanBlock, opt => opt.Ignore())
            .ForMember(dest => dest.VoCanUnblock, opt => opt.Ignore())
            .ForMember(dest => dest.VoIsBlockedByCurrentUser, opt => opt.Ignore())
            .ForMember(dest => dest.VoIsArchived, opt => opt.Ignore())
            .ForMember(dest => dest.VoIsPeerAvailable, opt => opt.Ignore());
        RecognizePrefixes("Vo");
        CreateMap<ChannelVo, Channel>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.LastMessageId, opt => opt.Ignore())
            .ForMember(dest => dest.LastMessageTime, opt => opt.Ignore());

        // ChannelMessage -> ChannelMessageVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<ChannelMessage, ChannelMessageVo>()
            .ForMember(dest => dest.VoReplyTo, opt => opt.Ignore())
            .ForMember(dest => dest.VoIsRecalled, opt => opt.MapFrom(src => src.IsDeleted))
            .ForMember(dest => dest.VoContent, opt => opt.MapFrom(src => src.IsDeleted ? null : src.Content))
            .ForMember(dest => dest.VoUserAvatarUrl, opt => opt.Ignore())
            .ForMember(dest => dest.VoImageUrl, opt => opt.Ignore())
            .ForMember(dest => dest.VoImageThumbnailUrl, opt => opt.Ignore());
        RecognizePrefixes("Vo");
        CreateMap<ChannelMessageVo, ChannelMessage>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore());
    }
}
