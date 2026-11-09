using Liwa.Application.Abstractions;
using Liwa.Application.Auth;
using Liwa.Domain.Users;
using Liwa.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Liwa.UnitTests.Auth;

/// <summary>"Sign in with your Liwa account": who gets in, and how accounts are linked.</summary>
public sealed class ExternalSignInTests
{
    private static readonly DateTimeOffset Now = new(2027, 1, 10, 8, 0, 0, TimeSpan.Zero);
    private const string Tenant = "11111111-1111-1111-1111-111111111111";

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Users _users = new();

    private AuthService Service() => new(
        _users,
        new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions { Iterations = 10_000 })),
        new Tokens(),
        _clock,
        NullLogger<AuthService>.Instance);

    private User AddUser(string email = "202700101@lu.ac.ae", bool active = true)
    {
        var user = new User(Guid.NewGuid(), "student.new", "Mariam", email, UserRole.Student, "202700101", Now.AddDays(-30));
        if (!active) user.Deactivate();
        _users.Items.Add(user);
        return user;
    }

    private static ExternalIdentity Identity(string oid = "aaaa", string? email = "202700101@lu.ac.ae", string? upn = null) =>
        new("entra", Tenant, oid, email, upn, "Mariam Al Mansoori");

    [Fact]
    public async Task First_sign_in_matches_by_university_email_and_links_the_account()
    {
        var user = AddUser();

        var result = await Service().SignInWithExternalIdentityAsync(Identity(), default);

        Assert.True(result.Succeeded);
        Assert.Equal("token-for-" + user.Id, result.Success!.AccessToken);
        Assert.Equal($"entra:{Tenant}:aaaa", user.ExternalSubject);
        Assert.Equal(Now, user.LastSignInAt);
    }

    [Fact]
    public async Task Email_match_ignores_case_and_falls_back_to_the_sign_in_name()
    {
        var user = AddUser();

        var result = await Service().SignInWithExternalIdentityAsync(Identity(email: null, upn: "  202700101@LU.AC.AE "), default);

        Assert.True(result.Succeeded);
        Assert.NotNull(user.ExternalSubject);
    }

    [Fact]
    public async Task Later_sign_ins_use_the_linked_identity_even_if_the_email_changed()
    {
        var user = AddUser();
        await Service().SignInWithExternalIdentityAsync(Identity(), default);

        var result = await Service().SignInWithExternalIdentityAsync(Identity(email: "new.name@lu.ac.ae"), default);

        Assert.True(result.Succeeded);
        Assert.Equal("token-for-" + user.Id, result.Success!.AccessToken);
    }

    [Fact]
    public async Task Someone_without_an_account_is_not_let_in()
    {
        AddUser();

        var result = await Service().SignInWithExternalIdentityAsync(Identity(email: "visitor@lu.ac.ae"), default);

        Assert.False(result.Succeeded);
        Assert.Equal(ExternalSignInFailure.NotRegistered, result.Failure);
    }

    [Fact]
    public async Task An_account_linked_to_another_person_is_never_re_linked()
    {
        var user = AddUser();
        await Service().SignInWithExternalIdentityAsync(Identity(oid: "original-person"), default);

        // The same email now belongs to a different directory account (e.g. the address was reassigned).
        var result = await Service().SignInWithExternalIdentityAsync(Identity(oid: "someone-else"), default);

        Assert.False(result.Succeeded);
        Assert.Equal(ExternalSignInFailure.AccountMismatch, result.Failure);
        Assert.Equal($"entra:{Tenant}:original-person", user.ExternalSubject);
    }

    [Fact]
    public async Task A_switched_off_account_is_refused_and_not_linked()
    {
        var user = AddUser(active: false);

        var result = await Service().SignInWithExternalIdentityAsync(Identity(), default);

        Assert.Equal(ExternalSignInFailure.Inactive, result.Failure);
        Assert.Null(user.ExternalSubject);
    }

    [Fact]
    public async Task Values_that_are_not_email_addresses_are_never_used_for_matching()
    {
        AddUser(email: "202700101@lu.ac.ae");

        var result = await Service().SignInWithExternalIdentityAsync(Identity(email: null, upn: "202700101"), default);

        Assert.Equal(ExternalSignInFailure.NotRegistered, result.Failure);
    }

    private sealed class Users : IUserRepository
    {
        public List<User> Items { get; } = [];

        public Task<User?> FindByUsernameAsync(string username, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(u => u.Username == username));

        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(u => u.Id == id));

        public Task<User?> FindByExternalSubjectAsync(string subject, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(u => u.ExternalSubject == subject));

        public Task<User?> FindByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(u => u.Email == email));

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Tokens : ITokenIssuer
    {
        public AccessToken Issue(User user) => new("token-for-" + user.Id, Now.AddHours(8));
    }
}
