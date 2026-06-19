using Radish.Shared.CustomEnum;

namespace Radish.Model.ViewModels;

/// <summary>宠物主档视图模型</summary>
public class PetProfileVo
{
    public long VoId { get; set; }

    public string VoPublicId { get; set; } = string.Empty;

    public long VoUserId { get; set; }

    public string VoName { get; set; } = string.Empty;

    public string VoSpeciesKey { get; set; } = string.Empty;

    public string VoShapeKey { get; set; } = string.Empty;

    public int VoGrowthStage { get; set; }

    public string VoGrowthStageName => VoGrowthStage switch
    {
        <= 1 => "幼芽期",
        2 => "舒展期",
        3 => "成长期",
        _ => "成熟期"
    };

    public string VoMood { get; set; } = PetMoodTypes.Calm;

    public string VoMoodDisplay => VoMood switch
    {
        PetMoodTypes.Happy => "开心",
        PetMoodTypes.Tired => "疲惫",
        PetMoodTypes.Hungry => "有点饿",
        PetMoodTypes.Messy => "想洗洗",
        _ => "安稳"
    };

    public int VoSatiety { get; set; }

    public int VoCleanliness { get; set; }

    public int VoEnergy { get; set; }

    public long VoGrowthValue { get; set; }

    public string? VoEquippedBackgroundKey { get; set; }

    public string? VoEquippedToyKey { get; set; }

    public bool VoIsPublic { get; set; }

    public DateTime? VoLastCareTime { get; set; }

    public DateTime VoCreateTime { get; set; }

    public List<PetCareActionStateVo> VoCareActions { get; set; } = new();
}

/// <summary>宠物照顾动作状态视图模型</summary>
public class PetCareActionStateVo
{
    public string VoActionType { get; set; } = string.Empty;

    public string VoActionName => VoActionType switch
    {
        PetCareActionTypes.Feed => "喂食",
        PetCareActionTypes.Clean => "清洁",
        PetCareActionTypes.Play => "互动",
        PetCareActionTypes.Rest => "休息",
        _ => VoActionType
    };

    public int VoDailyLimit { get; set; }

    public int VoUsedToday { get; set; }

    public int VoRemainingToday { get; set; }

    public DateTime? VoNextAvailableAt { get; set; }

    public bool VoCanUse { get; set; }
}

/// <summary>宠物照顾结果视图模型</summary>
public class PetCareResultVo
{
    public PetProfileVo VoPet { get; set; } = new();

    public PetStatLogVo VoLog { get; set; } = new();

    public string VoMessage { get; set; } = string.Empty;
}

/// <summary>宠物状态流水视图模型</summary>
public class PetStatLogVo
{
    public long VoId { get; set; }

    public long VoPetProfileId { get; set; }

    public string VoPetPublicId { get; set; } = string.Empty;

    public string VoActionType { get; set; } = string.Empty;

    public string VoActionName => VoActionType switch
    {
        PetCareActionTypes.Feed => "喂食",
        PetCareActionTypes.Clean => "清洁",
        PetCareActionTypes.Play => "互动",
        PetCareActionTypes.Rest => "休息",
        _ => VoActionType
    };

    public int VoBeforeSatiety { get; set; }

    public int VoAfterSatiety { get; set; }

    public int VoBeforeCleanliness { get; set; }

    public int VoAfterCleanliness { get; set; }

    public int VoBeforeEnergy { get; set; }

    public int VoAfterEnergy { get; set; }

    public long VoGrowthDelta { get; set; }

    public string VoMessage { get; set; } = string.Empty;

    public DateTime VoCreateTime { get; set; }
}
