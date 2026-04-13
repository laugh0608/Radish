using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Model.Tenants;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>帖子轻回应实体</summary>
[SugarTable("PostQuickReply")]
[SugarIndex("idx_postquickreply_post_recent", nameof(PostId), OrderByType.Asc, nameof(IsDeleted), OrderByType.Asc, nameof(Status), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_postquickreply_author_post", nameof(AuthorId), OrderByType.Asc, nameof(PostId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
public class PostQuickReply : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    public PostQuickReply()
    {
        Content = string.Empty;
        NormalizedContent = string.Empty;
        AuthorName = string.Empty;
        Status = (int)PostQuickReplyStatusEnum.Visible;
        IsDeleted = false;
        TenantId = 0;
        CreateTime = DateTime.UtcNow;
        CreateBy = "System";
        CreateId = 0;
    }

    /// <summary>所属帖子 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    /// <summary>作者 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long AuthorId { get; set; }

    /// <summary>作者名称</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string AuthorName { get; set; }

    /// <summary>轻回应内容</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string Content { get; set; }

    /// <summary>归一化内容，用于重复发送拦截</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string NormalizedContent { get; set; }

    /// <summary>状态（Visible/Hidden）</summary>
    [SugarColumn(IsNullable = false)]
    public int Status { get; set; }

    /// <summary>是否软删除</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; }

    /// <summary>删除时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除人</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    /// <summary>租户 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; }

    /// <summary>创建者</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; }

    /// <summary>创建者 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
