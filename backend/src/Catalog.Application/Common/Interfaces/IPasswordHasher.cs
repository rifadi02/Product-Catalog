namespace Catalog.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);

    /// <summary>
    /// Verifies <paramref name="password"/> against a fixed dummy hash and discards the result.
    /// Called on the unknown-email login path (§3.2 BR-3) so that "no such user" costs the same
    /// wall-clock time as "wrong password" and the endpoint is not a timing oracle.
    /// </summary>
    void BurnTime(string password);
}
