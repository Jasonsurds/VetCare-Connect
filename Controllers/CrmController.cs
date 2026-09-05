using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VetCare.Data;
using VetCare.Helpers;
using VetCare.Models;
using VetCare.Services;

namespace VetCare.Controllers;

[Authorize]
public class CrmController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;

    public CrmController(VetCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.CrmRecords.Include(c => c.Owner).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Owner!.Name.Contains(search) || (c.Interaction != null && c.Interaction.Contains(search)));

        var records = await query.OrderByDescending(c => c.InteractionDate).ToListAsync();

        var totals = await _db.CrmRecords
            .GroupBy(c => new { c.OwnerID, c.Owner!.Name })
            .Select(g => new LoyaltyLeaderboardRow { OwnerID = g.Key.OwnerID, OwnerName = g.Key.Name, Points = g.Sum(x => x.LoyaltyPoints) })
            .OrderByDescending(x => x.Points)
            .Take(5)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Leaderboard = totals;
        ViewData["Title"] = "Customer CRM";
        ViewData["DashTitle"] = "Customer Relationship Management";
        return View(records);
    }

    [Authorize(Roles = "Pet Owner")]
    public async Task<IActionResult> My()
    {
        var ownerId = User.GetUserId();
        var records = await _db.CrmRecords
            .Where(c => c.OwnerID == ownerId)
            .OrderByDescending(c => c.InteractionDate)
            .ToListAsync();

        ViewBag.TotalPoints = records.Sum(r => r.LoyaltyPoints);
        ViewData["Title"] = "Loyalty & Feedback";
        ViewData["DashTitle"] = "Loyalty & Feedback";
        return View("My", records);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Pet Owner")]
    public async Task<IActionResult> SubmitFeedback(string feedback)
    {
        if (!string.IsNullOrWhiteSpace(feedback))
        {
            _db.CrmRecords.Add(new CrmRecord
            {
                OwnerID = User.GetUserId(),
                Feedback = feedback.Trim(),
                LoyaltyPoints = 0,
                InteractionDate = DateTime.Now
            });
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Create", "CRM", $"Feedback submitted by owner (ID {User.GetUserId()}).");
            TempData["SuccessMessage"] = "Thank you for your feedback! We read every single one. 🐾";
        }
        return RedirectToAction(nameof(My));
    }

    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Create()
    {
        await PopulateOwnerDropdownAsync();
        ViewData["DashTitle"] = "Log CRM Interaction";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Create(int ownerId, string? interaction, string? feedback, int loyaltyPoints)
    {
        if (ownerId <= 0)
            ModelState.AddModelError(string.Empty, "Please select a pet owner.");
        if (string.IsNullOrWhiteSpace(interaction) && string.IsNullOrWhiteSpace(feedback))
            ModelState.AddModelError(string.Empty, "Provide an interaction note, customer feedback, or both.");
        if (loyaltyPoints < 0)
            ModelState.AddModelError(string.Empty, "Loyalty points cannot be negative.");

        if (!ModelState.IsValid)
        {
            await PopulateOwnerDropdownAsync(ownerId);
            return View();
        }

        var record = new CrmRecord
        {
            OwnerID = ownerId,
            Interaction = interaction,
            Feedback = feedback,
            LoyaltyPoints = loyaltyPoints,
            InteractionDate = DateTime.Now
        };
        _db.CrmRecords.Add(record);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "CRM", $"CRM entry added for owner (ID {ownerId}): {loyaltyPoints} pts.");
        TempData["SuccessMessage"] = "CRM interaction has been logged.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _db.CrmRecords.FindAsync(id);
        if (record != null)
        {
            _db.CrmRecords.Remove(record);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Delete", "CRM", $"CRM record #{id} deleted.");
            TempData["SuccessMessage"] = "CRM record has been deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOwnerDropdownAsync(int? selected = null)
    {
        var owners = await _db.Users.Where(u => u.Role == "Pet Owner" && u.IsActive)
            .OrderBy(u => u.Name).Select(u => new { u.UserID, u.Name }).ToListAsync();
        ViewBag.OwnerID = new SelectList(owners, "UserID", "Name", selected);
    }
}

public class LoyaltyLeaderboardRow
{
    public int OwnerID { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int Points { get; set; }
}
