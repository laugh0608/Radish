using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>等级配置整批重算的追加式审计记录。</summary>
[SugarTable("ExperienceLevelRecalculationAudit")]
[SugarIndex("idx_experience_level_recalc_time", nameof(CreateTime), OrderByType.Desc)]
public sealed class ExperienceLevelRecalculationAudit : RootEntityTKey<long>
{
    [SugarColumn(Length = 50, IsNullable = false)]
    public string FormulaType { get; set; } = string.Empty;

    [SugarColumn(Length = 500, IsNullable = false)]
    public string FormulaSummary { get; set; } = string.Empty;

    [SugarColumn(Length = 64, IsNullable = false)]
    public string PreviewFingerprint { get; set; } = string.Empty;

    public int ChangedLevelCount { get; set; }

    [SugarColumn(IsNullable = false, ColumnDataType = "longtext,text,clob")]
    public string BeforeSnapshot { get; set; } = "[]";

    [SugarColumn(IsNullable = false, ColumnDataType = "longtext,text,clob")]
    public string AfterSnapshot { get; set; } = "[]";

    [SugarColumn(Length = 500, IsNullable = false)]
    public string Reason { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    public long CreateId { get; set; }
}
