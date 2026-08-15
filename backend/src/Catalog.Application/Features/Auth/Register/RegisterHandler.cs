namespace Catalog.Application.Features.Auth.Register;

/// <summary>§3.1 — <c>POST /api/v1/auth/register</c>.</summary>
public sealed class RegisterHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IPasswordHasher hasher,
    IClock clock,
    ILogger<RegisterHandler> logger)
{
    public async Task<Result<RegisterResponse>> HandleAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = Email.Create(request.Email);
        if (!email.IsSuccess)
            return Result<RegisterResponse>.Failure(email.Error!);

        if (await users.EmailExistsAsync(email.Value.Value, ct))
        {
            logger.LogInformation("Registration rejected: {Email} already exists", email.Value.Value);
            return Result<RegisterResponse>.Failure(Error.Conflict(
                "email_already_registered", "An account with this email already exists."));
        }

        var user = User.Create(
            email.Value,
            hasher.Hash(request.Password),
            UserRole.User,
            clock.UtcNow);

        users.Add(user);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} registered", user.Id);

        return Result<RegisterResponse>.Success(
            new RegisterResponse(user.Id, user.Email.Value, user.Role.ToString(), user.CreatedAt));
    }
}
