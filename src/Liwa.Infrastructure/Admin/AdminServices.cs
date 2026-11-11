using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Liwa.Application.Abstractions;
using Liwa.Application.Admin;
using Liwa.Domain.Academic;
using Liwa.Domain.Activity;
using Liwa.Domain.Users;
using Liwa.Infrastructure.Academic;
using Liwa.Infrastructure.Persistence;
using Liwa.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Liwa.Infrastructure.Admin;

/// <summary>Credit rules with the "who would change" test (brief §11).</summary>
internal sealed class CreditPolicyAdmin(LiwaDbContext db, TimeProvider clock) : ICreditPolicyAdmin
{
    public async Task<CreditPolicyDto?> GetAsync(CancellationToken ct)
    {
        var p = await db.CreditPolicies.AsNoTracking().Include(x => x.Rules).FirstOrDefaultAsync(ct);
        return p is null ? null : ToDto(p);
    }

    public async Task<CreditPolicyPreview> PreviewAsync(CreditPolicyDto proposed, CancellationToken ct)
    {
        var problems = Validate(proposed);
        var term = await db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.IsCurrent, ct);
        var current = await db.CreditPolicies.AsNoTracking().Include(x => x.Rules).FirstOrDefaultAsync(ct);
        if (problems.Count > 0 || term is null) return new CreditPolicyPreview(term?.Name ?? "", 0, [], problems);

        var next = Build(proposed);
        var records = await db.StudentRecords.AsNoTracking().Include(r => r.Transcript).Include(r => r.TransferCredits).ToListAsync(ct);
        var programs = await db.Programs.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.TotalCredits, ct);
        var names = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Student).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var changes = new List<CreditPolicyChange>();
        foreach (var r in records.OrderBy(r => r.StudentId, StringComparer.Ordinal))
        {
            var before = Limits(current, r, term.Season, programs);
            var after = Limits(next, r, term.Season, programs);
            if (before?.MinCredits == after?.MinCredits && before?.MaxCredits == after?.MaxCredits && before?.RuleName == after?.RuleName) continue;
            var s = AcademicSummary.From(r, next.RepeatGradeRule);
            changes.Add(new CreditPolicyChange(r.StudentId, names.GetValueOrDefault(r.UserId, r.StudentId), s.Standing.ToString(), s.Gpa,
                r.Status == AcademicStatus.Probation, before?.RuleName, before?.MinCredits, before?.MaxCredits, after?.RuleName, after?.MinCredits, after?.MaxCredits));
        }

        return new CreditPolicyPreview(term.Name, records.Count, changes, []);
    }

    public async Task<CreditPolicyPreview> SaveAsync(CreditPolicyDto proposed, Guid actorUserId, CancellationToken ct)
    {
        var preview = await PreviewAsync(proposed, ct);
        if (preview.Problems.Count > 0) return preview;

        var policy = await db.CreditPolicies.Include(x => x.Rules).FirstOrDefaultAsync(ct);
        var before = policy is null ? null : JsonSerializer.Serialize(ToDto(policy));
        if (policy is null)
        {
            policy = new CreditPolicy(Guid.NewGuid(), "Credit policy");
            db.CreditPolicies.Add(policy);
        }

        db.RemoveRange(policy.Rules);
        policy.SetSwitches(proposed.RepeatGradeRule, proposed.AllowInProgressPrerequisites, proposed.GraduatingStudentsExemptFromMinimum);
        policy.SetNormalLoad(proposed.NormalLoad);
        policy.ReplaceRules(NewRules(proposed));
        foreach (var rule in policy.Rules) db.Entry(rule).State = EntityState.Added;
        db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actorUserId, "creditPolicy.saved", "CreditPolicy", policy.Id.ToString(),
            before, JsonSerializer.Serialize(proposed)));
        await db.SaveChangesAsync(ct);
        return preview;
    }

    private static CreditLimits? Limits(CreditPolicy? policy, StudentRecord r, TermSeason season, Dictionary<Guid, int> totals)
    {
        if (policy is null) return null;
        var s = AcademicSummary.From(r, policy.RepeatGradeRule);
        return policy.LimitsFor(season, s.Standing, s.Gpa, r.Status == AcademicStatus.Probation, totals.GetValueOrDefault(r.ProgramId) - s.CreditsEarned);
    }

    private static List<string> Validate(CreditPolicyDto p)
    {
        var problems = new List<string>();
        if (p.Rules.Count == 0) problems.Add("Add at least one rule, otherwise no student has a credit limit.");
        if (p.NormalLoad is < 0 or > 30) problems.Add("The normal load must be between 0 and 30 credits.");
        foreach (var (r, i) in p.Rules.Select((r, i) => (r, i + 1)))
        {
            if (r.MinCredits < 0 || r.MaxCredits < r.MinCredits) problems.Add($"Rule {i} ({r.Name}): the minimum must be between 0 and the maximum.");
            if (r.MaxCredits > 30) problems.Add($"Rule {i} ({r.Name}): a maximum above 30 credits isn't allowed.");
            if (r.NormalLoad is < 0 or > 30) problems.Add($"Rule {i} ({r.Name}): the normal load must be between 0 and 30 credits.");
            if (r.MinGpa is < 0 or > 4 || r.MaxGpaExclusive is < 0 or > 5) problems.Add($"Rule {i} ({r.Name}): GPA bounds must be between 0 and 4.");
        }

        // A rule with no conditions catches everyone; anything after it can never apply.
        var catchAll = p.Rules.Select((r, i) => (r, i)).FirstOrDefault(x => x.r.Season is null && x.r.Standing is null && x.r.MinGpa is null && x.r.MaxGpaExclusive is null && x.r.OnProbation is null);
        if (catchAll.r is not null && catchAll.i < p.Rules.Count - 1)
            problems.Add($"Rule {catchAll.i + 1} ({catchAll.r.Name}) matches every student, so the rules after it would never be used. Move it to the end.");
        return problems;
    }

    private static IEnumerable<CreditLimitRule> NewRules(CreditPolicyDto p) => p.Rules.Select((r, i) =>
        new CreditLimitRule(Guid.NewGuid(), i + 1, r.Name, r.MinCredits, r.MaxCredits).When(r.Season, r.Standing, r.MinGpa, r.MaxGpaExclusive, r.OnProbation).WithNormalLoad(r.NormalLoad));

    private static CreditPolicy Build(CreditPolicyDto p)
    {
        var policy = new CreditPolicy(Guid.NewGuid(), "Proposed");
        policy.SetSwitches(p.RepeatGradeRule, p.AllowInProgressPrerequisites, p.GraduatingStudentsExemptFromMinimum);
        policy.SetNormalLoad(p.NormalLoad);
        policy.ReplaceRules(NewRules(p));
        return policy;
    }

    private static CreditPolicyDto ToDto(CreditPolicy p) => new(p.RepeatGradeRule, p.AllowInProgressPrerequisites, p.GraduatingStudentsExemptFromMinimum,
        p.Rules.OrderBy(r => r.Priority).Select(r => new CreditRuleDto(r.Name, r.Season, r.Standing, r.MinGpa, r.MaxGpaExclusive, r.OnProbation, r.MinCredits, r.MaxCredits, r.NormalLoad)).ToList(),
        p.NormalLoad);
}

/// <summary>Staff accounts and bulk advisor reassignment (brief §11).</summary>
internal sealed partial class StaffAdmin(LiwaDbContext db, IPasswordHasher hasher, Notifier notifier, TimeProvider clock) : IStaffAdmin
{
    public const int MinPasswordLength = 10;

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{2,49}$")]
    private static partial Regex UsernamePattern();

    public async Task<IReadOnlyList<StaffUser>> ListAsync(CancellationToken ct)
    {
        var counts = await db.StudentRecords.AsNoTracking().Where(r => r.AdvisorUserId != null)
            .GroupBy(r => r.AdvisorUserId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return (await db.Users.AsNoTracking().Where(u => u.Role != UserRole.Student).OrderBy(u => u.Role).ThenBy(u => u.DisplayName).ToListAsync(ct))
            .Select(u => ToView(u, counts.GetValueOrDefault(u.Id))).ToList();
    }

    private static StaffUser ToView(User u, int advisees) =>
        new(u.Id, u.Username, u.DisplayName, u.Email, u.Role, u.IsActive, u.LastSignInAt, advisees, u.PasswordHash is not null);

    public async Task<UserAdminResult> CreateAsync(CreateStaffRequest r, Guid actorUserId, CancellationToken ct)
    {
        var username = User.NormalizeUsername(r.Username ?? "");
        if (!UsernamePattern().IsMatch(username)) return new(UserAdminOutcome.Invalid, "Usernames are 3–50 lower-case letters, digits, dots, dashes or underscores.");
        if (r.Role is not (UserRole.Advisor or UserRole.Admin)) return new(UserAdminOutcome.Invalid, "Staff accounts are advisors or administrators. Students come from the student import.");
        if (string.IsNullOrWhiteSpace(r.DisplayName)) return new(UserAdminOutcome.Invalid, "Enter the person's name.");
        if ((r.Password ?? "").Length < MinPasswordLength) return new(UserAdminOutcome.Invalid, $"Passwords need at least {MinPasswordLength} characters.");
        var email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Username == username, ct)) return new(UserAdminOutcome.Conflict, $"The username {username} is already taken.");
        if (email is not null && await db.Users.AnyAsync(u => u.Email == email, ct)) return new(UserAdminOutcome.Conflict, $"{email} is already used by another account.");

        var user = new User(Guid.NewGuid(), username, r.DisplayName, email, r.Role, null, clock.GetUtcNow());
        user.SetPasswordHash(hasher.Hash(r.Password!));
        db.Users.Add(user);
        db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actorUserId, "user.created", "User", user.Id.ToString(), null,
            JsonSerializer.Serialize(new { username, role = r.Role.ToString() })));
        await db.SaveChangesAsync(ct);
        return new(UserAdminOutcome.Done, null, ToView(user, 0));
    }

    public async Task<UserAdminResult> SetPasswordAsync(Guid userId, string password, Guid actorUserId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || user.Role == UserRole.Student) return new(UserAdminOutcome.NotFound);
        if ((password ?? "").Length < MinPasswordLength) return new(UserAdminOutcome.Invalid, $"Passwords need at least {MinPasswordLength} characters.");
        user.SetPasswordHash(hasher.Hash(password!));
        db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actorUserId, "user.passwordSet", "User", user.Id.ToString(), null, null));
        await db.SaveChangesAsync(ct);
        return new(UserAdminOutcome.Done);
    }

    public async Task<UserAdminResult> SetActiveAsync(Guid userId, bool active, Guid actorUserId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || user.Role == UserRole.Student) return new(UserAdminOutcome.NotFound);
        if (!active && user.Id == actorUserId) return new(UserAdminOutcome.Invalid, "You can't switch off your own account.");
        if (!active && user.Role == UserRole.Admin && await db.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive, ct) <= 1)
            return new(UserAdminOutcome.Invalid, "This is the last administrator account; it has to stay on.");
        if (active) user.Activate();
        else user.Deactivate();
        db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actorUserId, active ? "user.activated" : "user.deactivated", "User", user.Id.ToString(), null, null));
        await db.SaveChangesAsync(ct);
        return new(UserAdminOutcome.Done);
    }

    public async Task<(UserAdminOutcome Outcome, string? Problem, ReassignResult? Result)> ReassignAsync(ReassignRequest r, Guid actorUserId, CancellationToken ct)
    {
        if (r.FromAdvisorUserId == r.ToAdvisorUserId) return (UserAdminOutcome.Invalid, "Choose two different advisors.", null);
        var from = await db.Users.SingleOrDefaultAsync(u => u.Id == r.FromAdvisorUserId && u.Role == UserRole.Advisor, ct);
        var to = await db.Users.SingleOrDefaultAsync(u => u.Id == r.ToAdvisorUserId && u.Role == UserRole.Advisor, ct);
        if (from is null || to is null) return (UserAdminOutcome.NotFound, "Both people must be advisors.", null);
        if (!to.IsActive) return (UserAdminOutcome.Invalid, $"{to.DisplayName}'s account is switched off.", null);

        var query = db.StudentRecords.Where(s => s.AdvisorUserId == from.Id);
        if (r.StudentIds is { Count: > 0 } ids) query = query.Where(s => ids.Contains(s.StudentId));
        var records = await query.ToListAsync(ct);
        foreach (var s in records)
        {
            s.Update(s.ProgramId, s.CatalogYear, s.AdmitTermCode, s.Status, to.Id);
            notifier.Notify(s.UserId, "notifications.advisorChanged", new { advisor = to.DisplayName }, "/student");
        }

        if (records.Count > 0)
        {
            var n = records.Count.ToString(CultureInfo.InvariantCulture);
            notifier.Notify(to.Id, "notifications.adviseesAssigned", new { n, advisor = from.DisplayName }, "/advisor/students");
            notifier.Notify(from.Id, "notifications.adviseesMoved", new { n, advisor = to.DisplayName }, "/advisor/students");
            db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actorUserId, "advisees.reassigned", "User", from.Id.ToString(),
                JsonSerializer.Serialize(new { advisor = from.Username }),
                JsonSerializer.Serialize(new { advisor = to.Username, students = records.Select(x => x.StudentId) })));
        }

        await db.SaveChangesAsync(ct);
        return (UserAdminOutcome.Done, null, new ReassignResult(records.Count));
    }
}

/// <summary>
/// Sign-in and AI-provider settings (brief §11), stored in app_settings with secrets encrypted. Sign-in settings are
/// applied when the API starts (SettingsBootstrap), so a change never locks anyone out mid-session, and
/// username/password sign-in can only be switched off explicitly, with SSO on and its last test passed.
/// </summary>
internal sealed class SettingsService(LiwaDbContext db, SecretProtector protector, IConfiguration config, IHostEnvironment env,
    IHttpClientFactory http, TimeProvider clock) : ISettingsService
{
    public const string DefaultModel = "claude-sonnet-5-5";

    public static readonly IReadOnlySet<string> Providers = new HashSet<string>(StringComparer.Ordinal) { "anthropic", "openai-compatible", "mock" };

    private async Task<Dictionary<string, AppSetting>> AllAsync(CancellationToken ct) =>
        await db.Settings.ToDictionaryAsync(s => s.Key, StringComparer.Ordinal, ct);

    private static string? Get(Dictionary<string, AppSetting> all, string key) => all.TryGetValue(key, out var s) ? s.Value : null;

    private string? Secret(Dictionary<string, AppSetting> all, string key)
    {
        var v = Get(all, key);
        if (string.IsNullOrEmpty(v)) return null;
        try { return protector.Unprotect(v); }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }

    private void Put(Dictionary<string, AppSetting> all, string key, string? value, bool secret, Guid? actor)
    {
        var stored = secret && !string.IsNullOrEmpty(value) ? protector.Protect(value) : value;
        if (all.TryGetValue(key, out var s)) s.Set(stored, secret, clock.GetUtcNow(), actor);
        else
        {
            s = new AppSetting(key, stored, secret, clock.GetUtcNow(), actor);
            db.Settings.Add(s);
            all[key] = s;
        }
    }

    // ------------------------------------------------------------------ SSO

    public async Task<SsoSettingsView> GetSsoAsync(CancellationToken ct)
    {
        var all = await AllAsync(ct);
        var enabled = bool.TryParse(Get(all, "sso.enabled"), out var e) ? e : config.GetValue("Sso:Enabled", false);
        var local = bool.TryParse(Get(all, "sso.localSignIn"), out var l) ? l : config.GetValue("Sso:LocalSignInEnabled", true);
        DateTimeOffset? lastOk = DateTimeOffset.TryParse(Get(all, "sso.lastTestOk"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;
        return new SsoSettingsView(enabled, Get(all, "sso.tenantId") ?? config["Sso:TenantId"], Get(all, "sso.clientId") ?? config["Sso:ClientId"],
            SecretProtector.Mask(Secret(all, "sso.clientSecret") ?? config["Sso:ClientSecret"]), local,
            config.GetValue("Sso:Enabled", false), config.GetValue("Sso:LocalSignInEnabled", true), lastOk);
    }

    public async Task<(bool Ok, string? Problem)> SaveSsoAsync(SsoSettingsRequest r, Guid actorUserId, CancellationToken ct)
    {
        var all = await AllAsync(ct);
        var tenant = r.TenantId?.Trim();
        var client = r.ClientId?.Trim();
        var secret = r.ClientSecret is null ? Secret(all, "sso.clientSecret") : r.ClientSecret.Trim();
        if (r.Enabled && (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(client) || string.IsNullOrEmpty(secret)))
            return (false, "To switch on university sign-in, enter the tenant ID, the client ID and the client secret.");
        if (!string.IsNullOrEmpty(tenant) && !Regex.IsMatch(tenant, @"^[A-Za-z0-9.\-]{3,100}$"))
            return (false, "The tenant ID should be a GUID or a domain such as lu.ac.ae.");

        if (!r.LocalSignInEnabled)
        {
            // Never lock students or admins out: SSO-only needs SSO on, a passing test, and an explicit confirmation.
            var lastOk = Get(all, "sso.lastTestOk");
            if (!r.Enabled) return (false, "Username and password sign-in can only be switched off when university sign-in is on.");
            if (lastOk is null) return (false, "Test the university sign-in connection first; it has to pass before username and password sign-in can be switched off.");
            if (!r.ConfirmSsoOnly) return (false, "Confirm that everyone will sign in with their university account only.");
        }

        Put(all, "sso.enabled", r.Enabled.ToString(), false, actorUserId);
        Put(all, "sso.tenantId", tenant, false, actorUserId);
        Put(all, "sso.clientId", client, false, actorUserId);
        if (r.ClientSecret is not null) Put(all, "sso.clientSecret", secret, true, actorUserId);
        Put(all, "sso.localSignIn", r.LocalSignInEnabled.ToString(), false, actorUserId);
        db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actorUserId, "settings.sso", "Settings", "sso", null,
            JsonSerializer.Serialize(new { r.Enabled, tenant, client, secret = SecretProtector.Mask(secret), r.LocalSignInEnabled })));
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<ConnectionTestResult> TestSsoAsync(CancellationToken ct)
    {
        var all = await AllAsync(ct);
        var tenant = Get(all, "sso.tenantId") ?? config["Sso:TenantId"];
        if (string.IsNullOrWhiteSpace(tenant)) return new(false, "Enter and save the tenant ID first.");
        try
        {
            using var client = http.CreateClient("connection-test");
            client.Timeout = TimeSpan.FromSeconds(10);
            using var res = await client.GetAsync(new Uri($"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/v2.0/.well-known/openid-configuration"), ct);
            if (!res.IsSuccessStatusCode) return new(false, $"Microsoft didn't recognise this tenant (HTTP {(int)res.StatusCode}). Check the tenant ID.");
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("authorization_endpoint", out _)) return new(false, "The sign-in metadata looks wrong.");
            Put(all, "sso.lastTestOk", clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), false, null);
            await db.SaveChangesAsync(ct);
            return new(true, "Microsoft sign-in for this tenant is reachable. Changes apply when the service restarts.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(false, "Couldn't reach Microsoft sign-in from the server. Check the network and the tenant ID.");
        }
    }

    // ------------------------------------------------------------------ AI provider

    public async Task<AiSettingsView> GetAiAsync(CancellationToken ct)
    {
        var p = await GetAiProviderAsync(ct);
        return new AiSettingsView(p.Enabled, p.Provider, p.Model, p.BaseUrl, SecretProtector.Mask(p.ApiKey), p.TimeoutSeconds, p.HourlyLimitPerUser);
    }

    public async Task<AiProviderConfig> GetAiProviderAsync(CancellationToken ct)
    {
        var all = await AllAsync(ct);
        return new AiProviderConfig(
            bool.TryParse(Get(all, "ai.enabled"), out var e) ? e : config.GetValue("Ai:Enabled", false),
            Get(all, "ai.provider") ?? config["Ai:Provider"] ?? "anthropic",
            Get(all, "ai.model") ?? config["Ai:Model"] ?? DefaultModel,
            Get(all, "ai.baseUrl") ?? config["Ai:BaseUrl"],
            Secret(all, "ai.apiKey") ?? config["Ai:ApiKey"],
            int.TryParse(Get(all, "ai.timeout"), out var t) ? t : config.GetValue("Ai:TimeoutSeconds", 20),
            int.TryParse(Get(all, "ai.hourlyLimit"), out var h) ? h : config.GetValue("Ai:HourlyLimitPerUser", 20));
    }

    public async Task<(bool Ok, string? Problem)> SaveAiAsync(AiSettingsRequest r, Guid actorUserId, CancellationToken ct)
    {
        var provider = (r.Provider ?? "").Trim().ToLowerInvariant();
        if (!Providers.Contains(provider)) return (false, "Choose a provider: anthropic, openai-compatible or mock.");
        if (provider == "mock" && !env.IsDevelopment()) return (false, "The mock provider is only for demos on a development machine.");
        if (string.IsNullOrWhiteSpace(r.Model)) return (false, "Enter a model name.");
        if (provider == "openai-compatible" && !Uri.TryCreate(r.BaseUrl, UriKind.Absolute, out _)) return (false, "OpenAI-compatible endpoints need a base URL.");
        if (r.BaseUrl is { Length: > 0 } u && (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) && provider != "mock")
            return (false, "The base URL must start with https://.");
        if (r.TimeoutSeconds is < 5 or > 120) return (false, "The timeout must be between 5 and 120 seconds.");
        if (r.HourlyLimitPerUser is < 1 or > 500) return (false, "The hourly limit must be between 1 and 500 questions per person.");

        var all = await AllAsync(ct);
        Put(all, "ai.enabled", r.Enabled.ToString(), false, actorUserId);
        Put(all, "ai.provider", provider, false, actorUserId);
        Put(all, "ai.model", r.Model.Trim(), false, actorUserId);
        Put(all, "ai.baseUrl", string.IsNullOrWhiteSpace(r.BaseUrl) ? null : r.BaseUrl.Trim(), false, actorUserId);
        if (r.ApiKey is not null) Put(all, "ai.apiKey", r.ApiKey.Length == 0 ? null : r.ApiKey.Trim(), true, actorUserId);
        if (r.TimeoutSeconds is { } t) Put(all, "ai.timeout", t.ToString(CultureInfo.InvariantCulture), false, actorUserId);
        if (r.HourlyLimitPerUser is { } h) Put(all, "ai.hourlyLimit", h.ToString(CultureInfo.InvariantCulture), false, actorUserId);
        db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actorUserId, "settings.ai", "Settings", "ai", null,
            JsonSerializer.Serialize(new { r.Enabled, provider, r.Model, r.BaseUrl, apiKeyChanged = r.ApiKey is not null })));
        await db.SaveChangesAsync(ct);
        return (true, null);
    }
}
