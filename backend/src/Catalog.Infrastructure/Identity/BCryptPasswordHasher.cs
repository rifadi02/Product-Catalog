namespace Catalog.Infrastructure.Identity;

/// <summary>
/// §4.7. BCrypt at work factor 12 — roughly 250 ms per hash on commodity hardware. That cost is
/// the feature: it is what makes an offline attack against a stolen table impractical. If login
/// latency is ever complained about, the answer is caching the session, not lowering the factor.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    private static readonly string DummyHash =
        BCrypt.Net.BCrypt.HashPassword("not-a-real-password", WorkFactor);

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    public void BurnTime(string password)
    {
        try
        {
            BCrypt.Net.BCrypt.Verify(password, DummyHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
        }
    }
}
