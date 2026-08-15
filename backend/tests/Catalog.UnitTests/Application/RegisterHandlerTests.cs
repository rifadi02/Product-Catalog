namespace Catalog.UnitTests.Application;

public sealed class RegisterHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private RegisterHandler CreateHandler()
    {
        _clock.UtcNow.Returns(Now);
        _hasher.Hash(Arg.Any<string>()).Returns("bcrypt-hash");
        return new RegisterHandler(_users, _uow, _hasher, _clock, NullLogger<RegisterHandler>.Instance);
    }

    private static RegisterRequest Request(string email = "new@example.com") => new()
    {
        Email = email,
        Password = "Str0ngPassw0rd!",
        ConfirmPassword = "Str0ngPassw0rd!"
    };

    [Fact]
    public async Task A_new_account_is_always_created_with_the_User_role()
    {
        _users.EmailExistsAsync(Arg.Any<string>()).Returns(false);

        User? added = null;
        _users.When(u => u.Add(Arg.Any<User>())).Do(c => added = c.Arg<User>());

        var result = await CreateHandler().HandleAsync(Request());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Role.Should().Be(nameof(UserRole.User));
        added!.Role.Should().Be(UserRole.User);
    }

    [Fact]
    public async Task The_email_is_normalised_before_the_uniqueness_check_and_before_storage()
    {
        _users.EmailExistsAsync(Arg.Any<string>()).Returns(false);

        User? added = null;
        _users.When(u => u.Add(Arg.Any<User>())).Do(c => added = c.Arg<User>());

        var result = await CreateHandler().HandleAsync(Request("  Bob@Example.COM "));

        await _users.Received(1).EmailExistsAsync("bob@example.com", Arg.Any<CancellationToken>());
        added!.Email.Value.Should().Be("bob@example.com");
        result.Value!.Email.Should().Be("bob@example.com");
    }

    [Fact]
    public async Task The_password_is_hashed_and_never_stored_in_the_clear()
    {
        _users.EmailExistsAsync(Arg.Any<string>()).Returns(false);

        User? added = null;
        _users.When(u => u.Add(Arg.Any<User>())).Do(c => added = c.Arg<User>());

        await CreateHandler().HandleAsync(Request());

        _hasher.Received(1).Hash("Str0ngPassw0rd!");
        added!.PasswordHash.Should().Be("bcrypt-hash");
        added.PasswordHash.Should().NotBe("Str0ngPassw0rd!");
    }

    [Fact]
    public async Task A_duplicate_email_is_a_conflict_and_writes_nothing()
    {
        _users.EmailExistsAsync("taken@example.com").Returns(true);

        var result = await CreateHandler().HandleAsync(Request("taken@example.com"));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be("email_already_registered");

        _users.DidNotReceive().Add(Arg.Any<User>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_structurally_invalid_email_is_a_validation_failure()
    {
        var result = await CreateHandler().HandleAsync(Request("not-an-address"));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);

        await _users.DidNotReceive().EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registration_does_not_issue_tokens()
    {
        _users.EmailExistsAsync(Arg.Any<string>()).Returns(false);

        var result = await CreateHandler().HandleAsync(Request());

        typeof(RegisterResponse).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(["Id", "Email", "Role", "CreatedAt"]);

        result.Value!.CreatedAt.Should().Be(Now);
    }
}
