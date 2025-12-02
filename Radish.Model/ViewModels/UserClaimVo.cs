using System;

namespace Radish.Model.ViewModels;

/// <summary>用户声明视图模型</summary>
public class UserClaimVo
{
    /// <summary>声明 Id</summary>
    public long Id { get; set; }

    /// <summary>用户 Id</summary>
    public long UserId { get; set; }

    /// <summary>声明类型</summary>
    public string ClaimType { get; set; } = string.Empty;

    /// <summary>声明值</summary>
    public string ClaimValue { get; set; } = string.Empty;

    /// <summary>是否已被删除</summary>
    public bool IsDeleted { get; set; }

    /// <summary>创建者名称</summary>
    public string CreateBy { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新者名称</summary>
    public string? ModifyBy { get; set; }

    /// <summary>更新时间</summary>
    public DateTime? ModifyTime { get; set; }
}
