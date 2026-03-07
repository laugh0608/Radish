namespace Radish.Common.HttpContextTool;

public sealed class CurrentUser
{
    public static CurrentUser Anonymous { get; } = new();

    public bool IsAuthenticated { get; init; }

    public long UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public long TenantId { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    public bool IsInRole(string role)
    {
        return !string.IsNullOrWhiteSpace(role)
               && Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasScope(string scope)
    {
        return !string.IsNullOrWhiteSpace(scope)
               && Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
    }
}
