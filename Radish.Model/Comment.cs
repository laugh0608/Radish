using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>评论实体</summary>
/// <remarks>支持树形结构和多租户，主键为 Id，类型为 long</remarks>
public class Comment : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>初始化默认评论实例</summary>
    public Comment()
    {
        InitializeDefaults();
    }

    /// <summary>通过内容初始化评论</summary>
    /// <param name="content">评论内容</param>
    public Comment(string content)
        : this(new CommentInitializationOptions(content))
    {
    }

    /// <summary>通过初始化选项批量构造评论</summary>
    /// <param name="options">初始化选项</param>
    public Comment(CommentInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyRelationInformation(options);
        ApplyStatusInformation(options);
        ApplyStatisticsInformation(options);
        ApplyAuditInformation(options);
        ApplyTenantInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        Content = string.Empty;
        PostId = 0;
        ParentId = null;
        RootId = null;
        ReplyToUserId = null;
        ReplyToUserName = string.Empty;
        Level = 0;
        Path = string.Empty;
        AuthorId = 0;
        AuthorName = string.Empty;
        LikeCount = 0;
        ReplyCount = 0;
        IsTop = false;
        IsEnabled = true;
        IsDeleted = false;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(CommentInitializationOptions options)
    {
        Content = NormalizeRequired(options.Content, nameof(options.Content));
    }

    /// <summary>处理关联信息</summary>
    private void ApplyRelationInformation(CommentInitializationOptions options)
    {
        if (options.PostId.HasValue)
        {
            PostId = options.PostId.Value;
        }

        if (options.ParentId.HasValue)
        {
            ParentId = options.ParentId.Value;
        }

        if (options.RootId.HasValue)
        {
            RootId = options.RootId.Value;
        }

        if (options.ReplyToUserId.HasValue)
        {
            ReplyToUserId = options.ReplyToUserId.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.ReplyToUserName))
        {
            ReplyToUserName = options.ReplyToUserName.Trim();
        }

        if (options.Level.HasValue)
        {
            Level = Math.Max(0, options.Level.Value);
        }

        if (!string.IsNullOrWhiteSpace(options.Path))
        {
            Path = options.Path.Trim();
        }
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(CommentInitializationOptions options)
    {
        if (options.IsTop.HasValue)
        {
            IsTop = options.IsTop.Value;
        }

        if (options.IsEnabled.HasValue)
        {
            IsEnabled = options.IsEnabled.Value;
        }

        if (options.IsDeleted.HasValue)
        {
            IsDeleted = options.IsDeleted.Value;
        }
    }

    /// <summary>处理统计信息</summary>
    private void ApplyStatisticsInformation(CommentInitializationOptions options)
    {
        // 统计信息一般不在初始化时设置，但保留接口
    }

    /// <summary>处理审计信息</summary>
    private void ApplyAuditInformation(CommentInitializationOptions options)
    {
        if (options.AuthorId.HasValue)
        {
            AuthorId = options.AuthorId.Value;
            CreateId = options.AuthorId.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.AuthorName))
        {
            AuthorName = options.AuthorName.Trim();
            CreateBy = options.AuthorName.Trim();
        }
    }

    /// <summary>处理租户信息</summary>
    private void ApplyTenantInformation(CommentInitializationOptions options)
    {
        if (options.TenantId.HasValue)
        {
            TenantId = options.TenantId.Value;
        }
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} 不能为空。", paramName);
        }

        return value.Trim();
    }

    #region 基础信息

    /// <summary>评论内容</summary>
    /// <remarks>不可为空，最大 2000 字符</remarks>
    [SugarColumn(Length = 2000, IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    #endregion

    #region 关联信息

    /// <summary>所属帖子 Id</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; } = 0;

    /// <summary>父评论 Id</summary>
    /// <remarks>可空，顶级评论为 null</remarks>
    [SugarColumn(IsNullable = true)]
    public long? ParentId { get; set; }

    /// <summary>根评论 Id</summary>
    /// <remarks>可空，顶级评论为 null，用于快速查询整个评论树</remarks>
    [SugarColumn(IsNullable = true)]
    public long? RootId { get; set; }

    /// <summary>回复目标用户 Id</summary>
    /// <remarks>可空，用于 @某人</remarks>
    [SugarColumn(IsNullable = true)]
    public long? ReplyToUserId { get; set; }

    /// <summary>回复目标用户名称</summary>
    /// <remarks>可空，最大 100 字符，冗余字段便于查询</remarks>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string ReplyToUserName { get; set; } = string.Empty;

    /// <summary>层级深度</summary>
    /// <remarks>不可为空，顶级为 0，子级递增，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int Level { get; set; } = 0;

    /// <summary>路径</summary>
    /// <remarks>可空，最大 500 字符，如 "1-23-456" 用于查询所有子评论</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string Path { get; set; } = string.Empty;

    #endregion

    #region 作者信息

    /// <summary>作者 Id</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public long AuthorId { get; set; } = 0;

    /// <summary>作者名称</summary>
    /// <remarks>不可为空，最大 100 字符，冗余字段便于查询</remarks>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string AuthorName { get; set; } = string.Empty;

    #endregion

    #region 统计信息

    /// <summary>点赞次数</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int LikeCount { get; set; } = 0;

    /// <summary>回复次数</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int ReplyCount { get; set; } = 0;

    #endregion

    #region 状态信息

    /// <summary>是否置顶</summary>
    /// <remarks>不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsTop { get; set; } = false;

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

/// <summary>评论初始化选项</summary>
public sealed class CommentInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="content">评论内容</param>
    public CommentInitializationOptions(string content)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>评论内容</summary>
    public string Content { get; }

    /// <summary>所属帖子 Id</summary>
    public long? PostId { get; set; }

    /// <summary>父评论 Id</summary>
    public long? ParentId { get; set; }

    /// <summary>根评论 Id</summary>
    public long? RootId { get; set; }

    /// <summary>回复目标用户 Id</summary>
    public long? ReplyToUserId { get; set; }

    /// <summary>回复目标用户名称</summary>
    public string? ReplyToUserName { get; set; }

    /// <summary>层级深度</summary>
    public int? Level { get; set; }

    /// <summary>路径</summary>
    public string? Path { get; set; }

    /// <summary>作者 Id</summary>
    public long? AuthorId { get; set; }

    /// <summary>作者名称</summary>
    public string? AuthorName { get; set; }

    /// <summary>是否置顶</summary>
    public bool? IsTop { get; set; }

    /// <summary>是否启用</summary>
    public bool? IsEnabled { get; set; }

    /// <summary>是否删除</summary>
    public bool? IsDeleted { get; set; }

    /// <summary>租户 Id</summary>
    public long? TenantId { get; set; }
}
