namespace Radish.Model.ViewModels;

/// <summary>管理员经验调账的权威结果。</summary>
public sealed class AdminExperienceAdjustmentResultVo
{
    public UserExperienceVo VoExperience { get; set; } = new();
    public ExpTransactionVo VoTransaction { get; set; } = new();
    public bool VoReplayed { get; set; }
}

/// <summary>冻结、解冻或人工复核的权威结果。</summary>
public sealed class AdminExperienceGovernanceResultVo
{
    public UserExperienceVo VoExperience { get; set; } = new();
    public UserExperienceGovernanceActionVo VoAction { get; set; } = new();
    public bool VoReplayed { get; set; }
}

/// <summary>单个等级配置的重算差异。</summary>
public sealed class ExperienceLevelRecalculationChangeVo
{
    public int VoLevel { get; set; }
    public string VoLevelName { get; set; } = string.Empty;
    public long VoBeforeExpRequired { get; set; }
    public long VoAfterExpRequired { get; set; }
    public long VoBeforeExpCumulative { get; set; }
    public long VoAfterExpCumulative { get; set; }
    public bool VoChanged { get; set; }
}

/// <summary>等级配置重算的只读预览。</summary>
public sealed class ExperienceLevelRecalculationPreviewVo
{
    public string VoFingerprint { get; set; } = string.Empty;
    public string VoFormulaType { get; set; } = string.Empty;
    public string VoFormulaSummary { get; set; } = string.Empty;
    public int VoChangedLevelCount { get; set; }
    public List<int> VoMissingLevels { get; set; } = [];
    public List<ExperienceLevelRecalculationChangeVo> VoChanges { get; set; } = [];
}

/// <summary>等级配置重算审计。</summary>
public sealed class ExperienceLevelRecalculationAuditVo
{
    public long VoAuditId { get; set; }
    public string VoFormulaType { get; set; } = string.Empty;
    public string VoFormulaSummary { get; set; } = string.Empty;
    public string VoPreviewFingerprint { get; set; } = string.Empty;
    public int VoChangedLevelCount { get; set; }
    public string VoReason { get; set; } = string.Empty;
    public long VoOperatorId { get; set; }
    public string VoOperatorName { get; set; } = string.Empty;
    public DateTime VoCreateTime { get; set; }
}

/// <summary>等级配置整批重算结果。</summary>
public sealed class ExperienceLevelRecalculationResultVo
{
    public List<LevelConfigVo> VoLevels { get; set; } = [];
    public ExperienceLevelRecalculationAuditVo VoAudit { get; set; } = new();
}
