using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户电子宠物主档</summary>
[SugarTable("PetProfile")]
[SugarIndex("idx_petprofile_public_id", nameof(PublicId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_petprofile_user", nameof(UserId), OrderByType.Asc, IsUnique = true)]
public class PetProfile : RootEntityTKey<long>, IHasUserId, ITenantEntity, IDeleteFilter
{
    public const string PublicIdPrefix = "pet_";

    public PetProfile()
    {
        PublicId = GeneratePublicId();
    }

    public static string GeneratePublicId()
    {
        return $"{PublicIdPrefix}{Guid.CreateVersion7():N}";
    }

    public static bool HasPublicIdFormat(string? value)
    {
        var normalized = value?.Trim();
        return normalized is { Length: 36 } &&
               normalized.StartsWith(PublicIdPrefix, StringComparison.OrdinalIgnoreCase) &&
               normalized[PublicIdPrefix.Length..].All(Uri.IsHexDigit);
    }

    /// <summary>公开标识</summary>
    [SugarColumn(Length = 36, IsNullable = false)]
    public string PublicId { get; set; }

    /// <summary>用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>宠物名称</summary>
    [SugarColumn(Length = 40, IsNullable = false)]
    public string Name { get; set; } = "小萝卜";

    /// <summary>物种模板键</summary>
    [SugarColumn(Length = 40, IsNullable = false)]
    public string SpeciesKey { get; set; } = "radish";

    /// <summary>形态模板键</summary>
    [SugarColumn(Length = 40, IsNullable = false)]
    public string ShapeKey { get; set; } = "sprout";

    /// <summary>成长阶段</summary>
    [SugarColumn(IsNullable = false)]
    public int GrowthStage { get; set; } = 1;

    /// <summary>心情</summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string Mood { get; set; } = PetMoodTypes.Calm;

    /// <summary>饱食度，范围 0-100</summary>
    [SugarColumn(IsNullable = false)]
    public int Satiety { get; set; } = 70;

    /// <summary>清洁度，范围 0-100</summary>
    [SugarColumn(IsNullable = false)]
    public int Cleanliness { get; set; } = 70;

    /// <summary>精力，范围 0-100</summary>
    [SugarColumn(IsNullable = false)]
    public int Energy { get; set; } = 70;

    /// <summary>成长值</summary>
    [SugarColumn(IsNullable = false)]
    public long GrowthValue { get; set; } = 0;

    /// <summary>当前背景装扮键</summary>
    [SugarColumn(Length = 60, IsNullable = true)]
    public string? EquippedBackgroundKey { get; set; }

    /// <summary>当前玩具装扮键</summary>
    [SugarColumn(Length = 60, IsNullable = true)]
    public string? EquippedToyKey { get; set; }

    /// <summary>是否允许公开展示宠物名片</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsPublic { get; set; } = false;

    /// <summary>最后照顾时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? LastCareTime { get; set; }

    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>是否软删除</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

    /// <summary>删除时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除人</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>创建者</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; } = 0;

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
