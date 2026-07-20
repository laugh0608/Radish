using AutoMapper;
using Radish.Common.Exceptions;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Service;

/// <summary>电子宠物服务</summary>
public class PetService : BaseService<PetProfile, PetProfileVo>, IPetService
{
    private const int MaxStatValue = 100;
    private const int MinStatValue = 0;
    private const string DefaultPetName = "小萝卜";

    private static readonly IReadOnlyDictionary<string, PetCareRule> CareRules = new Dictionary<string, PetCareRule>
    {
        [PetCareActionTypes.Feed] = new(PetCareActionTypes.Feed, 3, 30, 24, 0, -4, 5, "小萝卜吃饱了一些。"),
        [PetCareActionTypes.Clean] = new(PetCareActionTypes.Clean, 3, 30, 0, 25, -4, 4, "小萝卜变清爽了。"),
        [PetCareActionTypes.Play] = new(PetCareActionTypes.Play, 3, 45, -6, -5, -14, 6, "小萝卜玩得很开心。"),
        [PetCareActionTypes.Rest] = new(PetCareActionTypes.Rest, 2, 60, -2, 0, 28, 3, "小萝卜恢复了精力。")
    };

    private readonly IBaseRepository<PetProfile> _petRepository;
    private readonly IBaseRepository<PetStatLog> _logRepository;

    public PetService(
        IMapper mapper,
        IBaseRepository<PetProfile> baseRepository,
        IBaseRepository<PetStatLog> logRepository)
        : base(mapper, baseRepository)
    {
        _petRepository = baseRepository;
        _logRepository = logRepository;
    }

    public async Task<PetProfileVo?> GetMyPetAsync(long userId)
    {
        EnsureUser(userId);
        var pet = await GetActivePetAsync(userId);
        return pet == null ? null : await BuildPetVoAsync(pet);
    }

    public async Task<PetProfileVo> ClaimAsync(long userId, string operatorName, long tenantId, PetClaimDto request)
    {
        EnsureUser(userId);
        ArgumentNullException.ThrowIfNull(request);

        var existing = await GetActivePetAsync(userId);
        if (existing != null)
        {
            return await BuildPetVoAsync(existing);
        }

        var normalizedName = NormalizeName(request.Name);
        var now = DateTime.UtcNow;
        var pet = new PetProfile
        {
            UserId = userId,
            Name = normalizedName,
            TenantId = tenantId,
            CreateTime = now,
            CreateBy = NormalizeOperatorName(operatorName),
            CreateId = userId
        };

        var id = await _petRepository.AddAsync(pet);
        pet.Id = id;
        return await BuildPetVoAsync(pet);
    }

    public async Task<PetProfileVo> UpdateProfileAsync(long userId, string operatorName, UpdatePetProfileDto request)
    {
        EnsureUser(userId);
        ArgumentNullException.ThrowIfNull(request);

        var pet = await RequirePetAsync(userId);
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            pet.Name = NormalizeName(request.Name);
        }

        if (request.IsPublic.HasValue)
        {
            pet.IsPublic = request.IsPublic.Value;
        }

        pet.ModifyTime = DateTime.UtcNow;
        pet.ModifyBy = NormalizeOperatorName(operatorName);
        pet.ModifyId = userId;
        await _petRepository.UpdateAsync(pet);
        return await BuildPetVoAsync(pet);
    }

    public async Task<PetCareResultVo> CareAsync(long userId, string operatorName, PetCareDto request)
    {
        EnsureUser(userId);
        ArgumentNullException.ThrowIfNull(request);

        var actionType = NormalizeActionType(request.ActionType);
        if (!CareRules.TryGetValue(actionType, out var rule))
        {
            throw new BusinessException(
                "不支持的照顾动作",
                (int)HttpStatusCodeEnum.BadRequest,
                "Pet.InvalidAction",
                "error.pet.invalid_action");
        }

        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        if (idempotencyKey != null)
        {
            var existingLog = await _logRepository.QueryFirstAsync(log =>
                log.UserId == userId &&
                log.IdempotencyKey == idempotencyKey &&
                !log.IsDeleted);
            if (existingLog != null)
            {
                var currentPet = await RequirePetAsync(userId);
                return new PetCareResultVo
                {
                    VoPet = await BuildPetVoAsync(currentPet),
                    VoLog = Mapper.Map<PetStatLogVo>(existingLog),
                    VoMessage = existingLog.Message
                };
            }
        }

        var pet = await RequirePetAsync(userId);
        var now = DateTime.UtcNow;
        await EnsureCareRuleAvailableAsync(pet, rule, now);

        var beforeSatiety = pet.Satiety;
        var beforeCleanliness = pet.Cleanliness;
        var beforeEnergy = pet.Energy;

        pet.Satiety = ClampStat(pet.Satiety + rule.SatietyDelta);
        pet.Cleanliness = ClampStat(pet.Cleanliness + rule.CleanlinessDelta);
        pet.Energy = ClampStat(pet.Energy + rule.EnergyDelta);
        pet.GrowthValue = Math.Max(0, pet.GrowthValue + rule.GrowthDelta);
        pet.GrowthStage = ResolveGrowthStage(pet.GrowthValue);
        pet.Mood = ResolveMood(pet);
        pet.LastCareTime = now;
        pet.ModifyTime = now;
        pet.ModifyBy = NormalizeOperatorName(operatorName);
        pet.ModifyId = userId;

        await _petRepository.UpdateAsync(pet);

        var log = new PetStatLog
        {
            UserId = userId,
            PetProfileId = pet.Id,
            PetPublicId = pet.PublicId,
            ActionType = rule.ActionType,
            Source = "care",
            IdempotencyKey = idempotencyKey ?? $"care:{pet.PublicId}:{Guid.CreateVersion7():N}",
            BeforeSatiety = beforeSatiety,
            AfterSatiety = pet.Satiety,
            BeforeCleanliness = beforeCleanliness,
            AfterCleanliness = pet.Cleanliness,
            BeforeEnergy = beforeEnergy,
            AfterEnergy = pet.Energy,
            GrowthDelta = rule.GrowthDelta,
            Message = rule.Message,
            TenantId = pet.TenantId,
            CreateTime = now,
            CreateBy = NormalizeOperatorName(operatorName),
            CreateId = userId
        };

        var logId = await _logRepository.AddAsync(log);
        log.Id = logId;

        return new PetCareResultVo
        {
            VoPet = await BuildPetVoAsync(pet),
            VoLog = Mapper.Map<PetStatLogVo>(log),
            VoMessage = rule.Message
        };
    }

    public async Task<VoPagedResult<PetStatLogVo>> GetMyLogsAsync(long userId, int pageIndex, int pageSize)
    {
        EnsureUser(userId);
        var safePageIndex = pageIndex < 1 ? 1 : pageIndex;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
        var pet = await GetActivePetAsync(userId);
        if (pet == null)
        {
            return new VoPagedResult<PetStatLogVo>
            {
                VoPageIndex = safePageIndex,
                VoPageSize = safePageSize
            };
        }

        var (items, total) = await _logRepository.QueryPageAsync(
            log => log.UserId == userId && log.PetProfileId == pet.Id && !log.IsDeleted,
            safePageIndex,
            safePageSize,
            log => log.CreateTime,
            OrderByType.Desc,
            log => log.Id,
            OrderByType.Desc);

        return new VoPagedResult<PetStatLogVo>
        {
            VoItems = Mapper.Map<List<PetStatLogVo>>(items),
            VoTotal = total,
            VoPageIndex = safePageIndex,
            VoPageSize = safePageSize
        };
    }

    private async Task<PetProfile?> GetActivePetAsync(long userId)
    {
        return await _petRepository.QueryFirstAsync(pet => pet.UserId == userId && !pet.IsDeleted);
    }

    private async Task<PetProfile> RequirePetAsync(long userId)
    {
        var pet = await GetActivePetAsync(userId);
        if (pet == null)
        {
            throw new BusinessException(
                "请先领取电子宠物",
                (int)HttpStatusCodeEnum.NotFound,
                "Pet.NotClaimed",
                "error.pet.not_claimed");
        }

        return pet;
    }

    private async Task EnsureCareRuleAvailableAsync(PetProfile pet, PetCareRule rule, DateTime now)
    {
        var dayStart = now.Date;
        var dayEnd = dayStart.AddDays(1);
        var usedToday = await _logRepository.QueryCountAsync(log =>
            log.UserId == pet.UserId &&
            log.PetProfileId == pet.Id &&
            log.ActionType == rule.ActionType &&
            log.CreateTime >= dayStart &&
            log.CreateTime < dayEnd &&
            !log.IsDeleted);

        if (usedToday >= rule.DailyLimit)
        {
            throw new BusinessException(
                "今日该照顾动作次数已用完",
                (int)HttpStatusCodeEnum.BadRequest,
                "Pet.DailyLimitReached",
                "error.pet.daily_limit_reached");
        }

        var (latestLogs, _) = await _logRepository.QueryPageAsync(
            log => log.UserId == pet.UserId &&
                   log.PetProfileId == pet.Id &&
                   log.ActionType == rule.ActionType &&
                   !log.IsDeleted,
            1,
            1,
            log => log.CreateTime,
            OrderByType.Desc,
            log => log.Id,
            OrderByType.Desc);

        var latest = latestLogs.FirstOrDefault();
        if (latest == null)
        {
            return;
        }

        var nextAvailableAt = latest.CreateTime.AddMinutes(rule.CooldownMinutes);
        if (nextAvailableAt > now)
        {
            throw new BusinessException(
                "照顾动作仍在冷却中",
                (int)HttpStatusCodeEnum.BadRequest,
                "Pet.CareCooldown",
                "error.pet.care_cooldown");
        }
    }

    private async Task<PetProfileVo> BuildPetVoAsync(PetProfile pet)
    {
        var vo = Mapper.Map<PetProfileVo>(pet);
        vo.VoCareActions = await BuildCareActionStatesAsync(pet);
        return vo;
    }

    private async Task<List<PetCareActionStateVo>> BuildCareActionStatesAsync(PetProfile pet)
    {
        var now = DateTime.UtcNow;
        var dayStart = now.Date;
        var dayEnd = dayStart.AddDays(1);
        var states = new List<PetCareActionStateVo>();

        foreach (var rule in CareRules.Values)
        {
            var usedToday = await _logRepository.QueryCountAsync(log =>
                log.UserId == pet.UserId &&
                log.PetProfileId == pet.Id &&
                log.ActionType == rule.ActionType &&
                log.CreateTime >= dayStart &&
                log.CreateTime < dayEnd &&
                !log.IsDeleted);
            var (latestLogs, _) = await _logRepository.QueryPageAsync(
                log => log.UserId == pet.UserId &&
                       log.PetProfileId == pet.Id &&
                       log.ActionType == rule.ActionType &&
                       !log.IsDeleted,
                1,
                1,
                log => log.CreateTime,
                OrderByType.Desc,
                log => log.Id,
                OrderByType.Desc);
            var latest = latestLogs.FirstOrDefault();
            var nextAvailableAt = latest?.CreateTime.AddMinutes(rule.CooldownMinutes);
            var remaining = Math.Max(0, rule.DailyLimit - usedToday);

            states.Add(new PetCareActionStateVo
            {
                VoActionType = rule.ActionType,
                VoDailyLimit = rule.DailyLimit,
                VoUsedToday = usedToday,
                VoRemainingToday = remaining,
                VoNextAvailableAt = nextAvailableAt,
                VoCanUse = remaining > 0 && (nextAvailableAt == null || nextAvailableAt <= now)
            });
        }

        return states;
    }

    private static void EnsureUser(long userId)
    {
        if (userId <= 0)
        {
            throw new BusinessException(
                "未登录",
                (int)HttpStatusCodeEnum.Unauthorized,
                "Auth.Required",
                "error.auth.unauthorized");
        }
    }

    private static int ClampStat(int value)
    {
        return Math.Clamp(value, MinStatValue, MaxStatValue);
    }

    private static int ResolveGrowthStage(long growthValue)
    {
        return growthValue switch
        {
            < 40 => 1,
            < 120 => 2,
            < 260 => 3,
            _ => 4
        };
    }

    private static string ResolveMood(PetProfile pet)
    {
        if (pet.Satiety < 30)
        {
            return PetMoodTypes.Hungry;
        }

        if (pet.Cleanliness < 30)
        {
            return PetMoodTypes.Messy;
        }

        if (pet.Energy < 30)
        {
            return PetMoodTypes.Tired;
        }

        return pet.Satiety >= 75 && pet.Cleanliness >= 75 && pet.Energy >= 60
            ? PetMoodTypes.Happy
            : PetMoodTypes.Calm;
    }

    private static string NormalizeName(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? DefaultPetName : value.Trim();
        return normalized.Length <= 40 ? normalized : normalized[..40];
    }

    private static string NormalizeOperatorName(string? operatorName)
    {
        var normalized = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();
        return normalized.Length <= 50 ? normalized : normalized[..50];
    }

    private static string NormalizeActionType(string actionType)
    {
        return actionType.Trim().ToLowerInvariant();
    }

    private static string? NormalizeIdempotencyKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= 80 ? normalized : normalized[..80];
    }

    private sealed record PetCareRule(
        string ActionType,
        int DailyLimit,
        int CooldownMinutes,
        int SatietyDelta,
        int CleanlinessDelta,
        int EnergyDelta,
        long GrowthDelta,
        string Message);
}
