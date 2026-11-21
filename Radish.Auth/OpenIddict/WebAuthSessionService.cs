namespace Radish.Auth.OpenIddict;

public sealed class WebAuthSessionService(IWebAuthSessionRepository repository, TimeProvider clock) : IWebAuthSessionService
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromDays(7);
    private static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ActivityWriteInterval = TimeSpan.FromMinutes(1);
    private const int MaxUpdateAttempts = 3;

    public async Task<Guid> CreatePendingAsync(string subject, string tenantId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var now = clock.GetUtcNow().UtcDateTime;
        var session = new WebAuthSession
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            TenantId = tenantId,
            CreatedAtUtc = now,
            LastActiveAtUtc = now
        };
        await repository.AddAsync(session, cancellationToken);
        return session.Id;
    }

    public Task<bool> BindAuthorizationAsync(WebAuthSessionBinding binding, CancellationToken cancellationToken)
    {
        return UpdateAsync(binding.SessionId, (session, now) =>
        {
            if (!MatchesIdentity(session, binding) || session.ActivatedAtUtc.HasValue ||
                GetStatus(session, now) != WebAuthSessionGrantStatus.Allowed)
            {
                return Change.Rejected;
            }

            if (session.AuthorizationId is not null)
            {
                return MatchesAuthorization(session, binding) ? Change.Unchanged : Change.Rejected;
            }

            session.AuthorizationId = binding.AuthorizationId;
            return Change.Update;
        }, cancellationToken);
    }

    public Task<bool> ActivateAsync(WebAuthSessionBinding binding, CancellationToken cancellationToken)
    {
        return UpdateAsync(binding.SessionId, (session, now) =>
        {
            if (!MatchesIdentity(session, binding) || !MatchesAuthorization(session, binding) ||
                GetStatus(session, now) != WebAuthSessionGrantStatus.Allowed)
            {
                return Change.Rejected;
            }

            if (session.ActivatedAtUtc.HasValue)
            {
                return Change.Unchanged;
            }

            session.ActivatedAtUtc = now;
            return Change.Update;
        }, cancellationToken);
    }

    public Task<bool> RecordActivityAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return UpdateAsync(sessionId, (session, now) =>
        {
            if (!session.ActivatedAtUtc.HasValue || GetStatus(session, now) != WebAuthSessionGrantStatus.Allowed)
            {
                return Change.Rejected;
            }

            if (now - session.LastActiveAtUtc < ActivityWriteInterval)
            {
                return Change.Unchanged;
            }

            session.LastActiveAtUtc = now;
            return Change.Update;
        }, cancellationToken);
    }

    public Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return UpdateAsync(sessionId, (session, now) =>
        {
            if (session.RevokedAtUtc.HasValue)
            {
                return Change.Unchanged;
            }

            session.RevokedAtUtc = now;
            return Change.Update;
        }, cancellationToken);
    }

    public async Task<WebAuthSessionGrant> EvaluateGrantAsync(
        WebAuthSessionBinding binding, bool isRefresh, CancellationToken cancellationToken)
    {
        var session = await repository.FindAsync(binding.SessionId, cancellationToken);
        if (session is null || !MatchesIdentity(session, binding) || !MatchesAuthorization(session, binding) ||
            session.ActivatedAtUtc.HasValue != isRefresh)
        {
            return new(WebAuthSessionGrantStatus.Invalid);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var status = GetStatus(session, now);
        if (status != WebAuthSessionGrantStatus.Allowed)
        {
            return new(status);
        }

        var absoluteDeadline = session.CreatedAtUtc + AbsoluteLifetime;
        var idleDeadline = session.LastActiveAtUtc + IdleTimeout;
        var sessionDeadline = idleDeadline < absoluteDeadline ? idleDeadline : absoluteDeadline;
        var accessDeadline = now + AccessTokenLifetime;
        if (accessDeadline > sessionDeadline)
        {
            accessDeadline = sessionDeadline;
        }

        return new(status, new DateTimeOffset(accessDeadline, TimeSpan.Zero), new DateTimeOffset(absoluteDeadline, TimeSpan.Zero));
    }

    private async Task<bool> UpdateAsync(
        Guid id, Func<WebAuthSession, DateTime, Change> change, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxUpdateAttempts; attempt++)
        {
            var session = await repository.FindAsync(id, cancellationToken);
            if (session is null)
            {
                return false;
            }

            var mutation = change(session, clock.GetUtcNow().UtcDateTime);
            if (mutation != Change.Update)
            {
                return mutation == Change.Unchanged;
            }

            if (await repository.UpdateAsync(session, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static WebAuthSessionGrantStatus GetStatus(WebAuthSession session, DateTime now)
    {
        if (session.RevokedAtUtc.HasValue || now < session.CreatedAtUtc)
        {
            return WebAuthSessionGrantStatus.Invalid;
        }

        if (now >= session.CreatedAtUtc + AbsoluteLifetime)
        {
            return WebAuthSessionGrantStatus.AbsoluteExpired;
        }

        return now >= session.LastActiveAtUtc + IdleTimeout
            ? WebAuthSessionGrantStatus.IdleExpired
            : WebAuthSessionGrantStatus.Allowed;
    }

    private static bool MatchesIdentity(WebAuthSession session, WebAuthSessionBinding binding)
    {
        return session.Subject == binding.Subject && session.TenantId == binding.TenantId;
    }

    private static bool MatchesAuthorization(WebAuthSession session, WebAuthSessionBinding binding)
    {
        return !string.IsNullOrWhiteSpace(binding.AuthorizationId) && session.AuthorizationId == binding.AuthorizationId;
    }

    private enum Change { Rejected, Unchanged, Update }
}
