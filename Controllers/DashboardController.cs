using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetCare.Data;
using VetCare.Helpers;

namespace VetCare.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly VetCareDbContext _db;

    public DashboardController(VetCareDbContext db) => _db = db;

    public IActionResult Index()
    {
        return User.GetUserRole() switch
        {
            "Administrator" => RedirectToAction(nameof(Admin)),
            "Veterinarian" => RedirectToAction(nameof(Vet)),
            "Clinic Staff" => RedirectToAction(nameof(Staff)),
            "Pet Owner" => RedirectToAction(nameof(Owner)),
            "Supplier" => RedirectToAction(nameof(Supplier)),
            _ => RedirectToAction("Index", "Home")
        };
    }

    // ---------------- Administrator ----------------
    public async Task<IActionResult> Admin()
    {
        if (User.GetUserRole() != "Administrator") return Forbid();

        var today = DateTime.Today;
        var vm = new ViewModels.AdminDashboardViewModel
        {
            TotalPets = await _db.Pets.CountAsync(),
            TotalOwners = await _db.Users.CountAsync(u => u.Role == "Pet Owner"),
            TodayAppointments = await _db.Appointments
                .Include(a => a.Pet).ThenInclude(p => p!.Owner)
                .Include(a => a.Vet)
                .Where(a => a.AppointmentDate.Date == today && a.Status != "Cancelled")
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync(),
            PendingInvoices = await _db.Billings.CountAsync(b => b.PaymentStatus == "Pending"),
            PendingAmount = await _db.Billings
                .Where(b => b.PaymentStatus == "Pending")
                .SumAsync(b => (decimal?)b.TotalAmount) ?? 0m,
            CollectedAmount = await _db.Billings
                .Where(b => b.PaymentStatus == "Paid")
                .SumAsync(b => (decimal?)b.TotalAmount) ?? 0m,
            LowStockItems = await _db.InventoryItems
                .Include(i => i.Supplier)
                .Where(i => i.Quantity <= i.ReorderLevel)
                .OrderBy(i => i.Quantity)
                .ToListAsync(),
            RemindersDue = await _db.VaccinationReminders
                .Include(v => v.Pet)
                .CountAsync(v => v.Status == "Pending" && v.DueDate.Date <= today.AddDays(7)),
            RecentAuditLogs = await _db.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(6)
                .ToListAsync(),
            MonthlyRevenue = await _db.Billings
                .Where(b => b.PaymentStatus == "Paid" && b.DateIssued >= today.AddMonths(-6))
                .ToListAsync()
        };

        ViewData["Title"] = "Admin Dashboard";
        ViewData["DashTitle"] = $"Welcome back, {User.Identity?.Name}";
        return View("Admin", vm);
    }

    // ---------------- Veterinarian ----------------
    public async Task<IActionResult> Vet()
    {
        if (User.GetUserRole() != "Veterinarian") return Forbid();
        var vetId = User.GetUserId();
        var today = DateTime.Today;

        var vm = new ViewModels.VetDashboardViewModel
        {
            TodayAppointments = await _db.Appointments
                .Include(a => a.Pet).ThenInclude(p => p!.Owner)
                .Where(a => a.VetID == vetId && a.AppointmentDate.Date == today && a.Status != "Cancelled")
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync(),
            UpcomingAppointments = await _db.Appointments
                .Include(a => a.Pet).ThenInclude(p => p!.Owner)
                .Where(a => a.VetID == vetId && a.AppointmentDate.Date > today && (a.Status == "Pending" || a.Status == "Confirmed"))
                .OrderBy(a => a.AppointmentDate)
                .Take(5)
                .ToListAsync(),
            CompletedWithoutRecord = await _db.Appointments
                .Include(a => a.Pet)
                .Where(a => a.VetID == vetId && a.Status == "Completed" && a.TreatmentRecord == null)
                .OrderByDescending(a => a.AppointmentDate)
                .ToListAsync(),
            RemindersToSet = await _db.VaccinationReminders
                .Include(v => v.Pet)
                .Where(v => v.Status == "Pending" && v.DueDate.Date <= today.AddDays(7))
                .OrderBy(v => v.DueDate)
                .Take(5)
                .ToListAsync(),
            TotalPatients = await _db.Pets.CountAsync(p =>
                p.Appointments.Any(a => a.VetID == vetId))
        };

        ViewData["Title"] = "Veterinarian Dashboard";
        ViewData["DashTitle"] = $"Dr. {User.Identity?.Name}'s schedule";
        return View("Vet", vm);
    }

    // ---------------- Clinic Staff ----------------
    public async Task<IActionResult> Staff()
    {
        if (User.GetUserRole() != "Clinic Staff") return Forbid();
        var today = DateTime.Today;

        var vm = new ViewModels.StaffDashboardViewModel
        {
            TodayAppointments = await _db.Appointments
                .Include(a => a.Pet).ThenInclude(p => p!.Owner)
                .Include(a => a.Vet)
                .Where(a => a.AppointmentDate.Date == today && a.Status != "Cancelled")
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync(),
            PendingInvoices = await _db.Billings
                .Include(b => b.Owner)
                .Include(b => b.Appointment).ThenInclude(a => a!.Pet)
                .Where(b => b.PaymentStatus == "Pending")
                .OrderBy(b => b.DateIssued)
                .ToListAsync(),
            LowStockItems = await _db.InventoryItems
                .Include(i => i.Supplier)
                .Where(i => i.Quantity <= i.ReorderLevel)
                .OrderBy(i => i.Quantity)
                .ToListAsync(),
            TotalPets = await _db.Pets.CountAsync(),
            TotalOwners = await _db.Users.CountAsync(u => u.Role == "Pet Owner")
        };

        ViewData["Title"] = "Staff Dashboard";
        ViewData["DashTitle"] = $"Front desk overview, {User.Identity?.Name}";
        return View("Staff", vm);
    }

    // ---------------- Pet Owner ----------------
    public async Task<IActionResult> Owner()
    {
        if (User.GetUserRole() != "Pet Owner") return Forbid();
        var ownerId = User.GetUserId();
        var today = DateTime.Today;

        var vm = new ViewModels.OwnerDashboardViewModel
        {
            MyPets = await _db.Pets.Where(p => p.OwnerID == ownerId).ToListAsync(),
            UpcomingAppointments = await _db.Appointments
                .Include(a => a.Pet)
                .Include(a => a.Vet)
                .Where(a => a.Pet!.OwnerID == ownerId && a.AppointmentDate.Date >= today && a.Status != "Cancelled")
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync(),
            PendingInvoices = await _db.Billings
                .Include(b => b.Appointment).ThenInclude(a => a!.Pet)
                .Where(b => b.OwnerID == ownerId && b.PaymentStatus == "Pending")
                .ToListAsync(),
            TotalPaid = await _db.Billings
                .Where(b => b.OwnerID == ownerId && b.PaymentStatus == "Paid")
                .SumAsync(b => (decimal?)b.TotalAmount) ?? 0m,
            UpcomingReminders = await _db.VaccinationReminders
                .Include(v => v.Pet)
                .Where(v => v.Pet!.OwnerID == ownerId && v.Status == "Pending" && v.DueDate.Date >= today)
                .OrderBy(v => v.DueDate)
                .Take(5)
                .ToListAsync(),
            LoyaltyPoints = await _db.CrmRecords
                .Where(c => c.OwnerID == ownerId)
                .SumAsync(c => (int?)c.LoyaltyPoints) ?? 0
        };

        ViewData["Title"] = "Pet Owner Dashboard";
        ViewData["DashTitle"] = $"Hello, {User.Identity?.Name}!";
        return View("Owner", vm);
    }

    // ---------------- Supplier ----------------
    public async Task<IActionResult> Supplier()
    {
        if (User.GetUserRole() != "Supplier") return Forbid();

        var name = User.Identity?.Name ?? "";
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.SupplierName == name);
        var vm = new ViewModels.SupplierDashboardViewModel
        {
            Supplier = supplier,
            CatalogItems = supplier == null
                ? new List<Models.InventoryItem>()
                : await _db.InventoryItems
                    .Include(i => i.Supplier)
                    .Where(i => i.SupplierID == supplier.SupplierID)
                    .OrderBy(i => i.ItemName)
                    .ToListAsync()
        };

        ViewData["Title"] = "Supplier Dashboard";
        ViewData["DashTitle"] = "Supplier portal";
        return View("Supplier", vm);
    }
}
