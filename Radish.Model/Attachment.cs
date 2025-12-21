using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>附件实体</summary>
/// <remarks>支持多租户，主键为 Id，类型为 long</remarks>
[SugarTable("Attachment")]
public class Attachment : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>初始化默认附件实例</summary>
    public Attachment()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        OriginalName = string.Empty;
        StoredName = string.Empty;
        Extension = string.Empty;
        FileSize = 0;
        MimeType = string.Empty;
        FileHash = string.Empty;
        StorageType = "Local";
        StoragePath = string.Empty;
        ThumbnailPath = string.Empty;
        Url = string.Empty;
        UploaderId = 0;
        UploaderName = string.Empty;
        BusinessType = string.Empty;
        BusinessId = null;
        IsPublic = true;
        DownloadCount = 0;
        AuditStatus = null;
        AuditResult = null;
        Remark = string.Empty;
        IsEnabled = true;
        IsDeleted = false;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    #region 基础信息

    /// <summary>原始文件名</summary>
    /// <remarks>不可为空，最大 255 字符</remarks>
    [SugarColumn(Length = 255, IsNullable = false)]
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>存储文件名（雪花ID）</summary>
    /// <remarks>不可为空，最大 100 字符，唯一</remarks>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string StoredName { get; set; } = string.Empty;

    /// <summary>文件扩展名</summary>
    /// <remarks>不可为空，最大 20 字符，包含点号（如 .jpg）</remarks>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string Extension { get; set; } = string.Empty;

    /// <summary>文件大小（字节）</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public long FileSize { get; set; } = 0;

    /// <summary>MIME 类型</summary>
    /// <remarks>不可为空，最大 100 字符（如 image/jpeg）</remarks>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>文件哈希值（SHA256）</summary>
    /// <remarks>可空，最大 64 字符，用于文件去重</remarks>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? FileHash { get; set; } = string.Empty;

    #endregion

    #region 存储信息

    /// <summary>存储类型</summary>
    /// <remarks>不可为空，最大 20 字符（Local/MinIO/OSS）</remarks>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string StorageType { get; set; } = "Local";

    /// <summary>存储路径（相对路径）</summary>
    /// <remarks>不可为空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>缩略图路径</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ThumbnailPath { get; set; } = string.Empty;

    /// <summary>访问 URL</summary>
    /// <remarks>不可为空，最大 1000 字符</remarks>
    [SugarColumn(Length = 1000, IsNullable = false)]
    public string Url { get; set; } = string.Empty;

    #endregion

    #region 上传者信息

    /// <summary>上传者 ID</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public long UploaderId { get; set; } = 0;

    /// <summary>上传者名称</summary>
    /// <remarks>不可为空，最大 50 字符，冗余字段便于查询</remarks>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string UploaderName { get; set; } = string.Empty;

    #endregion

    #region 业务关联

    /// <summary>业务类型</summary>
    /// <remarks>不可为空，最大 50 字符（Post/Comment/Avatar/Document）</remarks>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>业务 ID</summary>
    /// <remarks>可空（如 PostId、CommentId）</remarks>
    [SugarColumn(IsNullable = true)]
    public long? BusinessId { get; set; }

    #endregion

    #region 状态信息

    /// <summary>是否公开</summary>
    /// <remarks>不可为空，默认为 true（公开文件任何人可访问）</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsPublic { get; set; } = true;

    /// <summary>下载次数</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int DownloadCount { get; set; } = 0;

    /// <summary>内容审核状态</summary>
    /// <remarks>可空，最大 20 字符（Pending/Pass/Reject）</remarks>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string? AuditStatus { get; set; }

    /// <summary>内容审核结果</summary>
    /// <remarks>可空，长文本类型</remarks>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? AuditResult { get; set; }

    /// <summary>备注</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Remark { get; set; } = string.Empty;

    /// <summary>是否启用</summary>
    /// <remarks>不可为空，默认为 true</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>是否删除</summary>
    /// <remarks>不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

    #endregion

    #region 租户信息

    /// <summary>租户 Id</summary>
    /// <remarks>不可为空，默认为 0，支持多租户隔离</remarks>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    #endregion

    #region 审计信息

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间，更新时忽略该列</remarks>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者名称</summary>
    /// <remarks>不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 Id</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; } = 0;

    /// <summary>修改时间</summary>
    /// <remarks>可空</remarks>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者名称</summary>
    /// <remarks>可空，最大 50 字符</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 Id</summary>
    /// <remarks>可空</remarks>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }

    #endregion
}
