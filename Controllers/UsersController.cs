using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VetCare.Data;
using VetCare.Helpers;
using VetCare.Models;
using VetCare.Services;

namespace VetCare.Controllers;

[Authorize(Roles = "Administrator")]
public class UsersController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;
    private readonly PasswordHasher<User> _hasher = new();

    public UsersController(VetCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public static readonly string[] Roles = { "Administrator", "Veterinarian", "Clinic Staff", "Pet Owner", "Supplier" };

    public async Task<IActionResult> Index(string? search, string? role)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Name.Contains(search) || u.UserName.Contains(search));
        if (!string.IsNullOrWhiteSpace(role) && role != "All")
            query = query.Where(u => u.Role == role);

        var users = await query.OrderBy(u => u.Role).ThenBy(u => u.Name).ToListAsync();
        ViewBag.Search = search;
        ViewBag.Role = role;
        ViewData["Title"] = "User Accounts";
        ViewData["DashTitle"] = "User Account Management";
        return View(users);
    }

    public IActionResult Create()
    {
        ViewBag.RoleList = new SelectList(Roles);
        ViewData["DashTitle"] = "Create User Account";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string userName, string password, string role, string? email, string? contactNumber)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(role))
        {
            ModelState.AddModelError(string.Empty, "Name, username, password and role are required.");
        }
        else if (password.Length < 6)
        {
            ModelState.AddModelError(string.Empty, "Password must be at least 6 characters.");
        }
        else if (await _db.Users.AnyAsync(u => u.UserName == userName))
        {
            ModelState.AddModelError(string.Empty, $"Username '{userName}' is already taken.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.RoleList = new SelectList(Roles);
            return View();
        }

        var user = new User { Name = name, UserName = userName, Role = role, Email = email, ContactNumber = contactNumber };
        user.Password = _hasher.HashPassword(user, password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Users", $"Created {role} account '{userName}' for {name}.");
        TempData["SuccessMessage"] = $"Account '{userName}' ({role}) has been created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        ViewBag.Roles = Roles;
        ViewData["DashTitle"] = $"Edit account — {user.UserName}";
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, string role, string? email, string? contactNumber, bool isActive, string? newPassword)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        var oldRole = user.Role;
        user.Name = name;
        user.Role = role;
        user.Email = email;
        user.ContactNumber = contactNumber;
        user.IsActive = isActive;
        if (!string.IsNullOrWhiteSpace(newPassword))
            user.Password = _hasher.HashPassword(user, newPassword);

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Users", $"Updated account '{user.UserName}' (role: {oldRole} → {role}).");
        TempData["SuccessMessage"] = $"Account '{user.UserName}' has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (user.UserID == User.GetUserId())
        {
            TempData["SuccessMessage"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Index));
        }
        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Users", $"Account '{user.UserName}' {(user.IsActive ? "activated" : "deactivated")}.");
        TempData["SuccessMessage"] = $"Account '{user.UserName}' is now {(user.IsActive ? "active" : "inactive")}.";
        return RedirectToAction(nameof(Index));
    }
}
