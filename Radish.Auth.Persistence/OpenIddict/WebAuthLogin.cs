namespace Radish.Auth.OpenIddict;

public sealed class WebAuthLogin
{
    public required string StateDigest { get; init; }
    public required string BrowserDigest { get; init; }
    public required string ProtectedRequest { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public Guid? SessionId { get; init; }
}
