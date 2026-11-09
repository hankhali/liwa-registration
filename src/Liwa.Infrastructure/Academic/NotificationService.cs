using Liwa.Application.Planning;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Academic;

internal sealed class NotificationService(LiwaDbContext db, TimeProvider clock) : INotificationService
{
    public async Task<NotificationList> GetAsync(Guid userId, CancellationToken ct)
    {
        var items = await db.Notifications.AsNoTracking().Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).Take(20)
            .Select(n => new NotificationItem(n.Id, n.CreatedAt, n.MessageKey, n.Parameters, n.Link, n.ReadAt != null))
            .ToListAsync(ct);
        var unread = await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);
        return new NotificationList(unread, items);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        foreach (var n in await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ToListAsync(ct)) n.MarkRead(now);
        await db.SaveChangesAsync(ct);
    }
}
