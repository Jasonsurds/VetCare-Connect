using Microsoft.EntityFrameworkCore;
using VetCare.Data;
using VetCare.Models;

namespace VetCare.Services;

public class NotificationService : INotificationService
{
    private readonly VetCareDbContext _db;

    public NotificationService(VetCareDbContext db)
    {
        _db = db;
    }

    public async Task SendAsync(int userId, string title, string message, string category = "General", string? actionUrl = null)
    {
        var notif = new Notification
        {
            UserID = userId,
            Title = title,
            Message = message,
            Category = category,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.Now
        };

        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();
    }

    public async Task SendToRoleAsync(string role, string title, string message, string category = "General", string? actionUrl = null)
    {
        var users = await _db.Users
            .Where(u => u.Role == role && u.IsActive)
            .Select(u => u.UserID)
            .ToListAsync();

        if (users.Count == 0) return;

        var notifs = users.Select(uid => new Notification
        {
            UserID = uid,
            Title = title,
            Message = message,
            Category = category,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.Now
        }).ToList();

        _db.Notifications.AddRange(notifs);
        await _db.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _db.Notifications
            .CountAsync(n => n.UserID == userId && !n.IsRead);
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(int userId, int limit = 15)
    {
        return await _db.Notifications
            .Where(n => n.UserID == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
    {
        var notif = await _db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationID == notificationId && n.UserID == userId);

        if (notif == null) return false;

        notif.IsRead = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(int userId)
    {
        var unread = await _db.Notifications
            .Where(n => n.UserID == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
        }

        await _db.SaveChangesAsync();
        return unread.Count;
    }
}
