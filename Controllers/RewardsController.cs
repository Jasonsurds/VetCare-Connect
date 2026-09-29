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
public class RewardsController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;
    private readonly INotificationService _notif;
    private readonly ILoyaltyService _loyalty;

    public RewardsController(VetCareDbContext db, IAuditService audit, INotificationService notif, ILoyaltyService loyalty)
    {
        _db = db;
        _audit = audit;
        _notif = notif;
        _loyalty = loyalty;
    }

    [Authorize(Roles = "Pet Owner")]
    public async Task<IActionResult> Redeem(string? rewardType)
    {
        var vm = await BuildRedeemViewModelAsync(rewardType);
        ViewData["Title"] = "Redeem Rewards";
        ViewData["DashTitle"] = "Redeem a Loyalty Reward";
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Pet Owner")]
    public async Task<IActionResult> Redeem(int petId, int vetId, DateTime appointmentDate, string rewardType, string? notes)
    {
        var ownerId = User.GetUserId();
        var vm = await BuildRedeemViewModelAsync(rewardType);

        if (!RewardCatalog.IsEligible(rewardType))
            ModelState.AddModelError(string.Empty, "Please choose an available reward.");

        var pet = await _db.Pets.FirstOrDefaultAsync(p => p.PetID == petId);
        if (pet == null || pet.OwnerID != ownerId)
            ModelState.AddModelError(string.Empty, "Please select one of your own pets.");

        var vet = await _db.Users.FirstOrDefaultAsync(u => u.UserID == vetId && u.Role == "Veterinarian" && u.IsActive);
        if (vet == null)
            ModelState.AddModelError(string.Empty, "Please select a valid veterinarian.");

        if (appointmentDate == default || appointmentDate < DateTime.Now.AddMinutes(-5))
            ModelState.AddModelError(string.Empty, "Appointment date must be in the future.");

        var cost = RewardCatalog.IsEligible(rewardType) ? RewardCatalog.CostFor(rewardType) : 0;
        if (RewardCatalog.IsEligible(rewardType) && vm.Balance < cost)
            ModelState.AddModelError(string.Empty, $"You need {cost} loyalty points for a free {rewardType}, but you only have {vm.Balance}.");

        if (ModelState.IsValid)
        {
            var clash = await _db.Appointments.AnyAsync(a =>
                a.VetID == vetId &&
                a.Status != "Cancelled" &&
                a.AppointmentDate > appointmentDate.AddMinutes(-45) &&
                a.AppointmentDate < appointmentDate.AddMinutes(45));
            if (clash)
                ModelState.AddModelError(string.Empty, "The selected veterinarian already has an appointment within 45 minutes of that time. Please pick another slot.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Redeem Rewards";
            ViewData["DashTitle"] = "Redeem a Loyalty Reward";
            return View(vm);
        }

        // Book the free visit. It lands in Pending like any owner request, so staff still
        // approve it — but the points are already spent, hence they are deducted now.
        var appointment = new Appointment
        {
            PetID = petId,
            VetID = vetId,
            AppointmentDate = appointmentDate,
            ServiceType = rewardType,
            Status = "Pending",
            Notes = string.IsNullOrWhiteSpace(notes)
                ? $"Redeemed with {cost} loyalty points - free {rewardType}."
                : $"{notes.Trim()} (Redeemed with {cost} loyalty points - free {rewardType}.)"
        };
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        var redemption = new RewardRedemption
        {
            OwnerID = ownerId,
            RewardType = rewardType,
            PointsSpent = cost,
            ServiceValue = RewardCatalog.ValueOf(rewardType),
            Status = "Booked",
            AppointmentID = appointment.AppointmentID,
            RedeemedAt = DateTime.Now
        };
        _db.RewardRedemptions.Add(redemption);
        await _db.SaveChangesAsync();

        await _loyalty.DeductAsync(ownerId, cost,
            $"Redeemed {cost} loyalty points for a free {rewardType} (₱{RewardCatalog.ValueOf(rewardType):N2} value) on appointment #{appointment.AppointmentID}.");

        await _audit.LogAsync("Create", "RewardRedemption",
            $"Redemption #{redemption.RedemptionID}: {User.Identity?.Name} spent {cost} pts on a free {rewardType} for '{pet!.PetName}' (appointment #{appointment.AppointmentID}).");

        var detailsUrl = $"/Appointments/Details/{appointment.AppointmentID}";
        await _notif.SendToRoleAsync("Clinic Staff", "Free Reward Service Requested 🎁",
            $"Pet Owner '{User.Identity?.Name}' redeemed {cost} points for a free {rewardType} for '{pet!.PetName}' on {appointment.AppointmentDate:g}.",
            "Appointment", detailsUrl);
        await _notif.SendAsync(vetId, "Free Reward Service Assigned 🎁",
            $"You have a free {rewardType} for '{pet.PetName}' on {appointment.AppointmentDate:g} — covered by the owner's loyalty points, no charge.",
            "Appointment", detailsUrl);
        await _notif.SendAsync(ownerId, "Reward Redeemed 🎁",
            $"You used {cost} loyalty points for a free {rewardType} for '{pet.PetName}' on {appointment.AppointmentDate:g}. The clinic will confirm your slot shortly.",
            "Appointment", detailsUrl);

        TempData["SuccessMessage"] = $"🎁 {cost} loyalty points redeemed! Your free {rewardType} for {pet.PetName} is booked and awaiting clinic confirmation.";
        return RedirectToAction("Details", "Appointments", new { id = appointment.AppointmentID });
    }

    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Index(string? status)
    {
        var query = _db.RewardRedemptions
            .Include(r => r.Owner)
            .Include(r => r.Appointment).ThenInclude(a => a!.Pet)
            .Include(r => r.Appointment).ThenInclude(a => a!.Vet)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            query = query.Where(r => r.Status == status);

        var redemptions = await query
            .OrderByDescending(r => r.RedeemedAt)
            .ToListAsync();

        ViewBag.Status = status;
        ViewData["Title"] = "Reward Redemptions";
        ViewData["DashTitle"] = "Loyalty Reward Redemptions";
        return View(redemptions);
    }

    private async Task<RedeemRewardViewModel> BuildRedeemViewModelAsync(string? rewardType)
    {
        var ownerId = User.GetUserId();
        var selected = RewardCatalog.IsEligible(rewardType) ? rewardType : null;

        var vets = await _db.Users
            .Where(u => u.Role == "Veterinarian" && u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new { u.UserID, u.Name })
            .ToListAsync();
        ViewBag.VetID = new SelectList(vets, "UserID", "Name");

        var pets = await _db.Pets
            .Where(p => p.OwnerID == ownerId)
            .OrderBy(p => p.PetName)
            .Select(p => new { p.PetID, Label = p.PetName + " (" + p.Species + ")" })
            .ToListAsync();
        ViewBag.PetID = new SelectList(pets, "PetID", "Label");

        return new RedeemRewardViewModel
        {
            Balance = await _loyalty.GetBalanceAsync(ownerId),
            RewardType = selected,
            Cost = selected != null ? RewardCatalog.CostFor(selected) : 0,
            HasPets = pets.Count > 0,
            Rewards = RewardCatalog.Available.Select(name => new RewardOption
            {
                Name = name,
                Cost = RewardCatalog.CostFor(name),
                Value = RewardCatalog.ValueOf(name)
            }).ToList()
        };
    }
}

public class RedeemRewardViewModel
{
    public int Balance { get; set; }
    public string? RewardType { get; set; }
    public int Cost { get; set; }
    public bool HasPets { get; set; }
    public List<RewardOption> Rewards { get; set; } = new();

    public bool CanAfford => RewardType != null && Balance >= Cost;
}

public class RewardOption
{
    public string Name { get; set; } = string.Empty;
    public int Cost { get; set; }
    public decimal Value { get; set; }
}
