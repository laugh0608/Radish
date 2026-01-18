using System;

namespace Radish.Model.ViewModels;

/// <summary>用户声明视图模型</summary>
public class UserClaimVo
{
    /// <summary>声明 Id</summary>
    public long VoId { get; set; }

    /// <summary>用户 Id</summary>
    public long VoUserId { get; set; }

    /// <summary>声明类型</summary>
    public string VoClaimType { get; set; } = string.Empty;

    /// <summary>声明值</summary>
    public string VoClaimValue { get; set; } = string.Empty;

    /// <summary>是否已被删除</summary>
    public bool VoIsDeleted { get; set; }

    /// <summary>创建者名称</summary>
    public string VoCreateBy { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }

    /// <summary>更新者名称</summary>
    public string? VoModifyBy { get; set; }

    /// <summary>更新时间</summary>
    public DateTime? VoModifyTime { get; set; }
}
