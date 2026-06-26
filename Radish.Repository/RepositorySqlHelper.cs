namespace Radish.Repository;

internal static class RepositorySqlHelper
{
    public static string QuoteIdentifier(string identifier)
    {
        return string.Join(".", identifier
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => $"\"{part.Replace("\"", "\"\"")}\""));
    }

    public static bool IsUniqueConstraintException(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
