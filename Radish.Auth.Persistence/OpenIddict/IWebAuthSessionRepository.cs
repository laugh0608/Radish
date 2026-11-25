namespace Radish.Auth.OpenIddict;

public interface IWebAuthSessionRepository
{
    Task<WebAuthSession?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(WebAuthSession session, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(WebAuthSession session, CancellationToken cancellationToken);
}
