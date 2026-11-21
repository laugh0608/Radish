namespace Radish.Auth.OpenIddict;

public sealed class WebAuthSession
{
    public Guid Id { get; init; }
    public required string Subject { get; init; }
    public required string TenantId { get; init; }
    public string? AuthorizationId { get; set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime LastActiveAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public long Version { get; init; }
}
