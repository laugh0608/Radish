using System.Globalization;
using Radish.Auth.Models;

namespace Radish.Auth.OpenIddict;

public static class IdleSessionPolicy
{
    public const string LastActiveClaim = "radish_idle_last_active_at";
    public const string LastActiveParameter = "radish_last_active_at";
    public const string ExpiredErrorDescription = "session_idle_expired";

    public static long ToUnixTimeSeconds(DateTimeOffset value)
    {
        return value.ToUnixTimeSeconds();
    }

    public static long? ParseUnixTimeSeconds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;
    }

    public static long? NormalizeClientActivityUnixTimeSeconds(
        string? value,
        DateTimeOffset now,
        IdleSessionOptions options)
    {
        var parsed = ParseUnixTimeSeconds(value);
        if (parsed is null)
        {
            return null;
        }

        var nowSeconds = ToUnixTimeSeconds(now);
        var maxAllowedSeconds = now.Add(options.ClientActivityFutureTolerance).ToUnixTimeSeconds();
        if (parsed.Value > maxAllowedSeconds)
        {
            return nowSeconds;
        }

        return parsed.Value;
    }

    public static IdleSessionEvaluation Evaluate(
        long? storedLastActiveAt,
        long? clientLastActiveAt,
        DateTimeOffset now,
        IdleSessionOptions options)
    {
        if (!options.Enable)
        {
            return IdleSessionEvaluation.Valid(ResolveEffectiveLastActiveAt(storedLastActiveAt, clientLastActiveAt));
        }

        var effectiveLastActiveAt = ResolveEffectiveLastActiveAt(storedLastActiveAt, clientLastActiveAt);
        if (effectiveLastActiveAt is null)
        {
            return IdleSessionEvaluation.Valid(null);
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(effectiveLastActiveAt.Value)
            .Add(options.IdleTimeout)
            .Add(options.ClockSkew);

        return now > expiresAt
            ? IdleSessionEvaluation.Expired(effectiveLastActiveAt.Value)
            : IdleSessionEvaluation.Valid(effectiveLastActiveAt.Value);
    }

    public static long ResolveNextLastActiveAt(
        long? existingLastActiveAt,
        long? clientLastActiveAt,
        DateTimeOffset now)
    {
        return ResolveEffectiveLastActiveAt(existingLastActiveAt, clientLastActiveAt) ?? ToUnixTimeSeconds(now);
    }

    private static long? ResolveEffectiveLastActiveAt(long? storedLastActiveAt, long? clientLastActiveAt)
    {
        if (storedLastActiveAt is null)
        {
            return clientLastActiveAt;
        }

        if (clientLastActiveAt is null)
        {
            return storedLastActiveAt;
        }

        return Math.Max(storedLastActiveAt.Value, clientLastActiveAt.Value);
    }
}

public readonly record struct IdleSessionEvaluation(bool IsExpired, long? EffectiveLastActiveAt)
{
    public static IdleSessionEvaluation Valid(long? effectiveLastActiveAt)
    {
        return new IdleSessionEvaluation(false, effectiveLastActiveAt);
    }

    public static IdleSessionEvaluation Expired(long effectiveLastActiveAt)
    {
        return new IdleSessionEvaluation(true, effectiveLastActiveAt);
    }
}
