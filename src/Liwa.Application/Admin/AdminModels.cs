using Liwa.Application.Imports;
using Liwa.Domain.Academic;
using Liwa.Domain.Users;

namespace Liwa.Application.Admin;

/// <summary>A catalog table in the import-file format: the same columns an export file has.</summary>
public sealed record CatalogRows(string Kind, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

/// <summary>
/// Rows edited on a Catalog screen. They are turned into a small CSV file and sent through the importer, so an edit
/// gets exactly the same checks, preview and catalog version as an uploaded file (brief §4/§11).
/// For degree plans send every row of the plan being edited (a plan is replaced as a whole).
/// </summary>
public sealed record RowEditRequest(IReadOnlyList<Dictionary<string, string?>> Rows, bool DryRun = true);

public sealed record PrerequisiteCheckRequest(string CourseCode, string? Expression);

/// <param name="Problems">Plain sentences: can't read the rule, unknown course, its own prerequisite, a cycle.</param>
public sealed record PrerequisiteCheckResult(bool Ok, string? Canonical, IReadOnlyList<string> Problems);

public enum RemovalOutcome
{
    Removed = 1,
    NotFound = 2,
    StillAssigned = 3,
}

public sealed record RemovalResult(RemovalOutcome Outcome, int StudentsAssigned, int? CatalogVersion);

public interface IAdminCatalogService
{
    Task<CatalogRows> RowsAsync(ImportKind kind, CancellationToken ct);

    Task<ImportReport> EditAsync(ImportKind kind, RowEditRequest request, Guid actorUserId, CancellationToken ct);

    /// <summary>One file in the import format; importing it again changes nothing (round trip).</summary>
    Task<string> ExportCsvAsync(ImportKind kind, CancellationToken ct);

    /// <summary>All seven files in a zip, in import order.</summary>
    Task<byte[]> ExportZipAsync(CancellationToken ct);

    Task<PrerequisiteCheckResult> CheckPrerequisiteAsync(PrerequisiteCheckRequest request, CancellationToken ct);

    /// <summary>Removes a degree plan; refused while students are assigned to it (brief §11).</summary>
    Task<RemovalResult> RemoveDegreePlanAsync(string programCode, int catalogYear, Guid actorUserId, CancellationToken ct);

    Task<IReadOnlyList<CatalogVersionView>> VersionsAsync(int take, CancellationToken ct);
}

// ------------------------------------------------------------------------- credit policy

public sealed record CreditRuleDto(string Name, TermSeason? Season, Standing? Standing, decimal? MinGpa, decimal? MaxGpaExclusive, bool? OnProbation,
    int MinCredits, int MaxCredits);

public sealed record CreditPolicyDto(RepeatGradeRule RepeatGradeRule, bool AllowInProgressPrerequisites, bool GraduatingStudentsExemptFromMinimum,
    IReadOnlyList<CreditRuleDto> Rules);

/// <summary>A student whose credit limits for the current term would change.</summary>
public sealed record CreditPolicyChange(string StudentId, string StudentName, string Standing, decimal? Gpa, bool OnProbation,
    string? BeforeRule, int? BeforeMin, int? BeforeMax, string? AfterRule, int? AfterMin, int? AfterMax);

public sealed record CreditPolicyPreview(string TermName, int StudentsChecked, IReadOnlyList<CreditPolicyChange> Changes, IReadOnlyList<string> Problems);

public interface ICreditPolicyAdmin
{
    Task<CreditPolicyDto?> GetAsync(CancellationToken ct);

    /// <summary>The credit-rule test: every student whose limits, standing or probation cap would change.</summary>
    Task<CreditPolicyPreview> PreviewAsync(CreditPolicyDto proposed, CancellationToken ct);

    Task<CreditPolicyPreview> SaveAsync(CreditPolicyDto proposed, Guid actorUserId, CancellationToken ct);
}

// ------------------------------------------------------------------------- users

public sealed record StaffUser(Guid Id, string Username, string DisplayName, string? Email, UserRole Role, bool IsActive, DateTimeOffset? LastSignInAt,
    int Advisees, bool HasPassword);

public sealed record CreateStaffRequest(string Username, string DisplayName, string? Email, UserRole Role, string Password);

public sealed record PasswordRequest(string Password);

public sealed record ActiveRequest(bool Active);

/// <param name="StudentIds">Only these students (university IDs); empty or null = all of the advisor's students.</param>
public sealed record ReassignRequest(Guid FromAdvisorUserId, Guid ToAdvisorUserId, IReadOnlyList<string>? StudentIds);

public sealed record ReassignResult(int Moved);

public enum UserAdminOutcome
{
    Done = 1,
    NotFound = 2,
    Invalid = 3,
    Conflict = 4,
}

public sealed record UserAdminResult(UserAdminOutcome Outcome, string? Problem = null, StaffUser? User = null);

public interface IStaffAdmin
{
    Task<IReadOnlyList<StaffUser>> ListAsync(CancellationToken ct);

    Task<UserAdminResult> CreateAsync(CreateStaffRequest request, Guid actorUserId, CancellationToken ct);

    Task<UserAdminResult> SetPasswordAsync(Guid userId, string password, Guid actorUserId, CancellationToken ct);

    Task<UserAdminResult> SetActiveAsync(Guid userId, bool active, Guid actorUserId, CancellationToken ct);

    Task<(UserAdminOutcome Outcome, string? Problem, ReassignResult? Result)> ReassignAsync(ReassignRequest request, Guid actorUserId, CancellationToken ct);
}

// ------------------------------------------------------------------------- settings

/// <param name="Effective">What the running API uses now (sign-in changes apply on the next restart).</param>
public sealed record SsoSettingsView(bool Enabled, string? TenantId, string? ClientId, string? ClientSecret, bool LocalSignInEnabled,
    bool EffectiveEnabled, bool EffectiveLocalSignIn, DateTimeOffset? LastTestOkAt);

/// <param name="ClientSecret">Null = keep the stored secret.</param>
/// <param name="ConfirmSsoOnly">Must be true to switch off username/password sign-in (never by accident).</param>
public sealed record SsoSettingsRequest(bool Enabled, string? TenantId, string? ClientId, string? ClientSecret, bool LocalSignInEnabled, bool ConfirmSsoOnly);

public sealed record AiSettingsView(bool Enabled, string Provider, string Model, string? BaseUrl, string? ApiKey, int TimeoutSeconds, int HourlyLimitPerUser);

/// <param name="ApiKey">Null = keep the stored key; empty string = remove it.</param>
public sealed record AiSettingsRequest(bool Enabled, string Provider, string Model, string? BaseUrl, string? ApiKey, int? TimeoutSeconds, int? HourlyLimitPerUser);

public sealed record ConnectionTestResult(bool Ok, string Message);

/// <summary>Raw AI settings for the explainer (secret decrypted). Never sent to the browser.</summary>
public sealed record AiProviderConfig(bool Enabled, string Provider, string Model, string? BaseUrl, string? ApiKey, int TimeoutSeconds, int HourlyLimitPerUser);

public interface ISettingsService
{
    Task<SsoSettingsView> GetSsoAsync(CancellationToken ct);

    Task<(bool Ok, string? Problem)> SaveSsoAsync(SsoSettingsRequest request, Guid actorUserId, CancellationToken ct);

    /// <summary>Checks the tenant's sign-in metadata can be read (no user data is sent).</summary>
    Task<ConnectionTestResult> TestSsoAsync(CancellationToken ct);

    Task<AiSettingsView> GetAiAsync(CancellationToken ct);

    Task<(bool Ok, string? Problem)> SaveAiAsync(AiSettingsRequest request, Guid actorUserId, CancellationToken ct);

    Task<AiProviderConfig> GetAiProviderAsync(CancellationToken ct);
}
