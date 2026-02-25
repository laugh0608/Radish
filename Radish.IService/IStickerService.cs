using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>表情包服务接口</summary>
public interface IStickerService : IBaseService<StickerGroup, StickerGroupVo>
{
    /// <summary>获取启用的表情包分组（含启用表情）</summary>
    Task<List<StickerGroupVo>> GetGroupsAsync(long tenantId);

    /// <summary>获取表情包分组（管理端，包含禁用分组）</summary>
    Task<List<StickerGroupVo>> GetAdminGroupsAsync(long tenantId);

    /// <summary>按分组编码获取分组详情</summary>
    Task<StickerGroupVo?> GetGroupDetailAsync(long tenantId, string code);

    /// <summary>记录表情使用次数</summary>
    Task<bool> RecordUseAsync(long tenantId, string emojiType, string emojiValue, long operatorId = 0, string? operatorName = null);

    /// <summary>创建分组</summary>
    Task<long> CreateGroupAsync(long tenantId, CreateStickerGroupDto createDto, long operatorId, string operatorName);

    /// <summary>更新分组</summary>
    Task<bool> UpdateGroupAsync(long id, UpdateStickerGroupDto updateDto, long operatorId, string operatorName);

    /// <summary>软删除分组（同时软删除组内表情）</summary>
    Task<bool> DeleteGroupAsync(long id, long operatorId, string operatorName);

    /// <summary>校验分组编码是否可用</summary>
    Task<bool> CheckGroupCodeAvailableAsync(long tenantId, string code);

    /// <summary>新增单个表情</summary>
    Task<long> AddStickerAsync(CreateStickerDto createDto, long operatorId, string operatorName);

    /// <summary>更新单个表情</summary>
    Task<bool> UpdateStickerAsync(long id, UpdateStickerDto updateDto, long operatorId, string operatorName);

    /// <summary>软删除单个表情</summary>
    Task<bool> DeleteStickerAsync(long id, long operatorId, string operatorName);

    /// <summary>校验组内表情编码是否可用</summary>
    Task<bool> CheckStickerCodeAvailableAsync(long groupId, string code);

    /// <summary>获取分组内表情（管理端，可选包含禁用）</summary>
    Task<List<StickerVo>> GetGroupStickersAsync(long groupId, bool includeDisabled = true);

    /// <summary>预览文件名编码清洗结果</summary>
    StickerNormalizeCodeVo NormalizeCode(string filename);

    /// <summary>批量新增表情</summary>
    Task<StickerBatchAddResultVo> BatchAddStickersAsync(BatchAddStickersDto request, long operatorId, string operatorName);

    /// <summary>批量更新表情排序</summary>
    Task<int> BatchUpdateSortAsync(List<StickerSortItemDto> items, long operatorId, string operatorName);
}
