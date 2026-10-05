using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetCare.Models;

namespace VetCare.Data;

public static class DbInitializer
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static void Initialize(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();

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
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BillingItems')
                BEGIN
                    CREATE TABLE [BillingItems] (
                        [BillingItemID] int NOT NULL IDENTITY,
                        [InvoiceID] int NOT NULL,
                        [InventoryItemID] int NOT NULL,
                        [Description] nvarchar(200) NOT NULL,
                        [Quantity] int NOT NULL,
                        [UnitPrice] decimal(10,2) NOT NULL,
                        CONSTRAINT [PK_BillingItems] PRIMARY KEY ([BillingItemID]),
                        CONSTRAINT [FK_BillingItems_Billings_InvoiceID] FOREIGN KEY ([InvoiceID]) REFERENCES [Billings] ([InvoiceID]) ON DELETE CASCADE,
                        CONSTRAINT [FK_BillingItems_InventoryItems_InventoryItemID] FOREIGN KEY ([InventoryItemID]) REFERENCES [InventoryItems] ([ItemID])
                    );
                    CREATE INDEX [IX_BillingItems_InvoiceID] ON [BillingItems] ([InvoiceID]);
                END
            ");
        context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseRequests')
                BEGIN
                    CREATE TABLE [PurchaseRequests] (
                        [RequestID] int NOT NULL IDENTITY,
                        [InventoryItemID] int NOT NULL,
                        [RequestedBy] int NOT NULL,
                        [Quantity] int NOT NULL,
                        [Status] nvarchar(20) NOT NULL,
                        [Notes] nvarchar(500) NULL,
                        [RequestedAt] datetime2 NOT NULL,
                        [ProcessedAt] datetime2 NULL,
                        [ProcessedBy] int NULL,
                        CONSTRAINT [PK_PurchaseRequests] PRIMARY KEY ([RequestID]),
                        CONSTRAINT [FK_PurchaseRequests_InventoryItems_InventoryItemID] FOREIGN KEY ([InventoryItemID]) REFERENCES [InventoryItems] ([ItemID]),
                        CONSTRAINT [FK_PurchaseRequests_Users_RequestedBy] FOREIGN KEY ([RequestedBy]) REFERENCES [Users] ([UserID]),
                        CONSTRAINT [FK_PurchaseRequests_Users_ProcessedBy] FOREIGN KEY ([ProcessedBy]) REFERENCES [Users] ([UserID])
                    );
                    CREATE INDEX [IX_PurchaseRequests_InventoryItemID] ON [PurchaseRequests] ([InventoryItemID]);
                    CREATE INDEX [IX_PurchaseRequests_RequestedBy] ON [PurchaseRequests] ([RequestedBy]);
                END
            ");
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RewardRedemptions')
                BEGIN
                    CREATE TABLE [RewardRedemptions] (
                        [RedemptionID] int NOT NULL IDENTITY,
                        [OwnerID] int NOT NULL,
                        [RewardType] nvarchar(50) NOT NULL,
                        [PointsSpent] int NOT NULL,
                        [ServiceValue] decimal(10,2) NOT NULL,
                        [Status] nvarchar(20) NOT NULL,
                        [AppointmentID] int NULL,
                        [RedeemedAt] datetime2 NOT NULL,
                        [CompletedAt] datetime2 NULL,
                        CONSTRAINT [PK_RewardRedemptions] PRIMARY KEY ([RedemptionID]),
                        CONSTRAINT [FK_RewardRedemptions_Users_OwnerID] FOREIGN KEY ([OwnerID]) REFERENCES [Users] ([UserID]),
                        CONSTRAINT [FK_RewardRedemptions_Appointments_AppointmentID] FOREIGN KEY ([AppointmentID]) REFERENCES [Appointments] ([AppointmentID]) ON DELETE SET NULL
                    );
                    CREATE UNIQUE INDEX [IX_RewardRedemptions_AppointmentID] ON [RewardRedemptions] ([AppointmentID]) WHERE [AppointmentID] IS NOT NULL;
                    CREATE INDEX [IX_RewardRedemptions_OwnerID] ON [RewardRedemptions] ([OwnerID]);
                END
            ");
        }
        catch { /* ignore if already created or managed by EF */ }

        Seed(context, configuration);
        SeedNotifications(context);
        SeedInventory(context);
    }

    private static void Seed(VetCareDbContext context, IConfiguration configuration)
    {
        if (context.Users.Any()) return;

        // Seed only the login accounts. All business modules (pets, appointments,
        // treatments, inventory, billing, reminders, CRM, suppliers, reports) start
        // empty — data is entered through the system itself.
        context.Users.AddRange(
            NewUser("Administrator", "Dr. Amelia Cruz", "admin", ResolvePassword(configuration, "Administrator"), "admin@vetcare.com"),
            NewUser("Veterinarian", "Dr. Sarah Chen", "vet", ResolvePassword(configuration, "Veterinarian"), "vet@vetcare.com", "0917-100-2000"),
            NewUser("Clinic Staff", "Grace Lim", "staff", ResolvePassword(configuration, "ClinicStaff"), "staff@vetcare.com", "0917-100-3000"),
            NewUser("Pet Owner", "Jason Surdilla", "owner", ResolvePassword(configuration, "PetOwner"), "owner@vetcare.com", "0917-100-4000", "123 Mabini St., Quezon City"),
            NewUser("Supplier", "VetSupply Co.", "supplier", ResolvePassword(configuration, "Supplier"), "supplier@vetcare.com"));

        context.SaveChanges();

        if (context.Suppliers.Any() == false)
        {
            context.Suppliers.Add(new Supplier
            {
                SupplierName = "VetSupply Co.",
                ContactInfo = "supplier@vetcare.com | 0917-555-0142 | Unit 8, Sterling Industrial Park, Valenzuela City",
                ProductCatalog = "Antibiotics, antiparasitics, vaccines",
                ContractDetails = "Standard supply agreement; 30-day payment terms; clinic restock requests are sent here."
            });
            context.SaveChanges();
        }

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

    private static void SeedInventory(VetCareDbContext context)
    {
        if (context.InventoryItems.Any()) return;

        var supplierID = context.Suppliers.FirstOrDefault()?.SupplierID;

        context.InventoryItems.AddRange(
            NewInventoryItem("Amoxicillin", "Antibiotic", 50, 35.00m, 10, supplierID),
            NewInventoryItem("Enrofloxacin", "Antibiotic", 40, 60.00m, 10, supplierID),
            NewInventoryItem("Ivermectin", "Antiparasitic", 30, 45.00m, 8, supplierID),
            NewInventoryItem("Doxycycline", "Antibiotic", 45, 55.00m, 10, supplierID),
            NewInventoryItem("Vaccines", "Vaccine", 25, 150.00m, 5, supplierID));

        context.SaveChanges();
    }

    private static InventoryItem NewInventoryItem(string itemName, string category, int quantity, decimal unitPrice, int reorderLevel, int? supplierID = null)
    {
        return new InventoryItem
        {
            ItemName = itemName,
            Category = category,
            Quantity = quantity,
            UnitPrice = unitPrice,
            ReorderLevel = reorderLevel,
            SupplierID = supplierID,
            LastUpdated = DateTime.Now
        };
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

    /// <summary>
    /// Resolves a demo account password from configuration (e.g. "Seed:Administrator:Password",
    /// supplied via user-secrets, an environment variable, or the hosting control panel).
    /// When nothing is configured a strong random password is generated and logged once,
    /// so no credential ever has to be committed to source control.
    /// </summary>
    private static string ResolvePassword(IConfiguration configuration, string accountKey)
    {
        var configured = configuration[$"Seed:{accountKey}:Password"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        var generated = GeneratePassword();
        Console.WriteLine($"[DbInitializer] No 'Seed:{accountKey}:Password' configured — generated demo password: {generated}");
        return generated;
    }

    private static string GeneratePassword()
    {
        // Ambiguous characters (0/O, 1/l/I) omitted so passwords stay easy to retype.
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$%^&*";
        return RandomNumberGenerator.GetString(alphabet, 16);
    }
}
