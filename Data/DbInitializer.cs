using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetCare.Models;

namespace VetCare.Data;

public static class DbInitializer
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static void Initialize(IServiceProvider services)
    {
        using var context = new VetCareDbContext(
            services.GetRequiredService<DbContextOptions<VetCareDbContext>>());
        context.Database.EnsureCreated();
        try
        {
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
                BEGIN
                    CREATE TABLE [Notifications] (
                        [NotificationID] int NOT NULL IDENTITY,
                        [UserID] int NOT NULL,
                        [Title] nvarchar(150) NOT NULL,
                        [Message] nvarchar(500) NOT NULL,
                        [Category] nvarchar(50) NOT NULL,
                        [ActionUrl] nvarchar(255) NULL,
                        [IsRead] bit NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_Notifications] PRIMARY KEY ([NotificationID]),
                        CONSTRAINT [FK_Notifications_Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [Users] ([UserID]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_Notifications_UserID] ON [Notifications] ([UserID]);
                END
            ");
        }
        catch { /* ignore if already created or managed by EF */ }

        Seed(context);
        SeedNotifications(context);
    }

    private static void Seed(VetCareDbContext context)
    {
        if (context.Users.Any()) return;

        // Seed only the login accounts. All business modules (pets, appointments,
        // treatments, inventory, billing, reminders, CRM, suppliers, reports) start
        // empty — data is entered through the system itself.
        context.Users.AddRange(
            NewUser("Administrator", "Dr. Amelia Cruz", "admin", "admin123", "admin@vetcare.com"),
            NewUser("Veterinarian", "Dr. Sarah Chen", "vet", "vet123", "vet@vetcare.com", "0917-100-2000"),
            NewUser("Veterinarian", "Dr. Marco Reyes", "vet2", "vet123", "vet2@vetcare.com", "0917-100-2001"),
            NewUser("Clinic Staff", "Grace Lim", "staff", "staff123", "staff@vetcare.com", "0917-100-3000"),
            NewUser("Pet Owner", "Jason Surdilla", "owner", "owner123", "owner@vetcare.com", "0917-100-4000", "123 Mabini St., Quezon City"),
            NewUser("Pet Owner", "Maria Santos", "owner2", "owner123", "owner2@vetcare.com", "0917-100-4001", "45 Rizal Ave., Makati City"),
            NewUser("Supplier", "VetSupply Co.", "supplier", "supplier123", "supplier@vetcare.com"));

        context.SaveChanges();

        SeedNotifications(context);
    }

    private static void SeedNotifications(VetCareDbContext context)
    {
        if (context.Notifications.Any()) return;

        var owner = context.Users.FirstOrDefault(u => u.UserName == "owner");
        var staff = context.Users.FirstOrDefault(u => u.UserName == "staff");
        var admin = context.Users.FirstOrDefault(u => u.UserName == "admin");

        if (owner != null)
        {
            context.Notifications.AddRange(
                new Notification
                {
                    UserID = owner.UserID,
                    Title = "Appointment Confirmed! 🎉",
                    Message = "Your appointment for 'Milo' on Tomorrow at 10:00 AM with Dr. Sarah Chen has been accepted and confirmed by the clinic.",
                    Category = "Appointment",
                    ActionUrl = "/Appointments",
                    IsRead = false,
                    CreatedAt = DateTime.Now.AddMinutes(-12)
                },
                new Notification
                {
                    UserID = owner.UserID,
                    Title = "New Invoice Issued 💳",
                    Message = "Invoice #INV-0001 for ₱500.00 has been issued for Milo's General Checkup.",
                    Category = "Billing",
                    ActionUrl = "/Billing",
                    IsRead = false,
                    CreatedAt = DateTime.Now.AddHours(-2)
                },
                new Notification
                {
                    UserID = owner.UserID,
                    Title = "Upcoming Vaccination Scheduled 💉",
                    Message = "Notice: 'Milo' is scheduled for Rabies Vaccine due on next week.",
                    Category = "Reminder",
                    ActionUrl = "/VaccinationReminders",
                    IsRead = true,
                    CreatedAt = DateTime.Now.AddDays(-1)
                }
            );
        }

        if (staff != null)
        {
            context.Notifications.AddRange(
                new Notification
                {
                    UserID = staff.UserID,
                    Title = "New Appointment Request",
                    Message = "Pet Owner 'Jason Surdilla' requested a General Checkup for 'Milo' on Tomorrow at 10:00 AM.",
                    Category = "Appointment",
                    ActionUrl = "/Appointments",
                    IsRead = false,
                    CreatedAt = DateTime.Now.AddMinutes(-15)
                },
                new Notification
                {
                    UserID = staff.UserID,
                    Title = "Payment Settled by Owner",
                    Message = "Pet Owner Jason Surdilla paid Invoice #INV-0001 (₱500.00) via Cash.",
                    Category = "Billing",
                    ActionUrl = "/Billing",
                    IsRead = false,
                    CreatedAt = DateTime.Now.AddHours(-1)
                }
            );
        }

        if (admin != null)
        {
            context.Notifications.AddRange(
                new Notification
                {
                    UserID = admin.UserID,
                    Title = "New Appointment Request",
                    Message = "Pet Owner 'Jason Surdilla' requested an appointment for 'Milo'.",
                    Category = "Appointment",
                    ActionUrl = "/Appointments",
                    IsRead = false,
                    CreatedAt = DateTime.Now.AddMinutes(-20)
                },
                new Notification
                {
                    UserID = admin.UserID,
                    Title = "Inventory Low Stock Alert ⚠️",
                    Message = "Amoxicillin 250mg is below reorder level (4 units remaining).",
                    Category = "General",
                    ActionUrl = "/Inventory",
                    IsRead = true,
                    CreatedAt = DateTime.Now.AddDays(-1)
                }
            );
        }

        context.SaveChanges();
    }

    private static User NewUser(string role, string name, string userName, string password, string email, string? contact = null, string? address = null)
    {
        var user = new User
        {
            Role = role,
            Name = name,
            UserName = userName,
            Email = email,
            ContactNumber = contact,
            Address = address,
            CreatedDate = DateTime.Now
        };
        user.Password = Hasher.HashPassword(user, password);
        return user;
    }
}
