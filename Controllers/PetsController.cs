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
public class PetsController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;

    public PetsController(VetCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private bool IsStaff =>
        User.GetUserRole() is "Administrator" or "Clinic Staff" or "Veterinarian";

    public async Task<IActionResult> Index(string? search)
    {
        var role = User.GetUserRole();
        var query = _db.Pets.Include(p => p.Owner).AsQueryable();

        if (role == "Pet Owner")
            query = query.Where(p => p.OwnerID == User.GetUserId());

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.PetName.Contains(search) || p.Species.Contains(search));

        var pets = await query.OrderBy(p => p.PetName).ToListAsync();
        ViewBag.Search = search;
        ViewData["Title"] = "Pets";
        ViewData["DashTitle"] = role == "Pet Owner" ? "My Pets" : "Pet Records";
        return View(pets);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var pet = await _db.Pets
            .Include(p => p.Owner)
            .Include(p => p.Appointments).ThenInclude(a => a.Vet)
            .Include(p => p.Appointments).ThenInclude(a => a.TreatmentRecord)
            .Include(p => p.VaccinationReminders)
            .FirstOrDefaultAsync(p => p.PetID == id);
        if (pet == null) return NotFound();

        if (User.GetUserRole() == "Pet Owner" && pet.OwnerID != User.GetUserId())
            return Forbid();

        ViewData["Title"] = pet.PetName;
        ViewData["DashTitle"] = $"Pet profile — {pet.PetName}";
        return View(pet);
    }

    [Authorize(Roles = "Administrator, Clinic Staff, Pet Owner")]
    public async Task<IActionResult> Create()
    {
        await PopulateOwnerDropdownAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff, Pet Owner")]
    public async Task<IActionResult> Create(Pet pet)
    {
        if (User.GetUserRole() == "Pet Owner")
        {
            pet.OwnerID = User.GetUserId();
            ModelState.Remove(nameof(pet.OwnerID));
        }

        if (!ModelState.IsValid)
        {
            await PopulateOwnerDropdownAsync(pet.OwnerID);
            return View(pet);
        }

        _db.Pets.Add(pet);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Pets", $"Registered pet '{pet.PetName}' (ID {pet.PetID}).");
        TempData["SuccessMessage"] = $"Pet '{pet.PetName}' has been registered.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var pet = await _db.Pets.FindAsync(id);
        if (pet == null) return NotFound();
        await PopulateOwnerDropdownAsync(pet.OwnerID);
        ViewData["DashTitle"] = $"Edit pet — {pet.PetName}";
        return View(pet);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Edit(int id, Pet pet)
    {
        if (id != pet.PetID) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateOwnerDropdownAsync(pet.OwnerID);
            return View(pet);
        }

        _db.Update(pet);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Pets", $"Updated pet '{pet.PetName}' (ID {pet.PetID}).");
        TempData["SuccessMessage"] = $"Pet '{pet.PetName}' has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Delete(int id)
    {
        var pet = await _db.Pets.FindAsync(id);
        if (pet != null)
        {
            _db.Pets.Remove(pet);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Delete", "Pets", $"Deleted pet '{pet.PetName}' (ID {id}).");
            TempData["SuccessMessage"] = $"Pet '{pet.PetName}' has been removed.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOwnerDropdownAsync(int? selected = null)
    {
        var owners = await _db.Users
            .Where(u => u.Role == "Pet Owner" && u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new { u.UserID, u.Name })
            .ToListAsync();
        ViewBag.OwnerID = new SelectList(owners, "UserID", "Name", selected);
    }
}
