namespace Catalog.Api.Identity;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue("role"), ignoreCase: true, out var role)
            ? role
            : null;
}
