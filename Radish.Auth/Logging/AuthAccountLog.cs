using Radish.Common.LogTool;

namespace Radish.Auth.Logging;

/// <summary>账户流程只提交固定阶段、原因和异常分类；不接收身份、URL 或异常正文。</summary>
internal static class AuthAccountLog
{
    internal static void LoginRejected(ILogger logger, string reason) =>
        logger.LogDebug(new EventId(1, "LoginRejected"), "Login result rejected; reason={authReason}.", reason);

    internal static void LoginResultPrepared(ILogger logger, long durationMs) =>
        logger.LogInformation(new EventId(2, "LoginResultPrepared"), "Login result prepared; duration={durationMs}.", durationMs);

    internal static void UserCreateReturned(ILogger logger) =>
        logger.LogDebug(new EventId(3, "RegistrationUserCreateReturned"), "Registration user creation call returned.");

    internal static void RewardReturned(ILogger logger) =>
        logger.LogDebug(new EventId(4, "RegistrationRewardReturned"), "Registration reward call returned.");

    internal static void RegistrationResultPrepared(ILogger logger) =>
        logger.LogInformation(new EventId(5, "RegistrationResultPrepared"), "Registration result prepared.");

    internal static void RegistrationFailed(ILogger logger, string stage, Exception exception) =>
        logger.LogError(new EventId(6, "RegistrationFailed"),
            "Registration failure result prepared; stage={authStage}; kind={failureKind}.", stage, RuntimeFailureSummary.Classify(exception));

    internal static void ClientResolutionReturned(ILogger logger, string outcome) =>
        logger.LogDebug(new EventId(7, "ClientResolutionReturned"), "Client summary returned; outcome={authClientOutcome}.", outcome);

    internal static void ClientResolutionFailed(ILogger logger, Exception exception) =>
        logger.LogWarning(new EventId(8, "ClientResolutionFailed"), "Client summary unavailable; kind={failureKind}.", RuntimeFailureSummary.Classify(exception));

    internal static void ReturnUrlParseFailed(ILogger logger, Exception exception) =>
        logger.LogWarning(new EventId(9, "ReturnUrlParseFailed"), "Return URL parsing failed; kind={failureKind}.", RuntimeFailureSummary.Classify(exception));
}
