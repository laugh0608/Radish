using System.Net.Mail;
using Radish.Common.HelpTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

public class BootstrapService : IBootstrapService
{
    private const int DisplayNameMinLength = 2;
    private const int DisplayNameMaxLength = 24;

    private readonly IBootstrapRepository _bootstrapRepository;
    private readonly ICoinService _coinService;
    private readonly ISystemSettingProvider _systemSettingProvider;

    public BootstrapService(
        IBootstrapRepository bootstrapRepository,
        ICoinService coinService,
        ISystemSettingProvider systemSettingProvider)
    {
        _bootstrapRepository = bootstrapRepository;
        _coinService = coinService;
        _systemSettingProvider = systemSettingProvider;
    }

    public async Task<BootstrapStatusVo> GetStatusAsync()
    {
        var administratorExists = await _bootstrapRepository.AdministratorExistsAsync();
        return new BootstrapStatusVo
        {
            VoAdministratorExists = administratorExists,
            VoRequiresAdminInitialization = !administratorExists
        };
    }

    public async Task<BootstrapAdminCreationResult> CreateFirstAdministratorAsync(BootstrapCreateAdminDto dto)
    {
        var displayName = dto.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.InvalidInput,
                "展示名不能为空");
        }

        if (displayName.Length is < DisplayNameMinLength or > DisplayNameMaxLength)
        {
            return BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.InvalidInput,
                $"展示名长度必须为 {DisplayNameMinLength}-{DisplayNameMaxLength} 位");
        }

        if (!displayName.All(IsValidDisplayNameCharacter))
        {
            return BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.InvalidInput,
                "展示名只能包含中文、英文字母和数字");
        }

        var normalizedEmail = NormalizeEmail(dto.Email);
        if (normalizedEmail == null)
        {
            return BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.InvalidInput,
                "请填写有效的电子邮箱");
        }

        var passwordError = BootstrapPasswordPolicy.Validate(displayName, normalizedEmail, dto.Password, dto.ConfirmPassword);
        if (!string.IsNullOrWhiteSpace(passwordError))
        {
            return BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.InvalidInput,
                passwordError);
        }

        var passwordHash = PasswordHasher.HashPassword(dto.Password);
        var publicIndexReservationPolicy = await GetPublicIndexReservationPolicyAsync();
        var result = await _bootstrapRepository.TryCreateFirstAdministratorAsync(
            displayName,
            passwordHash,
            normalizedEmail,
            publicIndexReservationPolicy);
        if (result.Status == BootstrapAdminCreationStatus.Created)
        {
            await _coinService.GrantRegistrationRewardAsync(result.UserId);
        }

        return result;
    }

    private static bool IsValidDisplayNameCharacter(char value)
    {
        return (value >= '0' && value <= '9') ||
               (value >= 'a' && value <= 'z') ||
               (value >= 'A' && value <= 'Z') ||
               (value >= '\u4e00' && value <= '\u9fff');
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLowerInvariant();
        try
        {
            _ = new MailAddress(normalized);
            return normalized;
        }
        catch
        {
            return null;
        }
    }

    private async Task<PublicIndexReservationPolicy> GetPublicIndexReservationPolicyAsync()
    {
        var reservedIndexes = await _systemSettingProvider.GetEffectiveValueAsync(
            SystemConfigDefaults.PublicIndexReservedIndexesKey);
        var vanityRules = await _systemSettingProvider.GetEffectiveValueAsync(
            SystemConfigDefaults.PublicIndexVanityRulesKey);
        return PublicIndexReservationPolicy.FromSettings(reservedIndexes, vanityRules);
    }
}
