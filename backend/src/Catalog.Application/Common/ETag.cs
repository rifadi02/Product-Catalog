namespace Catalog.Application.Common;

/// <summary>
/// Entity-tag formatting and comparison for the <c>xmin</c> concurrency token (§3.7 BR-4).
/// Kept out of the handlers because the fiddly part — clients that send the value unquoted,
/// or send <c>*</c>, or send a comma-separated list — is exactly the part that needs a test.
/// </summary>
public static class ETag
{
    /// <summary>Strong ETag for a row version, e.g. <c>"8125"</c>.</summary>
    public static string From(uint version) => $"\"{version}\"";

    /// <summary>
    /// True when an <c>If-Match</c> / <c>If-None-Match</c> header value matches
    /// <paramref name="version"/>. Accepts <c>*</c> (matches any existing resource), a bare
    /// number, a quoted value, a <c>W/</c>-prefixed value, and a comma-separated list of any
    /// of those.
    /// </summary>
    public static bool Matches(string? headerValue, uint version)
    {
        if (string.IsNullOrWhiteSpace(headerValue)) return false;

        var expected = version.ToString();

        foreach (var raw in headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = raw;

            if (candidate == "*") return true;

            if (candidate.StartsWith("W/", StringComparison.Ordinal))
                candidate = candidate[2..];

            candidate = candidate.Trim('"');

            if (string.Equals(candidate, expected, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
