using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>帖子实体</summary>
/// <remarks>支持多租户，主键为 Id，类型为 long</remarks>
public class Post : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>初始化默认帖子实例</summary>
    public Post()
    {
        InitializeDefaults();
    }

    /// <summary>通过标题和内容初始化帖子</summary>
    /// <param name="title">帖子标题</param>
    /// <param name="content">帖子内容</param>
    public Post(string title, string content)
        : this(new PostInitializationOptions(title, content))
    {
    }

    /// <summary>通过初始化选项批量构造帖子</summary>
    /// <param name="options">初始化选项</param>
    public Post(PostInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyCategoryInformation(options);
        ApplyStatusInformation(options);
        ApplyStatisticsInformation(options);
        ApplyAuditInformation(options);
        ApplyTenantInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        Title = string.Empty;
        Slug = string.Empty;
        Summary = string.Empty;
        Content = string.Empty;
        ContentType = "markdown"; // 默认使用 Markdown
        CoverImage = string.Empty;
        AuthorId = 0;
        AuthorName = string.Empty;
        CategoryId = 0;
        IsTop = false;
        IsEssence = false;
        IsLocked = false;
        ViewCount = 0;
        LikeCount = 0;
        CommentCount = 0;
        CollectCount = 0;
        ShareCount = 0;
        IsPublished = false;
        PublishTime = null;
        IsEnabled = true;
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(PostInitializationOptions options)
    {
        Title = NormalizeRequired(options.Title, nameof(options.Title));
        Content = NormalizeRequired(options.Content, nameof(options.Content));

        if (!string.IsNullOrWhiteSpace(options.Slug))
        {
            Slug = options.Slug.Trim().ToLowerInvariant();
        }
        else
        {
            // 默认使用标题生成 slug（简化版）
            Slug = Title.ToLowerInvariant().Replace(" ", "-");
        }

        if (!string.IsNullOrWhiteSpace(options.Summary))
        {
            Summary = options.Summary.Trim();
        }
        else
        {
            // 默认从内容中提取前 200 字符作为摘要
            Summary = Content.Length > 200 ? Content.Substring(0, 200) + "..." : Content;
        }

        if (!string.IsNullOrWhiteSpace(options.ContentType))
        {
            ContentType = options.ContentType.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(options.CoverImage))
        {
            CoverImage = options.CoverImage.Trim();
        }
    }

    /// <summary>处理分类信息</summary>
    private void ApplyCategoryInformation(PostInitializationOptions options)
    {
        if (options.CategoryId.HasValue)
        {
            CategoryId = options.CategoryId.Value;
        }
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(PostInitializationOptions options)
    {
        if (options.IsTop.HasValue)
        {
            IsTop = options.IsTop.Value;
        }

        if (options.IsEssence.HasValue)
        {
            IsEssence = options.IsEssence.Value;
        }

        if (options.IsLocked.HasValue)
        {
            IsLocked = options.IsLocked.Value;
        }

        if (options.IsPublished.HasValue)
        {
            IsPublished = options.IsPublished.Value;
            if (IsPublished && !PublishTime.HasValue)
            {
                PublishTime = DateTime.Now;
            }
        }

        if (options.PublishTime.HasValue)
        {
            PublishTime = options.PublishTime.Value;
        }

        if (options.IsEnabled.HasValue)
        {
            IsEnabled = options.IsEnabled.Value;
        }

        if (options.IsDeleted.HasValue)
        {
            IsDeleted = options.IsDeleted.Value;
            if (IsDeleted && options.DeletedAt.HasValue)
            {
                DeletedAt = options.DeletedAt.Value;
            }
            if (IsDeleted && !string.IsNullOrWhiteSpace(options.DeletedBy))
            {
                DeletedBy = options.DeletedBy.Trim();
            }
        }
    }

    /// <summary>处理统计信息</summary>
    private void ApplyStatisticsInformation(PostInitializationOptions options)
    {
        // 统计信息一般不在初始化时设置，但保留接口
    }

    /// <summary>处理审计信息</summary>
    private void ApplyAuditInformation(PostInitializationOptions options)
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
    private void ApplyTenantInformation(PostInitializationOptions options)
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

    /// <summary>帖子标题</summary>
    /// <remarks>不可为空，最大 200 字符</remarks>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    /// <summary>URL 友好的标识符</summary>
    /// <remarks>不可为空，最大 200 字符，唯一索引</remarks>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>帖子摘要</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>帖子内容</summary>
    /// <remarks>不可为空，长文本类型</remarks>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    /// <summary>内容类型</summary>
    /// <remarks>不可为空，最大 20 字符，如 markdown、html、text，默认为 markdown</remarks>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string ContentType { get; set; } = "markdown";

    /// <summary>封面图片</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string CoverImage { get; set; } = string.Empty;

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

    #region 分类信息

    /// <summary>分类 Id</summary>
    /// <remarks>不可为空，默认为 0（未分类）</remarks>
    [SugarColumn(IsNullable = false)]
    public long CategoryId { get; set; } = 0;

    #endregion

    #region 状态信息

    /// <summary>是否置顶</summary>
    /// <remarks>不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsTop { get; set; } = false;

    /// <summary>是否精华</summary>
    /// <remarks>不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsEssence { get; set; } = false;

    /// <summary>是否锁定（禁止评论）</summary>
    /// <remarks>不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsLocked { get; set; } = false;

    /// <summary>是否已发布</summary>
    /// <remarks>不可为空，默认为 false（草稿状态）</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsPublished { get; set; } = false;

    /// <summary>发布时间</summary>
    /// <remarks>可空，首次发布时自动设置</remarks>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? PublishTime { get; set; }

    /// <summary>是否启用</summary>
    /// <remarks>不可为空，默认为 true</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>是否删除</summary>
    /// <remarks>不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

    /// <summary>删除时间</summary>
    /// <remarks>可空，软删除时自动设置，恢复时清空</remarks>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除操作者</summary>
    /// <remarks>可空，最大 50 字符，执行软删除操作的用户名或系统标识</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    #endregion

    #region 统计信息

    /// <summary>浏览次数</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int ViewCount { get; set; } = 0;

    /// <summary>点赞次数</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int LikeCount { get; set; } = 0;

    /// <summary>评论次数</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int CommentCount { get; set; } = 0;

    /// <summary>收藏次数</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int CollectCount { get; set; } = 0;

    /// <summary>分享次数</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int ShareCount { get; set; } = 0;

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

/// <summary>帖子初始化选项</summary>
public sealed class PostInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="title">帖子标题</param>
    /// <param name="content">帖子内容</param>
    public PostInitializationOptions(string title, string content)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>帖子标题</summary>
    public string Title { get; }

    /// <summary>帖子内容</summary>
    public string Content { get; }

    /// <summary>URL 友好标识符</summary>
    public string? Slug { get; set; }

    /// <summary>帖子摘要</summary>
    public string? Summary { get; set; }

    /// <summary>内容类型</summary>
    public string? ContentType { get; set; }

    /// <summary>封面图片</summary>
    public string? CoverImage { get; set; }

    /// <summary>作者 Id</summary>
    public long? AuthorId { get; set; }

    /// <summary>作者名称</summary>
    public string? AuthorName { get; set; }

    /// <summary>分类 Id</summary>
    public long? CategoryId { get; set; }

    /// <summary>是否置顶</summary>
    public bool? IsTop { get; set; }

    /// <summary>是否精华</summary>
    public bool? IsEssence { get; set; }

    /// <summary>是否锁定</summary>
    public bool? IsLocked { get; set; }

    /// <summary>是否已发布</summary>
    public bool? IsPublished { get; set; }

    /// <summary>发布时间</summary>
    public DateTime? PublishTime { get; set; }

    /// <summary>是否启用</summary>
    public bool? IsEnabled { get; set; }

    /// <summary>是否删除</summary>
    public bool? IsDeleted { get; set; }

    /// <summary>删除时间</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除操作者</summary>
    public string? DeletedBy { get; set; }

    /// <summary>租户 Id</summary>
    public long? TenantId { get; set; }
}
