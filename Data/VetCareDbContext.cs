using Microsoft.EntityFrameworkCore;
using VetCare.Models;

namespace VetCare.Data;

public class VetCareDbContext : DbContext
{
    public VetCareDbContext(DbContextOptions<VetCareDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<TreatmentRecord> TreatmentRecords => Set<TreatmentRecord>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Billing> Billings => Set<Billing>();
    public DbSet<VaccinationReminder> VaccinationReminders => Set<VaccinationReminder>();
    public DbSet<CrmRecord> CrmRecords => Set<CrmRecord>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.UserName).IsUnique();
        });

        modelBuilder.Entity<Pet>(e =>
        {
            e.HasOne(p => p.Owner)
             .WithMany(u => u.Pets)
             .HasForeignKey(p => p.OwnerID)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Appointment>(e =>
        {
            e.HasOne(a => a.Pet)
             .WithMany(p => p.Appointments)
             .HasForeignKey(a => a.PetID)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Vet)
             .WithMany(u => u.Appointments)
             .HasForeignKey(a => a.VetID)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TreatmentRecord>(e =>
        {
            e.HasKey(t => t.TreatmentID);
            e.HasOne(t => t.Appointment)
             .WithOne(a => a.TreatmentRecord)
             .HasForeignKey<TreatmentRecord>(t => t.AppointmentID)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryItem>(e =>
        {
            e.HasKey(i => i.ItemID);
            e.Property(i => i.UnitPrice).HasPrecision(10, 2);
            e.HasOne(i => i.Supplier)
             .WithMany(s => s.InventoryItems)
             .HasForeignKey(i => i.SupplierID)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Billing>(e =>
        {
            e.HasKey(b => b.InvoiceID);
            e.Property(b => b.TotalAmount).HasPrecision(10, 2);
            e.HasOne(b => b.Appointment)
             .WithOne(a => a.Billing)
             .HasForeignKey<Billing>(b => b.AppointmentID)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Owner)
             .WithMany(u => u.Invoices)
             .HasForeignKey(b => b.OwnerID)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VaccinationReminder>(e =>
        {
            e.HasKey(v => v.ReminderID);
            e.HasOne(v => v.Pet)
             .WithMany(p => p.VaccinationReminders)
             .HasForeignKey(v => v.PetID)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CrmRecord>(e =>
        {
            e.HasKey(c => c.CRMID);
            e.HasOne(c => c.Owner)
             .WithMany(u => u.CrmRecords)
             .HasForeignKey(c => c.OwnerID)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Report>(e =>
        {
            e.HasOne(r => r.Generator)
             .WithMany()
             .HasForeignKey(r => r.GeneratedBy)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
