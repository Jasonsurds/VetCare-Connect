using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.Helpers;
using VetCare.Services;

namespace VetCare.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? filter = "all")
    {
        var userId = User.GetUserId();
        var allNotifications = await _notificationService.GetUserNotificationsAsync(userId, 50);

        var filtered = filter?.ToLowerInvariant() switch
        {
            "unread" => allNotifications.Where(n => !n.IsRead).ToList(),
            "appointment" or "appointments" => allNotifications.Where(n => n.Category == "Appointment").ToList(),
            "billing" or "invoices" => allNotifications.Where(n => n.Category == "Billing").ToList(),
            "reminder" or "reminders" => allNotifications.Where(n => n.Category == "Reminder").ToList(),
            _ => allNotifications
        };

        ViewBag.Filter = filter ?? "all";
        ViewBag.UnreadCount = await _notificationService.GetUnreadCountAsync(userId);
        ViewData["Title"] = "Notifications";
        ViewData["DashTitle"] = "Notification Center";
        return View(filtered);
    }

    [HttpGet]
    public async Task<IActionResult> GetLatest()
    {
        var userId = User.GetUserId();
        if (userId == 0) return Unauthorized();

        var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, 10);

        var list = notifications.Select(n => new
        {
            id = n.NotificationID,
            title = n.Title,
            message = n.Message,
            category = n.Category,
            actionUrl = n.ActionUrl ?? "#",
            isRead = n.IsRead,
            createdAt = n.CreatedAt.ToString("MMM dd, yyyy h:mm tt"),
            timeAgo = GetTimeAgo(n.CreatedAt)
        });

        return Json(new { unreadCount, notifications = list });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, string? returnUrl = null)
    {
        var userId = User.GetUserId();
        await _notificationService.MarkAsReadAsync(id, userId);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(string? returnUrl = null)
    {
        var userId = User.GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    private static string GetTimeAgo(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dt.ToString("MMM dd");
    }
}
