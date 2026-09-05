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
        Seed(context);
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
