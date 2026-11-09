using Liwa.Domain.Planning;

namespace Liwa.Application.Planning;

/// <summary>Loads a student's data and runs the pure <see cref="Planner"/> for the current term.</summary>
public interface IRecommendationService
{
    /// <summary>Null when the student has no record, no degree plan, or there is no current term.</summary>
    Task<PlannerResult?> RecommendAsync(Guid studentUserId, Availability? availability, PlannerPreferences preferences, CancellationToken ct);
}
