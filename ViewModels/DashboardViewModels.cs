using VetCare.Models;

namespace VetCare.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalPets { get; set; }
    public int TotalOwners { get; set; }
    public List<Appointment> TodayAppointments { get; set; } = new();
    public int PendingInvoices { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal CollectedAmount { get; set; }
    public List<InventoryItem> LowStockItems { get; set; } = new();
    public int RemindersDue { get; set; }
    public List<AuditLog> RecentAuditLogs { get; set; } = new();
    public List<Billing> MonthlyRevenue { get; set; } = new();
}

public class VetDashboardViewModel
{
    public int TotalPatients { get; set; }
    public List<Appointment> TodayAppointments { get; set; } = new();
    public List<Appointment> UpcomingAppointments { get; set; } = new();
    public List<Appointment> CompletedWithoutRecord { get; set; } = new();
    public List<VaccinationReminder> RemindersToSet { get; set; } = new();
}

public class StaffDashboardViewModel
{
    public int TotalPets { get; set; }
    public int TotalOwners { get; set; }
    public List<Appointment> TodayAppointments { get; set; } = new();
    public List<Billing> PendingInvoices { get; set; } = new();
    public List<InventoryItem> LowStockItems { get; set; } = new();
}

public class OwnerDashboardViewModel
{
    public List<Pet> MyPets { get; set; } = new();
    public List<Appointment> UpcomingAppointments { get; set; } = new();
    public List<Billing> PendingInvoices { get; set; } = new();
    public decimal TotalPaid { get; set; }
    public List<VaccinationReminder> UpcomingReminders { get; set; } = new();
    public int LoyaltyPoints { get; set; }
}

public class SupplierDashboardViewModel
{
    public Supplier? Supplier { get; set; }
    public List<InventoryItem> CatalogItems { get; set; } = new();
}
