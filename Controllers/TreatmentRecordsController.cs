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
public class TreatmentRecordsController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;

    public TreatmentRecordsController(VetCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var role = User.GetUserRole();
        var query = _db.TreatmentRecords
            .Include(t => t.Appointment).ThenInclude(a => a!.Pet).ThenInclude(p => p!.Owner)
            .Include(t => t.Appointment).ThenInclude(a => a!.Vet)
            .AsQueryable();

        if (role == "Pet Owner")
            query = query.Where(t => t.Appointment!.Pet!.OwnerID == User.GetUserId());

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t =>
                t.Diagnosis.Contains(search) ||
                (t.Prescription != null && t.Prescription.Contains(search)) ||
                t.Appointment!.Pet!.PetName.Contains(search));

        var records = await query.OrderByDescending(t => t.TreatmentDate).ToListAsync();
        ViewBag.Search = search;
        ViewData["Title"] = "Treatment Records";
        ViewData["DashTitle"] = role == "Pet Owner" ? "My Pets' Treatments" : "Treatment Records";
        return View(records);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var record = await _db.TreatmentRecords
            .Include(t => t.Appointment).ThenInclude(a => a!.Pet).ThenInclude(p => p!.Owner)
            .Include(t => t.Appointment).ThenInclude(a => a!.Vet)
            .FirstOrDefaultAsync(t => t.TreatmentID == id);
        if (record == null) return NotFound();

        if (User.GetUserRole() == "Pet Owner" && record.Appointment!.Pet!.OwnerID != User.GetUserId())
            return Forbid();

        ViewData["Title"] = $"Treatment #{record.TreatmentID}";
        ViewData["DashTitle"] = $"Treatment record #{record.TreatmentID}";
        return View(record);
    }

    [Authorize(Roles = "Administrator, Veterinarian")]
    public async Task<IActionResult> Create(int? appointmentId)
    {
        await PopulateAppointmentDropdownAsync(appointmentId);
        ViewData["Title"] = "New Treatment Record";
        ViewData["DashTitle"] = "Write Treatment Record";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Veterinarian")]
    public async Task<IActionResult> Create(int appointmentId, string diagnosis, string? prescription, string? treatmentNotes)
    {
        var appointment = await _db.Appointments.Include(a => a.Pet).FirstOrDefaultAsync(a => a.AppointmentID == appointmentId);
        if (appointment == null)
        {
            ModelState.AddModelError(string.Empty, "Please select a valid completed appointment.");
        }
        else if (appointment.Status != "Completed")
        {
            ModelState.AddModelError(string.Empty, "Treatment records can only be added to completed appointments.");
        }
        else if (await _db.TreatmentRecords.AnyAsync(t => t.AppointmentID == appointmentId))
        {
            ModelState.AddModelError(string.Empty, "This appointment already has a treatment record.");
        }

        if (string.IsNullOrWhiteSpace(diagnosis))
            ModelState.AddModelError(string.Empty, "Diagnosis is required.");

        if (!ModelState.IsValid)
        {
            await PopulateAppointmentDropdownAsync(appointmentId);
            return View();
        }

        var record = new TreatmentRecord
        {
            AppointmentID = appointmentId,
            Diagnosis = diagnosis!,
            Prescription = prescription,
            TreatmentNotes = treatmentNotes,
            TreatmentDate = DateTime.Now
        };
        _db.TreatmentRecords.Add(record);

        // Append a summary line to the pet's medical history.
        if (appointment!.Pet != null)
        {
            var entry = $"\n[{record.TreatmentDate:yyyy-MM-dd}] {appointment.Pet.PetName}: {diagnosis}";
            appointment.Pet.MedicalHistory = string.IsNullOrWhiteSpace(appointment.Pet.MedicalHistory)
                ? entry.TrimStart()
                : appointment.Pet.MedicalHistory + entry;
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "TreatmentRecords", $"Treatment record #{record.TreatmentID} added for appointment #{appointmentId}.");
        TempData["SuccessMessage"] = "Treatment record saved and medical history updated.";
        return RedirectToAction(nameof(Details), new { id = record.TreatmentID });
    }

    [Authorize(Roles = "Administrator, Veterinarian")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var record = await _db.TreatmentRecords.FindAsync(id);
        if (record == null) return NotFound();
        ViewData["DashTitle"] = $"Edit treatment record #{record.TreatmentID}";
        return View(record);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Veterinarian")]
    public async Task<IActionResult> Edit(int id, string diagnosis, string? prescription, string? treatmentNotes)
    {
        var record = await _db.TreatmentRecords.FindAsync(id);
        if (record == null) return NotFound();

        record.Diagnosis = diagnosis!;
        record.Prescription = prescription;
        record.TreatmentNotes = treatmentNotes;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "TreatmentRecords", $"Treatment record #{id} updated.");
        TempData["SuccessMessage"] = "Treatment record has been updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Veterinarian")]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _db.TreatmentRecords.FindAsync(id);
        if (record != null)
        {
            _db.TreatmentRecords.Remove(record);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Delete", "TreatmentRecords", $"Treatment record #{id} deleted.");
            TempData["SuccessMessage"] = "Treatment record has been deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAppointmentDropdownAsync(int? selected)
    {
        var appointments = await _db.Appointments
            .Include(a => a.Pet)
            .Where(a => a.Status == "Completed" && a.TreatmentRecord == null)
            .OrderByDescending(a => a.AppointmentDate)
            .Select(a => new { a.AppointmentID, Label = "#" + a.AppointmentID + " — " + a.Pet!.PetName + " (" + a.AppointmentDate.ToString("MMM dd") + ")" })
            .ToListAsync();
        ViewBag.AppointmentID = new SelectList(appointments, "AppointmentID", "Label", selected);
    }
}
