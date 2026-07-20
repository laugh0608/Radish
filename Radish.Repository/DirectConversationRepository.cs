using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;

namespace Radish.Repository;

/// <summary>一对一私聊仓储</summary>
public sealed class DirectConversationRepository : BaseRepository<DirectConversation>, IDirectConversationRepository
{
    public DirectConversationRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<DirectConversationCreateResult> CreateOrGetAsync(
        DirectConversation conversation,
        Channel channel,
        ChannelMember lowParticipant,
        ChannelMember highParticipant)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(lowParticipant);
        ArgumentNullException.ThrowIfNull(highParticipant);

        ValidateCreateDraft(conversation, channel, lowParticipant, highParticipant);
        return await ExecuteDbOperationAsync(() => CreateOrGetCoreAsync(
            conversation,
            channel,
            lowParticipant,
            highParticipant));
    }

    private async Task<DirectConversationCreateResult> CreateOrGetCoreAsync(
        DirectConversation conversation,
        Channel channel,
        ChannelMember lowParticipant,
        ChannelMember highParticipant)
    {

        var existing = await QueryExistingAsync(conversation);
        if (existing != null)
        {
            return new DirectConversationCreateResult(existing, false);
        }

        DbProtectedClient.Ado.BeginTran();
        try
        {
            existing = await QueryExistingAsync(conversation);
            if (existing != null)
            {
                DbProtectedClient.Ado.CommitTran();
                return new DirectConversationCreateResult(existing, false);
            }

            await DbProtectedClient.Insertable(channel).ExecuteCommandAsync();
            await DbProtectedClient.Insertable(new[] { lowParticipant, highParticipant }).ExecuteCommandAsync();
            await DbProtectedClient.Insertable(conversation).ExecuteCommandAsync();
            DbProtectedClient.Ado.CommitTran();
            return new DirectConversationCreateResult(conversation, true);
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            var winner = await QueryExistingAsync(conversation);
            if (winner != null)
            {
                return new DirectConversationCreateResult(winner, false);
            }

            throw;
        }
    }

    private async Task<DirectConversation?> QueryExistingAsync(DirectConversation conversation)
    {
        return await DbProtectedClient.Queryable<DirectConversation>()
            .Where(candidate =>
                candidate.TenantId == conversation.TenantId &&
                candidate.ParticipantLowUserId == conversation.ParticipantLowUserId &&
                candidate.ParticipantHighUserId == conversation.ParticipantHighUserId &&
                !candidate.IsDeleted)
            .FirstAsync();
    }

    private static void ValidateCreateDraft(
        DirectConversation conversation,
        Channel channel,
        ChannelMember lowParticipant,
        ChannelMember highParticipant)
    {
        if (conversation.Id <= 0 || channel.Id <= 0 || conversation.ChannelId != channel.Id)
        {
            throw new ArgumentException("私聊会话与频道必须使用有效且一致的预分配 ID。", nameof(conversation));
        }

        if (conversation.ParticipantLowUserId <= 0 ||
            conversation.ParticipantHighUserId <= 0 ||
            conversation.ParticipantLowUserId >= conversation.ParticipantHighUserId)
        {
            throw new ArgumentException("私聊参与者必须按有效用户 ID 升序规范化。", nameof(conversation));
        }

        if (channel.Type != ChannelType.Private ||
            channel.TenantId != conversation.TenantId ||
            lowParticipant.TenantId != conversation.TenantId ||
            highParticipant.TenantId != conversation.TenantId ||
            lowParticipant.ChannelId != channel.Id ||
            highParticipant.ChannelId != channel.Id ||
            lowParticipant.UserId != conversation.ParticipantLowUserId ||
            highParticipant.UserId != conversation.ParticipantHighUserId)
        {
            throw new ArgumentException("私聊频道、成员与关系元数据不一致。", nameof(conversation));
        }
    }
}
