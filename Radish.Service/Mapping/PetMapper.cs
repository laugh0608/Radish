using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class PetMapper
{
    // PetProfile → PetProfileVo
    [MapProperty(nameof(PetProfile.Id), nameof(PetProfileVo.VoId))]
    [MapProperty(nameof(PetProfile.PublicId), nameof(PetProfileVo.VoPublicId))]
    [MapProperty(nameof(PetProfile.UserId), nameof(PetProfileVo.VoUserId))]
    [MapProperty(nameof(PetProfile.Name), nameof(PetProfileVo.VoName))]
    [MapProperty(nameof(PetProfile.SpeciesKey), nameof(PetProfileVo.VoSpeciesKey))]
    [MapProperty(nameof(PetProfile.ShapeKey), nameof(PetProfileVo.VoShapeKey))]
    [MapProperty(nameof(PetProfile.GrowthStage), nameof(PetProfileVo.VoGrowthStage))]
    [MapProperty(nameof(PetProfile.Mood), nameof(PetProfileVo.VoMood))]
    [MapProperty(nameof(PetProfile.Satiety), nameof(PetProfileVo.VoSatiety))]
    [MapProperty(nameof(PetProfile.Cleanliness), nameof(PetProfileVo.VoCleanliness))]
    [MapProperty(nameof(PetProfile.Energy), nameof(PetProfileVo.VoEnergy))]
    [MapProperty(nameof(PetProfile.GrowthValue), nameof(PetProfileVo.VoGrowthValue))]
    [MapProperty(nameof(PetProfile.EquippedBackgroundKey), nameof(PetProfileVo.VoEquippedBackgroundKey))]
    [MapProperty(nameof(PetProfile.EquippedToyKey), nameof(PetProfileVo.VoEquippedToyKey))]
    [MapProperty(nameof(PetProfile.IsPublic), nameof(PetProfileVo.VoIsPublic))]
    [MapProperty(nameof(PetProfile.LastCareTime), nameof(PetProfileVo.VoLastCareTime))]
    [MapProperty(nameof(PetProfile.CreateTime), nameof(PetProfileVo.VoCreateTime))]
    [MapperIgnoreTarget(nameof(PetProfileVo.VoCareActions))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PetProfileVo? ToPetProfileVo(PetProfile? source);

    public static partial List<PetProfileVo> ToPetProfileVoList(IEnumerable<PetProfile>? source);

    // PetStatLog → PetStatLogVo
    [MapProperty(nameof(PetStatLog.Id), nameof(PetStatLogVo.VoId))]
    [MapProperty(nameof(PetStatLog.PetProfileId), nameof(PetStatLogVo.VoPetProfileId))]
    [MapProperty(nameof(PetStatLog.PetPublicId), nameof(PetStatLogVo.VoPetPublicId))]
    [MapProperty(nameof(PetStatLog.ActionType), nameof(PetStatLogVo.VoActionType))]
    [MapProperty(nameof(PetStatLog.BeforeSatiety), nameof(PetStatLogVo.VoBeforeSatiety))]
    [MapProperty(nameof(PetStatLog.AfterSatiety), nameof(PetStatLogVo.VoAfterSatiety))]
    [MapProperty(nameof(PetStatLog.BeforeCleanliness), nameof(PetStatLogVo.VoBeforeCleanliness))]
    [MapProperty(nameof(PetStatLog.AfterCleanliness), nameof(PetStatLogVo.VoAfterCleanliness))]
    [MapProperty(nameof(PetStatLog.BeforeEnergy), nameof(PetStatLogVo.VoBeforeEnergy))]
    [MapProperty(nameof(PetStatLog.AfterEnergy), nameof(PetStatLogVo.VoAfterEnergy))]
    [MapProperty(nameof(PetStatLog.GrowthDelta), nameof(PetStatLogVo.VoGrowthDelta))]
    [MapProperty(nameof(PetStatLog.Message), nameof(PetStatLogVo.VoMessage))]
    [MapProperty(nameof(PetStatLog.CreateTime), nameof(PetStatLogVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial PetStatLogVo? ToPetStatLogVo(PetStatLog? source);

    public static partial List<PetStatLogVo> ToPetStatLogVoList(IEnumerable<PetStatLog>? source);

}
