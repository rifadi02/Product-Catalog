namespace Catalog.Application.Common;

/// <summary>
/// §3.6 BR-3. A user searching for <c>100%</c> must not match every row, and a user searching
/// for <c>a_b</c> must not match <c>axb</c>. Both <c>%</c> and <c>_</c> are wildcards in
/// <c>LIKE</c>/<c>ILIKE</c>, so they — and the escape character itself — are escaped before the
/// term is wrapped in <c>%…%</c>.
/// </summary>
public static class LikePattern
{
    public const char EscapeChar = '\\';

    /// <summary>Escapes LIKE metacharacters in a user-supplied term.</summary>
    public static string Escape(string term)
    {
        var span = term
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return span;
    }

    /// <summary>Builds the <c>%term%</c> pattern for a case-insensitive substring match.</summary>
    public static string Contains(string term) => $"%{Escape(term)}%";
}
