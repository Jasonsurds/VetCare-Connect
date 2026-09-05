using System.Security.Claims;
using VetCare.Data;
using VetCare.Models;

namespace VetCare.Services;

public interface IAuditService
{
    Task LogAsync(string action, string entityName, string details, string? userNameOverride = null);
}

public class AuditService : IAuditService
{
    private readonly VetCareDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(VetCareDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(string action, string entityName, string details, string? userNameOverride = null)
    {
        var user = _http.HttpContext?.User;
        int? userId = null;
        var userName = "Anonymous";

        if (user?.Identity?.IsAuthenticated == true)
        {
            var idRaw = user.FindFirstValue(ClaimTypes.NameIdentifier);
            userId = int.TryParse(idRaw, out var id) ? id : null;
            userName = user.Identity?.Name ?? "Unknown";
        }

        if (!string.IsNullOrWhiteSpace(userNameOverride))
            userName = userNameOverride;

        _db.AuditLogs.Add(new AuditLog
        {
            UserID = userId,
            UserName = userName,
            Action = action,
            EntityName = entityName,
            Details = details,
            Timestamp = DateTime.Now
        });
        await _db.SaveChangesAsync();
    }
}
