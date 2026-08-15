namespace Catalog.Application.Common.Interfaces;

/// <summary>
/// The authenticated caller, read from claims (§4.3).
///
/// Handlers depend on this, never on <c>IHttpContextAccessor</c>. That is what keeps them
/// unit-testable without a web host.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
}
