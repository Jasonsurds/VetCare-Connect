using VetCare.Models;

namespace VetCare.Services;

public interface INotificationService
{
    Task SendAsync(int userId, string title, string message, string category = "General", string? actionUrl = null);
    Task SendToRoleAsync(string role, string title, string message, string category = "General", string? actionUrl = null);
    Task<int> GetUnreadCountAsync(int userId);
    Task<List<Notification>> GetUserNotificationsAsync(int userId, int limit = 15);
    Task<bool> MarkAsReadAsync(int notificationId, int userId);
    Task<int> MarkAllAsReadAsync(int userId);
}
