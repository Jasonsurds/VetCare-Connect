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
public class VaccinationRemindersController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;

    public VaccinationRemindersController(VetCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private bool CanManage => User.GetUserRole() is "Administrator" or "Clinic Staff" or "Veterinarian";

    public async Task<IActionResult> Index()
    {
        var role = User.GetUserRole();
        var query = _db.VaccinationReminders.Include(v => v.Pet).AsQueryable();

        if (role == "Pet Owner")
            query = query.Where(v => v.Pet!.OwnerID == User.GetUserId());

        var reminders = await query.OrderBy(v => v.DueDate).ToListAsync();
        ViewData["Title"] = "Vaccination Reminders";
        ViewData["DashTitle"] = role == "Pet Owner" ? "My Pets' Vaccination Reminders" : "Vaccination Reminders";
        return View(reminders);
    }

    [Authorize(Roles = "Administrator, Clinic Staff, Veterinarian")]
    public async Task<IActionResult> Create(int? petId)
    {
        await PopulatePetDropdownAsync(petId);
        ViewData["DashTitle"] = "Set Vaccination Reminder";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff, Veterinarian")]
    public async Task<IActionResult> Create(int petId, string vaccineName, DateTime dueDate)
    {
        if (string.IsNullOrWhiteSpace(vaccineName))
            ModelState.AddModelError(string.Empty, "Vaccine name is required.");
        if (dueDate == default)
            ModelState.AddModelError(string.Empty, "Due date is required.");

        if (!ModelState.IsValid)
        {
            await PopulatePetDropdownAsync(petId);
            return View();
        }

        var reminder = new VaccinationReminder { PetID = petId, VaccineName = vaccineName, DueDate = dueDate, Status = "Pending" };
        _db.VaccinationReminders.Add(reminder);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "VaccinationReminders", $"Reminder set: {vaccineName} for pet (ID {petId}) due {dueDate:d}.");
        TempData["SuccessMessage"] = $"Vaccination reminder for '{vaccineName}' has been scheduled. The owner will be notified.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator, Clinic Staff, Veterinarian")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var reminder = await _db.VaccinationReminders.FindAsync(id);
        if (reminder == null) return NotFound();
        await PopulatePetDropdownAsync(reminder.PetID);
        ViewData["DashTitle"] = "Edit Reminder";
        return View(reminder);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff, Veterinarian")]
    public async Task<IActionResult> Edit(int id, int petId, string vaccineName, DateTime dueDate, string status)
    {
        var reminder = await _db.VaccinationReminders.FindAsync(id);
        if (reminder == null) return NotFound();

        reminder.PetID = petId;
        reminder.VaccineName = vaccineName;
        reminder.DueDate = dueDate;
        reminder.Status = status;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "VaccinationReminders", $"Reminder #{id} updated ({vaccineName}, {status}).");
        TempData["SuccessMessage"] = "Reminder has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff, Veterinarian")]
    public async Task<IActionResult> MarkSent(int id)
    {
        var reminder = await _db.VaccinationReminders.Include(v => v.Pet).FirstOrDefaultAsync(v => v.ReminderID == id);
        if (reminder == null) return NotFound();

        reminder.Status = "Sent";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "VaccinationReminders", $"Reminder #{id} ({reminder.VaccineName}) sent to owner of '{reminder.Pet?.PetName}'.");
        TempData["SuccessMessage"] = $"Reminder for '{reminder.VaccineName}' marked as sent to the owner.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff, Veterinarian")]
    public async Task<IActionResult> Delete(int id)
    {
        var reminder = await _db.VaccinationReminders.FindAsync(id);
        if (reminder != null)
        {
            _db.VaccinationReminders.Remove(reminder);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Delete", "VaccinationReminders", $"Reminder #{id} ({reminder.VaccineName}) deleted.");
            TempData["SuccessMessage"] = "Reminder has been deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulatePetDropdownAsync(int? selected)
    {
        var pets = await _db.Pets.Include(p => p.Owner).OrderBy(p => p.PetName)
            .Select(p => new { p.PetID, Label = p.PetName + " (" + p.Owner!.Name + ")" })
            .ToListAsync();
        ViewBag.PetID = new SelectList(pets, "PetID", "Label", selected);
    }
}
