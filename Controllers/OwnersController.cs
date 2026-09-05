using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetCare.Data;
using VetCare.Helpers;
using VetCare.Models;
using VetCare.Services;

namespace VetCare.Controllers;

[Authorize(Roles = "Administrator, Clinic Staff")]
public class OwnersController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;
    private readonly PasswordHasher<User> _hasher = new();

    public OwnersController(VetCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.Users.Where(u => u.Role == "Pet Owner");
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Name.Contains(search) || u.Email!.Contains(search));

        var owners = await query
            .Select(u => new OwnerListItem
            {
                UserID = u.UserID,
                Name = u.Name,
                Email = u.Email,
                ContactNumber = u.ContactNumber,
                Address = u.Address,
                IsActive = u.IsActive,
                PetCount = u.Pets.Count
            })
            .OrderBy(u => u.Name)
            .ToListAsync();

        ViewBag.Search = search;
        ViewData["Title"] = "Pet Owners";
        ViewData["DashTitle"] = "Pet Owner Management";
        return View(owners);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var owner = await _db.Users
            .Include(u => u.Pets)
            .Include(u => u.Invoices)
            .Include(u => u.CrmRecords)
            .FirstOrDefaultAsync(u => u.UserID == id && u.Role == "Pet Owner");
        if (owner == null) return NotFound();

        ViewData["Title"] = owner.Name;
        ViewData["DashTitle"] = $"Owner profile — {owner.Name}";
        return View(owner);
    }

    public IActionResult Create()
    {
        ViewData["DashTitle"] = "Register Pet Owner";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string userName, string password, string? email, string? contactNumber, string? address)
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

        var owner = new User
        {
            Role = "Pet Owner",
            Name = name,
            UserName = userName,
            Email = email,
            ContactNumber = contactNumber,
            Address = address
        };
        owner.Password = _hasher.HashPassword(owner, password);

        _db.Users.Add(owner);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Users", $"Registered pet owner '{name}' (ID {owner.UserID}).");
        TempData["SuccessMessage"] = $"Pet owner '{name}' has been registered.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var owner = await _db.Users.FirstOrDefaultAsync(u => u.UserID == id && u.Role == "Pet Owner");
        if (owner == null) return NotFound();
        ViewData["DashTitle"] = $"Edit owner — {owner.Name}";
        return View(owner);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, string? email, string? contactNumber, string? address, bool isActive, string? newPassword)
    {
        var owner = await _db.Users.FirstOrDefaultAsync(u => u.UserID == id && u.Role == "Pet Owner");
        if (owner == null) return NotFound();

        owner.Name = name;
        owner.Email = email;
        owner.ContactNumber = contactNumber;
        owner.Address = address;
        owner.IsActive = isActive;

        if (!string.IsNullOrWhiteSpace(newPassword))
            owner.Password = _hasher.HashPassword(owner, newPassword);

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Users", $"Updated pet owner '{owner.Name}' (ID {id}).");
        TempData["SuccessMessage"] = $"Pet owner '{owner.Name}' has been updated.";
        return RedirectToAction(nameof(Index));
    }
}

public class OwnerListItem
{
    public int UserID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ContactNumber { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public int PetCount { get; set; }
}
