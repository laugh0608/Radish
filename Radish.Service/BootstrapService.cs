using System.Net.Mail;
using Radish.Common.HelpTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

public class BootstrapService : IBootstrapService
{
    private readonly IBootstrapRepository _bootstrapRepository;

    public BootstrapService(IBootstrapRepository bootstrapRepository)
    {
        _bootstrapRepository = bootstrapRepository;
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
        var loginName = dto.LoginName.Trim();
        if (string.IsNullOrWhiteSpace(loginName))
        {
            return BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.InvalidInput,
                "登录账号不能为空");
        }

        if (loginName.Length is < 3 or > 50)
        {
            return BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.InvalidInput,
                "登录账号长度必须为 3-50 位");
        }

        if (!loginName.All(IsValidLoginNameCharacter))
        {
            return BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.InvalidInput,
                "登录账号只能包含字母、数字、点、下划线和短横线");
        }

        var normalizedEmail = NormalizeEmail(dto.Email);
        if (normalizedEmail == null && !string.IsNullOrWhiteSpace(dto.Email))
        {
            return BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.InvalidInput,
                "邮箱格式不正确");
        }

        var passwordError = BootstrapPasswordPolicy.Validate(loginName, dto.Password, dto.ConfirmPassword);
        if (!string.IsNullOrWhiteSpace(passwordError))
        {
            return BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.InvalidInput,
                passwordError);
        }

        var passwordHash = PasswordHasher.HashPassword(dto.Password);
        return await _bootstrapRepository.TryCreateFirstAdministratorAsync(loginName, passwordHash, normalizedEmail);
    }

    private static bool IsValidLoginNameCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '.' or '_' or '-';
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var normalized = email.Trim();
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
}
