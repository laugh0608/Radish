using System.Net.Mail;
using Radish.Common.HelpTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Serilog;

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
        var displayName = (dto.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return InvalidInput(
                "展示名不能为空",
                BootstrapErrorCodes.DisplayNameRequired);
        }

        if (displayName.Length is < DisplayNameMinLength or > DisplayNameMaxLength)
        {
            return InvalidInput(
                $"展示名长度必须为 {DisplayNameMinLength}-{DisplayNameMaxLength} 位",
                BootstrapErrorCodes.DisplayNameLengthInvalid,
                DisplayNameMinLength,
                DisplayNameMaxLength);
        }

        if (!displayName.All(IsValidDisplayNameCharacter))
        {
            return InvalidInput(
                "展示名只能包含中文、英文字母和数字",
                BootstrapErrorCodes.DisplayNameCharactersInvalid);
        }

        var normalizedEmail = NormalizeEmail(dto.Email);
        if (normalizedEmail == null)
        {
            return InvalidInput(
                "请填写有效的电子邮箱",
                BootstrapErrorCodes.EmailInvalid);
        }

        var passwordError = BootstrapPasswordPolicy.Validate(
            displayName,
            normalizedEmail,
            dto.Password,
            dto.ConfirmPassword);
        if (passwordError != null)
        {
            return InvalidInput(
                passwordError.Message,
                passwordError.Code,
                passwordError.MessageArguments);
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
            try
            {
                await _coinService.GrantRegistrationRewardAsync(result.UserId);
            }
            catch (Exception exception)
            {
                Log.Error(
                    exception,
                    "首个管理员已创建，但注册奖励暂未发放：UserId={UserId}",
                    result.UserId);
            }
        }

        return result;
    }

    private static BootstrapAdminCreationResult InvalidInput(
        string fallbackMessage,
        string code,
        params object[] messageArguments)
    {
        return BootstrapAdminCreationResult.Failed(
            BootstrapAdminCreationStatus.InvalidInput,
            fallbackMessage,
            code,
            messageArguments);
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
