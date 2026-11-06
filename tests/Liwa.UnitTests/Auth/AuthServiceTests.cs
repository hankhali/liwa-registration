using Liwa.Application.Abstractions;
using Liwa.Application.Auth;
using Liwa.Domain.Users;
using Liwa.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Liwa.UnitTests.Auth;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2027, 1, 10, 8, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly InMemoryUsers _users = new();
    private Pbkdf2PasswordHasher _hasher = new(Options.Create(new PasswordHashingOptions { Iterations = 10_000 }));

    private AuthService Service() => new(_users, _hasher, new FakeTokens(), _clock, NullLogger<AuthService>.Instance);

    private User AddUser(string username = "student.new", string password = "Liwa#2027", bool active = true)
    {
        var user = new User(Guid.NewGuid(), username, "Test User", null, UserRole.Student, null, Now.AddDays(-30));
        user.SetPasswordHash(_hasher.Hash(password));
        if (!active) user.Deactivate();
        _users.Items.Add(user);
        return user;
    }

    [Fact]
    public async Task Correct_credentials_sign_in_and_record_time()
    {
        var user = AddUser();

        var result = await Service().SignInWithPasswordAsync("student.new", "Liwa#2027", default);

        Assert.True(result.Succeeded);
        Assert.Equal("token-for-" + user.Id, result.Success!.AccessToken);
        Assert.Equal("Student", result.Success.User.Role);
        Assert.Equal(Now, user.LastSignInAt);
    }

    [Theory]
    [InlineData("STUDENT.NEW")]
    [InlineData("  Student.New ")]
    public async Task Username_is_case_and_whitespace_insensitive(string typed)
    {
        AddUser();
        Assert.True((await Service().SignInWithPasswordAsync(typed, "Liwa#2027", default)).Succeeded);
    }

    [Theory]
    [InlineData("student.new", "wrong")]
    [InlineData("nobody", "Liwa#2027")]
    [InlineData("", "Liwa#2027")]
    [InlineData("student.new", "")]
    public async Task Bad_credentials_fail(string username, string password)
    {
        AddUser();
        Assert.False((await Service().SignInWithPasswordAsync(username, password, default)).Succeeded);
    }

    [Fact]
    public async Task Deactivated_account_cannot_sign_in()
    {
        AddUser(active: false);
        Assert.False((await Service().SignInWithPasswordAsync("student.new", "Liwa#2027", default)).Succeeded);
    }

    [Fact]
    public async Task Sso_only_account_without_password_cannot_use_password_sign_in()
    {
        _users.Items.Add(new User(Guid.NewGuid(), "sso.user", "SSO User", null, UserRole.Student, null, Now));
        Assert.False((await Service().SignInWithPasswordAsync("sso.user", "anything", default)).Succeeded);
    }

    [Fact]
    public async Task Outdated_hash_is_upgraded_on_successful_sign_in()
    {
        var user = AddUser();
        var oldHash = user.PasswordHash;
        _hasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions { Iterations = 20_000 }));

        Assert.True((await Service().SignInWithPasswordAsync("student.new", "Liwa#2027", default)).Succeeded);

        Assert.NotEqual(oldHash, user.PasswordHash);
        Assert.Equal("20000", user.PasswordHash!.Split('$')[1]);
    }

    [Fact]
    public async Task Profile_of_deactivated_user_is_not_returned()
    {
        var user = AddUser(active: false);
        Assert.Null(await Service().GetProfileAsync(user.Id, default));
    }

    private sealed class InMemoryUsers : IUserRepository
    {
        public List<User> Items { get; } = [];

        public Task<User?> FindByUsernameAsync(string username, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(u => u.Username == username));

        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(u => u.Id == id));

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeTokens : ITokenIssuer
    {
        public AccessToken Issue(User user) => new("token-for-" + user.Id, Now.AddHours(8));
    }
}
