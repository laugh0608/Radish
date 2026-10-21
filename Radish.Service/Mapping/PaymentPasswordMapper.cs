using System.Diagnostics.CodeAnalysis;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Shared.Security;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class PaymentPasswordMapper
{
    // UserPaymentPassword → UserPaymentPasswordVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static UserPaymentPasswordVo? ToUserPaymentPasswordVo(UserPaymentPassword? source)
    {
        if (source is null) return null;
        var target = MapUserPaymentPasswordToUserPaymentPasswordVo(source);
        target.VoIsLocked = source.LockedUntil.HasValue && source.LockedUntil.Value > DateTime.Now;
        target.VoLockedRemainingMinutes = source.LockedUntil.HasValue && source.LockedUntil.Value > DateTime.Now ? (int)(source.LockedUntil.Value - DateTime.Now).TotalMinutes : 0;
        target.VoIsLegacyPasscode = !string.IsNullOrEmpty(source.PasswordHash) && PaymentPasscodeRules.RequiresUpgrade(source.PasscodeVersion);
        target.VoRequiresPasscodeUpgrade = !string.IsNullOrEmpty(source.PasswordHash) && PaymentPasscodeRules.RequiresUpgrade(source.PasscodeVersion);
        target.VoHasPaymentPassword = !string.IsNullOrEmpty(source.PasswordHash);
        return target;
    }

    [MapProperty(nameof(UserPaymentPassword.Id), nameof(UserPaymentPasswordVo.VoId))]
    [MapProperty(nameof(UserPaymentPassword.UserId), nameof(UserPaymentPasswordVo.VoUserId))]
    [MapProperty(nameof(UserPaymentPassword.FailedAttempts), nameof(UserPaymentPasswordVo.VoFailedAttempts))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoIsLocked))]
    [MapProperty(nameof(UserPaymentPassword.LockedUntil), nameof(UserPaymentPasswordVo.VoLockedUntil))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoLockedRemainingMinutes))]
    [MapProperty(nameof(UserPaymentPassword.LastUsedTime), nameof(UserPaymentPasswordVo.VoLastUsedTime))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoLastUsedTimeDisplay))]
    [MapProperty(nameof(UserPaymentPassword.LastModifiedTime), nameof(UserPaymentPasswordVo.VoLastModifiedTime))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoLastModifiedTimeDisplay))]
    [MapProperty(nameof(UserPaymentPassword.StrengthLevel), nameof(UserPaymentPasswordVo.VoStrengthLevel))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoStrengthLevelDisplay))]
    [MapProperty(nameof(UserPaymentPassword.PasscodeVersion), nameof(UserPaymentPasswordVo.VoPasscodeVersion))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoIsLegacyPasscode))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoRequiresPasscodeUpgrade))]
    [MapProperty(nameof(UserPaymentPassword.IsEnabled), nameof(UserPaymentPasswordVo.VoIsEnabled))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoHasPaymentPassword))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoSecurityStatus))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoSecuritySuggestions))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoCreatedAt))]
    [MapperIgnoreTarget(nameof(UserPaymentPasswordVo.VoCreatedAtDisplay))]
    [UserMapping(Default = false)]
    private static partial UserPaymentPasswordVo MapUserPaymentPasswordToUserPaymentPasswordVo(UserPaymentPassword source);

    public static partial List<UserPaymentPasswordVo> ToUserPaymentPasswordVoList(IEnumerable<UserPaymentPassword>? source);

}
