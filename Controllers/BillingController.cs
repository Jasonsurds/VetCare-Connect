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
public class BillingController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;

    public BillingController(VetCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? status)
    {
        var role = User.GetUserRole();
        var query = _db.Billings
            .Include(b => b.Owner)
            .Include(b => b.Appointment).ThenInclude(a => a!.Pet)
            .AsQueryable();

        if (role == "Pet Owner")
            query = query.Where(b => b.OwnerID == User.GetUserId());

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            query = query.Where(b => b.PaymentStatus == status);

        var invoices = await query.OrderByDescending(b => b.DateIssued).ToListAsync();

        ViewBag.Status = status;
        ViewData["Title"] = "Billing";
        ViewData["DashTitle"] = role == "Pet Owner" ? "My Invoices" : "Billing & Payments";
        return View(invoices);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var invoice = await _db.Billings
            .Include(b => b.Owner)
            .Include(b => b.Appointment).ThenInclude(a => a!.Pet)
            .Include(b => b.Appointment).ThenInclude(a => a!.Vet)
            .FirstOrDefaultAsync(b => b.InvoiceID == id);
        if (invoice == null) return NotFound();

        if (User.GetUserRole() == "Pet Owner" && invoice.OwnerID != User.GetUserId())
            return Forbid();

        ViewData["Title"] = $"Invoice #{invoice.InvoiceID}";
        ViewData["DashTitle"] = $"Invoice #INV-{invoice.InvoiceID:D4}";
        return View(invoice);
    }

    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Create(int? appointmentId)
    {
        await PopulateAppointmentDropdownAsync(appointmentId);
        ViewData["DashTitle"] = "Issue Invoice";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Create(int appointmentId, decimal totalAmount, string paymentMethod)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Pet)
            .FirstOrDefaultAsync(a => a.AppointmentID == appointmentId);

        if (appointment == null || appointment.Status != "Completed")
            ModelState.AddModelError(string.Empty, "Invoices can only be issued for completed appointments.");
        else if (await _db.Billings.AnyAsync(b => b.AppointmentID == appointmentId))
            ModelState.AddModelError(string.Empty, "This appointment already has an invoice.");
        if (totalAmount <= 0)
            ModelState.AddModelError(string.Empty, "Total amount must be greater than zero.");

        if (!ModelState.IsValid)
        {
            await PopulateAppointmentDropdownAsync(appointmentId);
            return View();
        }

        var invoice = new Billing
        {
            AppointmentID = appointmentId,
            OwnerID = appointment!.Pet!.OwnerID,
            TotalAmount = totalAmount,
            PaymentMethod = paymentMethod,
            PaymentStatus = "Pending",
            DateIssued = DateTime.Now
        };
        _db.Billings.Add(invoice);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Billing", $"Invoice #INV-{invoice.InvoiceID:D4} issued for {invoice.TotalAmount:N2} (appointment #{appointmentId}).");
        TempData["SuccessMessage"] = $"Invoice #INV-{invoice.InvoiceID:D4} has been issued.";
        return RedirectToAction(nameof(Details), new { id = invoice.InvoiceID });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int id, string? paymentMethod)
    {
        var role = User.GetUserRole();
        var invoice = await _db.Billings.FindAsync(id);
        if (invoice == null) return NotFound();

        if (role != "Administrator" && role != "Clinic Staff")
        {
            if (role != "Pet Owner" || invoice.OwnerID != User.GetUserId())
                return Forbid();
            // Owners settling their own invoice always use their selected method.
            if (!string.IsNullOrWhiteSpace(paymentMethod))
                invoice.PaymentMethod = paymentMethod;
        }

        invoice.PaymentStatus = "Paid";
        await _db.SaveChangesAsync();

        // Award loyalty points: 1 point per ₱100 paid.
        var points = (int)(invoice.TotalAmount / 100m);
        if (points > 0)
        {
            _db.CrmRecords.Add(new CrmRecord
            {
                OwnerID = invoice.OwnerID,
                Interaction = $"Invoice #INV-{invoice.InvoiceID:D4} paid ({invoice.PaymentMethod}). Loyalty points awarded.",
                LoyaltyPoints = points,
                InteractionDate = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }

        await _audit.LogAsync("Update", "Billing", $"Invoice #INV-{invoice.InvoiceID:D4} marked as Paid ({invoice.PaymentMethod}). {points} loyalty points awarded.");
        TempData["SuccessMessage"] = $"Invoice #INV-{invoice.InvoiceID:D4} is now paid. {(points > 0 ? $"{points} loyalty points awarded!" : "")}";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateAppointmentDropdownAsync(int? selected)
    {
        var appointments = await _db.Appointments
            .Include(a => a.Pet).ThenInclude(p => p!.Owner)
            .Where(a => a.Status == "Completed" && a.Billing == null)
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();

        ViewBag.AppointmentOptions = appointments.Select(a => new AppointmentFeeOption
        {
            AppointmentID = a.AppointmentID,
            Label = $"#{a.AppointmentID} — {a.Pet!.PetName} / {a.Pet.Owner!.Name} ({a.AppointmentDate:MMM dd}) · {a.ServiceType}",
            Fee = ServiceFees.GetFee(a.ServiceType)
        }).ToList();
    }
}

public class AppointmentFeeOption
{
    public int AppointmentID { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Fee { get; set; }
}
