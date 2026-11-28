using Microsoft.EntityFrameworkCore.Storage;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Radish.Common.HttpContextTool;

namespace Radish.Auth.OpenIddict;

// Keep account revocation ordered with authorization creation and token persistence across Auth instances.
public sealed class WebAccountIssuanceTransaction(AuthOpenIddictDbContext db) : IAsyncDisposable
{
    private IDbContextTransaction? _transaction;

    public sealed class Begin(WebAccountIssuanceTransaction owner)
        : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
    {
        public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
        {
            if (context.Request?.ClientId != WebAuthClient.ClientId ||
                context.EndpointType is not (OpenIddictServerEndpointType.Authorization or OpenIddictServerEndpointType.Token))
                return;

            var subject = context.Principal?.GetClaim(OpenIddictConstants.Claims.Subject);
            var tenant = context.Principal?.GetClaim(UserClaimTypes.TenantId);
            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(tenant))
            {
                context.Reject(OpenIddictConstants.Errors.InvalidGrant);
                return;
            }
            owner._transaction = await WebAccountSessionRevocation.BeginAccountWriteAsync(
                owner.Context, tenant, subject, context.CancellationToken);
        }
    }

    private AuthOpenIddictDbContext Context => db;

    public sealed class Commit(WebAccountIssuanceTransaction owner)
        : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
    {
        public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
        {
            if (owner._transaction is { } transaction)
            {
                await transaction.CommitAsync(context.CancellationToken);
                await owner.DisposeAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is { } transaction)
        {
            _transaction = null;
            await transaction.DisposeAsync();
        }
    }
}
