using Liwa.Application.Abstractions;
using Liwa.Domain.Users;
using Liwa.Infrastructure.Security;

namespace Liwa.Api.Infrastructure;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirst(LiwaClaims.Subject)?.Value, out var id)
            ? id
            : throw new InvalidOperationException("No authenticated user.");

    public UserRole Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirst(LiwaClaims.Role)?.Value, out var role)
            ? role
            : throw new InvalidOperationException("No authenticated user.");
}
