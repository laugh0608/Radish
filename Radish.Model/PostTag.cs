using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>帖子-标签关联实体</summary>
/// <remarks>多对多关系中间表，主键为 Id，类型为 long</remarks>
public class PostTag : RootEntityTKey<long>
{
    /// <summary>初始化默认实例</summary>
    public PostTag()
    {
        InitializeDefaults();
    }

    /// <summary>通过帖子 Id 和标签 Id 初始化关联</summary>
    /// <param name="postId">帖子 Id</param>
    /// <param name="tagId">标签 Id</param>
    public PostTag(long postId, long tagId)
        : this(new PostTagInitializationOptions(postId, tagId))
    {
    }

    /// <summary>通过初始化选项批量构造关联</summary>
    /// <param name="options">初始化选项</param>
    public PostTag(PostTagInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyRelationInformation(options);
        ApplyAuditInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        PostId = 0;
        TagId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    /// <summary>处理关联信息</summary>
    private void ApplyRelationInformation(PostTagInitializationOptions options)
    {
        if (options.PostId <= 0)
        {
            throw new ArgumentException("PostId 必须大于 0。", nameof(options.PostId));
        }

        if (options.TagId <= 0)
        {
            throw new ArgumentException("TagId 必须大于 0。", nameof(options.TagId));
        }

        PostId = options.PostId;
        TagId = options.TagId;
    }

    /// <summary>处理审计信息</summary>
    private void ApplyAuditInformation(PostTagInitializationOptions options)
    {
        if (options.CreateId.HasValue)
        {
            CreateId = options.CreateId.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.CreateBy))
        {
            CreateBy = options.CreateBy.Trim();
        }
    }

    #region 关联信息

    /// <summary>帖子 Id</summary>
    /// <remarks>不可为空，组合索引成员</remarks>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; } = 0;

    /// <summary>标签 Id</summary>
    /// <remarks>不可为空，组合索引成员</remarks>
    [SugarColumn(IsNullable = false)]
    public long TagId { get; set; } = 0;

    #endregion

    #region 审计信息

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间</remarks>
    [SugarColumn(IsNullable = false)]
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

    #endregion
}

/// <summary>帖子-标签关联初始化选项</summary>
public sealed class PostTagInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="postId">帖子 Id</param>
    /// <param name="tagId">标签 Id</param>
    public PostTagInitializationOptions(long postId, long tagId)
    {
        PostId = postId;
        TagId = tagId;
    }

    /// <summary>帖子 Id</summary>
    public long PostId { get; }

    /// <summary>标签 Id</summary>
    public long TagId { get; }

    /// <summary>创建者 Id</summary>
    public long? CreateId { get; set; }

    /// <summary>创建者名称</summary>
    public string? CreateBy { get; set; }
}
