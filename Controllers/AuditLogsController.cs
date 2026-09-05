using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetCare.Data;

namespace VetCare.Controllers;

[Authorize(Roles = "Administrator")]
public class AuditLogsController : Controller
{
    private readonly VetCareDbContext _db;

    public AuditLogsController(VetCareDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? actionType, int page = 1)
    {
        const int pageSize = 25;
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(actionType) && actionType != "All")
            query = query.Where(a => a.Action == actionType);

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.ActionType = actionType;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.Total = total;
        ViewData["Title"] = "Audit Logs";
        ViewData["DashTitle"] = "Security Audit Logs";
        return View(logs);
    }
}
