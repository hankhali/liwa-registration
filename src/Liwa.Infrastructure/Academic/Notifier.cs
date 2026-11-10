using System.Text.Json;
using System.Text.RegularExpressions;
using Liwa.Domain.Activity;
using Liwa.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Liwa.Infrastructure.Academic;

/// <summary>
/// Creates in-app notifications and, until a mail server exists, writes an email stub to the log in the
/// recipient's language (brief §4). The stub carries the user id, never the email address.
/// </summary>
internal sealed partial class Notifier(LiwaDbContext db, TimeProvider clock, ILogger<Notifier> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Adds a notification (saved with the caller's SaveChanges) and logs the email stub.</summary>
    public void Notify(Guid userId, string messageKey, object parameters, string? link)
    {
        var json = JsonSerializer.Serialize(parameters, Json);
        db.Notifications.Add(new Notification(Guid.NewGuid(), userId, clock.GetUtcNow(), messageKey, json, link));

        var language = db.Users.Where(u => u.Id == userId).Select(u => u.PreferredLanguage).SingleOrDefault() ?? "en";
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, Json) ?? [];
        var subject = EmailTemplates.Render(messageKey, language, values);
        logger.LogInformation("Email stub (no mail server configured) to user {UserId} [{Language}]: {Subject} {Link}", userId, language, subject, link);
    }
}

/// <summary>Email subjects for each notification key, in English and Arabic (same wording as the app).</summary>
public static partial class EmailTemplates
{
    private static readonly Dictionary<string, (string En, string Ar)> Templates = new(StringComparer.Ordinal)
    {
        ["notifications.planSubmitted"] = ("{student} sent a plan for {term}.", "أرسل {student} خطة لفصل {term}."),
        ["notifications.planReviewing"] = ("{advisor} is reviewing your plan.", "{advisor} يراجع خطتك الآن."),
        ["notifications.planApproved"] = ("{advisor} approved your plan.", "اعتمد {advisor} خطتك."),
        ["notifications.planRejected"] = ("{advisor} sent your plan back with a comment.", "أعاد {advisor} خطتك مع تعليق."),
        ["notifications.planChangesProposed"] = ("{advisor} suggested changes to your plan.", "اقترح {advisor} تعديلات على خطتك."),
        ["notifications.planAccepted"] = ("{student} accepted your suggested changes for {term}.", "قبل {student} التعديلات المقترحة لفصل {term}."),
        ["notifications.planNeedsReviewStudent"] = ("New grades were posted: your approved plan for {term} needs another look with your advisor.", "رُصدت درجات جديدة: خطتك المعتمدة لفصل {term} تحتاج إلى مراجعة مع مرشدك."),
        ["notifications.planNeedsReviewAdvisor"] = ("New grades were posted: {student}'s approved plan for {term} needs review.", "رُصدت درجات جديدة: خطة {student} المعتمدة لفصل {term} تحتاج إلى مراجعة."),
        ["notifications.you.submitted"] = ("You sent your plan for {term} to your advisor.", "أرسلت خطتك لفصل {term} إلى مرشدك."),
        ["notifications.you.reviewing"] = ("You started reviewing {student}'s plan.", "بدأت مراجعة خطة {student}."),
        ["notifications.you.approved"] = ("You approved {student}'s plan.", "اعتمدت خطة {student}."),
        ["notifications.you.rejected"] = ("You sent {student}'s plan back.", "أعدت خطة {student}."),
        ["notifications.you.proposed"] = ("You suggested changes to {student}'s plan.", "اقترحت تعديلات على خطة {student}."),
        ["notifications.you.accepted"] = ("You accepted your advisor's changes. Your plan for {term} is approved.", "قبلت تعديلات مرشدك. خطتك لفصل {term} معتمدة."),
    };

    public static IReadOnlyCollection<string> Keys => Templates.Keys;

    public static string Render(string key, string language, IReadOnlyDictionary<string, JsonElement> values)
    {
        if (!Templates.TryGetValue(key, out var t)) return key;
        var text = language == "ar" ? t.Ar : t.En;
        return Placeholder().Replace(text, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v.ToString() : m.Value);
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}
