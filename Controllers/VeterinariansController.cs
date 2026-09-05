using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetCare.Data;
using VetCare.Helpers;
using VetCare.Models;
using VetCare.Services;

namespace VetCare.Controllers;

[Authorize]
public class VeterinariansController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;
    private readonly PasswordHasher<User> _hasher = new();

    public VeterinariansController(VetCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var vets = await _db.Users
            .Where(u => u.Role == "Veterinarian")
            .Select(u => new VetListItem
            {
                UserID = u.UserID,
                Name = u.Name,
                Email = u.Email,
                ContactNumber = u.ContactNumber,
                IsActive = u.IsActive,
                AppointmentCount = u.Appointments.Count(a => a.Status != "Cancelled")
            })
            .OrderBy(u => u.Name)
            .ToListAsync();

        ViewData["Title"] = "Veterinarians";
        ViewData["DashTitle"] = "Veterinarian Management";
        return View(vets);
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult Create()
    {
        ViewData["DashTitle"] = "Add Veterinarian";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create(string name, string userName, string password, string? email, string? contactNumber)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(string.Empty, "Name, username and password are required.");
            return View();
        }
        if (await _db.Users.AnyAsync(u => u.UserName == userName))
        {
            ModelState.AddModelError(string.Empty, $"Username '{userName}' is already taken.");
            return View();
        }

        var vet = new User
        {
            Role = "Veterinarian",
            Name = name,
            UserName = userName,
            Email = email,
            ContactNumber = contactNumber
        };
        vet.Password = _hasher.HashPassword(vet, password);

        _db.Users.Add(vet);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Users", $"Added veterinarian '{name}' (ID {vet.UserID}).");
        TempData["SuccessMessage"] = $"Veterinarian '{name}' has been added and can now sign in.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var vet = await _db.Users.FirstOrDefaultAsync(u => u.UserID == id && u.Role == "Veterinarian");
        if (vet == null) return NotFound();
        ViewData["DashTitle"] = $"Edit veterinarian — {vet.Name}";
        return View(vet);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int id, string name, string? email, string? contactNumber, bool isActive, string? newPassword)
    {
        var vet = await _db.Users.FirstOrDefaultAsync(u => u.UserID == id && u.Role == "Veterinarian");
        if (vet == null) return NotFound();

        vet.Name = name;
        vet.Email = email;
        vet.ContactNumber = contactNumber;
        vet.IsActive = isActive;
        if (!string.IsNullOrWhiteSpace(newPassword))
            vet.Password = _hasher.HashPassword(vet, newPassword);

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Users", $"Updated veterinarian '{vet.Name}' (ID {id}).");
        TempData["SuccessMessage"] = $"Veterinarian '{vet.Name}' has been updated.";
        return RedirectToAction(nameof(Index));
    }
}

public class VetListItem
{
    public int UserID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ContactNumber { get; set; }
    public bool IsActive { get; set; }
    public int AppointmentCount { get; set; }
}
