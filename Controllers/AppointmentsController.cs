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
public class AppointmentsController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;

    public AppointmentsController(VetCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public static readonly string[] ServiceTypes =
        { "General Checkup", "Vaccination", "Dental Cleaning", "Grooming", "Follow-up", "Surgery Consult" };

    public async Task<IActionResult> Index(string? status, string? search)
    {
        var role = User.GetUserRole();
        var query = _db.Appointments
            .Include(a => a.Pet).ThenInclude(p => p!.Owner)
            .Include(a => a.Vet)
            .AsQueryable();

        if (role == "Pet Owner")
            query = query.Where(a => a.Pet!.OwnerID == User.GetUserId());
        else if (role == "Veterinarian")
            query = query.Where(a => a.VetID == User.GetUserId());

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            query = query.Where(a => a.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => a.Pet!.PetName.Contains(search) || a.Vet!.Name.Contains(search));

        var appointments = await query.OrderByDescending(a => a.AppointmentDate).ToListAsync();

        ViewBag.Status = status;
        ViewBag.Search = search;
        ViewBag.ServiceTypes = ServiceTypes;
        ViewData["Title"] = "Appointments";
        ViewData["DashTitle"] = role == "Veterinarian" ? "My Schedule" : role == "Pet Owner" ? "My Appointments" : "Appointment Scheduling";
        return View(appointments);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var appointment = await _db.Appointments
            .Include(a => a.Pet).ThenInclude(p => p!.Owner)
            .Include(a => a.Vet)
            .Include(a => a.TreatmentRecord)
            .Include(a => a.Billing)
            .FirstOrDefaultAsync(a => a.AppointmentID == id);
        if (appointment == null) return NotFound();

        var role = User.GetUserRole();
        var allowed = role switch
        {
            "Administrator" or "Clinic Staff" => true,
            "Veterinarian" => appointment.VetID == User.GetUserId(),
            "Pet Owner" => appointment.Pet!.OwnerID == User.GetUserId(),
            _ => false
        };
        if (!allowed) return Forbid();

        ViewData["Title"] = $"Appointment #{appointment.AppointmentID}";
        ViewData["DashTitle"] = $"Appointment #{appointment.AppointmentID}";
        return View(appointment);
    }

    public async Task<IActionResult> Create(int? petId)
    {
        await PopulateDropdownsAsync();
        ViewBag.SelectedPetId = petId;
        ViewData["Title"] = "Book Appointment";
        ViewData["DashTitle"] = "Schedule an Appointment";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int petId, int vetId, DateTime appointmentDate, string serviceType, string? notes)
    {
        var role = User.GetUserRole();

        var pet = await _db.Pets.FirstOrDefaultAsync(p => p.PetID == petId);
        if (pet == null || (role == "Pet Owner" && pet.OwnerID != User.GetUserId()))
        {
            ModelState.AddModelError(string.Empty, "Please select a valid pet.");
        }
        if (appointmentDate == default || appointmentDate < DateTime.Now.AddMinutes(-5))
        {
            ModelState.AddModelError(string.Empty, "Appointment date must be in the future.");
        }

        if (ModelState.IsValid)
        {
            var clash = await _db.Appointments.AnyAsync(a =>
                a.VetID == vetId &&
                a.Status != "Cancelled" &&
                a.AppointmentDate > appointmentDate.AddMinutes(-45) &&
                a.AppointmentDate < appointmentDate.AddMinutes(45));
            if (clash)
            {
                ModelState.AddModelError(string.Empty, "The selected veterinarian already has an appointment within 45 minutes of that time. Please pick another slot.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            ViewBag.SelectedPetId = petId;
            return View();
        }

        var appointment = new Appointment
        {
            PetID = petId,
            VetID = vetId,
            AppointmentDate = appointmentDate,
            ServiceType = serviceType,
            Status = "Pending",
            Notes = notes
        };
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Create", "Appointments",
            $"Appointment #{appointment.AppointmentID} booked for pet '{pet.PetName}' on {appointment.AppointmentDate:g}.");

        TempData["SuccessMessage"] = "Appointment booked! Our staff will confirm your visit shortly.";
        return RedirectToAction(nameof(Details), new { id = appointment.AppointmentID });
    }

    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();
        await PopulateDropdownsAsync();
        ViewData["Title"] = "Edit Appointment";
        ViewData["DashTitle"] = $"Edit appointment #{appointment.AppointmentID}";
        return View(appointment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Edit(int id, int petId, int vetId, DateTime appointmentDate, string serviceType, string status, string? notes)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        appointment.PetID = petId;
        appointment.VetID = vetId;
        appointment.AppointmentDate = appointmentDate;
        appointment.ServiceType = serviceType;
        appointment.Status = status;
        appointment.Notes = notes;

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(appointment);
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Appointments", $"Appointment #{id} updated (status: {status}).");
        TempData["SuccessMessage"] = $"Appointment #{id} has been updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Approve(int id)
    {
        if (User.GetUserRole() is not ("Administrator" or "Clinic Staff")) return Forbid();

        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();
        if (appointment.Status != "Pending")
        {
            TempData["SuccessMessage"] = $"Only pending appointments can be approved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        appointment.Status = "Confirmed";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Appointments", $"Appointment #{id} approved (pending → confirmed).");
        TempData["SuccessMessage"] = $"Appointment #{id} has been approved. It can be marked completed after the visit.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var role = User.GetUserRole();
        var appointment = await _db.Appointments
            .Include(a => a.Pet)
            .FirstOrDefaultAsync(a => a.AppointmentID == id);
        if (appointment == null) return NotFound();
        if (role != "Administrator" && role != "Clinic Staff" && !(role == "Veterinarian" && appointment.VetID == User.GetUserId()))
            return Forbid();

        appointment.Status = "Completed";
        await _db.SaveChangesAsync();

        // Automatically forward the consultation bill to Billing (front desk) for invoicing.
        var billNote = string.Empty;
        if (!await _db.Billings.AnyAsync(b => b.AppointmentID == id))
        {
            var fee = ServiceFees.GetFee(appointment.ServiceType);
            _db.Billings.Add(new Billing
            {
                AppointmentID = appointment.AppointmentID,
                OwnerID = appointment.Pet!.OwnerID,
                TotalAmount = fee,
                PaymentMethod = "Cash",
                PaymentStatus = "Pending",
                DateIssued = DateTime.Now
            });
            await _db.SaveChangesAsync();
            billNote = $" The bill (₱{fee:N2}) has been sent to Billing — the front desk will issue it to the owner.";
        }

        await _audit.LogAsync("Update", "Appointments", $"Appointment #{id} marked as Completed. Bill forwarded to Billing.");
        TempData["SuccessMessage"] = $"Appointment #{id} completed.{billNote} You can now add the treatment record.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var role = User.GetUserRole();
        var appointment = await _db.Appointments
            .Include(a => a.Pet)
            .FirstOrDefaultAsync(a => a.AppointmentID == id);
        if (appointment == null) return NotFound();
        if (role != "Administrator" && role != "Clinic Staff" && !(role == "Pet Owner" && appointment.Pet!.OwnerID == User.GetUserId()))
            return Forbid();

        appointment.Status = "Cancelled";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Appointments", $"Appointment #{id} cancelled by {role}.");
        TempData["SuccessMessage"] = $"Appointment #{id} has been cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int id)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment != null)
        {
            _db.Appointments.Remove(appointment);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Delete", "Appointments", $"Appointment #{id} deleted.");
            TempData["SuccessMessage"] = $"Appointment #{id} has been deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync()
    {
        var vets = await _db.Users
            .Where(u => u.Role == "Veterinarian" && u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new { u.UserID, u.Name })
            .ToListAsync();
        ViewBag.VetID = new SelectList(vets, "UserID", "Name");
        ViewBag.ServiceTypes = ServiceTypes;

        var role = User.GetUserRole();
        var petsQuery = _db.Pets.Include(p => p.Owner).AsQueryable();
        if (role == "Pet Owner")
            petsQuery = petsQuery.Where(p => p.OwnerID == User.GetUserId());
        var pets = await petsQuery.OrderBy(p => p.PetName)
            .Select(p => new { p.PetID, Label = p.PetName + " (" + p.Owner!.Name + ")" })
            .ToListAsync();
        ViewBag.PetID = new SelectList(pets, "PetID", "Label");
    }
}
