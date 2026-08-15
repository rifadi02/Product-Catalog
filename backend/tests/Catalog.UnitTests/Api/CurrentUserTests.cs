namespace Catalog.UnitTests.Api;

/// <summary>
/// The claims reader every handler depends on for "who is asking".
///
/// <para>Its whole body is null-guards, which is exactly why an integration test cannot reach
/// most of it: inside a live request there is always an <c>HttpContext</c> and always an
/// <c>Identity</c>, so the defensive arms never run. They do run outside one — a hosted background
/// service resolving <c>ICurrentUser</c> from the root scope has no request at all — and the
/// difference between returning <c>null</c> and throwing a <c>NullReferenceException</c> there is
/// the difference between an unattributed audit row and a crashed worker.</para>
/// </summary>
public sealed class CurrentUserTests
{
    private readonly IHttpContextAccessor _accessor = Substitute.For<IHttpContextAccessor>();

    private ICurrentUser Sut => new CurrentUser(_accessor);

    private void GivenNoRequest() => _accessor.HttpContext.Returns((HttpContext?)null);

    private void GivenAnonymousRequest()
    {
        // A ClaimsIdentity with no authentication type is, by definition, not authenticated.
        _accessor.HttpContext.Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        });
    }

    private void GivenSignedInAs(params Claim[] claims)
    {
        _accessor.HttpContext.Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer"))
        });
    }

    // ── No request at all ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Outside_a_request_every_property_is_empty_rather_than_explosive()
    {
        GivenNoRequest();

        var user = Sut;

        user.IsAuthenticated.Should().BeFalse();
        user.UserId.Should().BeNull();
        user.Email.Should().BeNull();
        user.Role.Should().BeNull();
    }

    // ── Anonymous ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_anonymous_request_carries_no_identity()
    {
        GivenAnonymousRequest();

        var user = Sut;

        user.IsAuthenticated.Should().BeFalse();
        user.UserId.Should().BeNull();
        user.Email.Should().BeNull();
        user.Role.Should().BeNull();
    }

    // ── Signed in ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_signed_in_request_exposes_the_subject_email_and_role()
    {
        var id = Guid.CreateVersion7();

        GivenSignedInAs(
            new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "someone@example.com"),
            new Claim("role", "Admin"));

        var user = Sut;

        user.IsAuthenticated.Should().BeTrue();
        user.UserId.Should().Be(id);
        user.Email.Should().Be("someone@example.com");
        user.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public void The_role_claim_is_matched_without_regard_to_case()
    {
        GivenSignedInAs(new Claim("role", "admin"));

        Sut.Role.Should().Be(UserRole.Admin);
    }

    // ── Present but unusable ──────────────────────────────────────────────────────────────────

    [Fact]
    public void A_subject_claim_that_is_not_a_Guid_reads_as_no_subject()
    {
        GivenSignedInAs(new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid"));

        Sut.UserId.Should().BeNull(
            "a token that authenticated but carries nonsense must not be treated as Guid.Empty");
    }

    [Fact]
    public void A_role_claim_outside_the_enum_reads_as_no_role()
    {
        GivenSignedInAs(new Claim("role", "Superuser"));

        Sut.Role.Should().BeNull(
            "an unrecognised role must not silently fall through to User");
    }

    [Fact]
    public void A_token_with_no_role_claim_has_no_role()
    {
        GivenSignedInAs(new Claim(JwtRegisteredClaimNames.Sub, Guid.CreateVersion7().ToString()));

        Sut.Role.Should().BeNull();
        Sut.Email.Should().BeNull();
        Sut.IsAuthenticated.Should().BeTrue("the token is still valid; it is just sparse");
    }
}
