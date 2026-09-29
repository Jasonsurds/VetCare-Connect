using Microsoft.EntityFrameworkCore;
using VetCare.Data;
using VetCare.Models;

namespace VetCare.Services;

public class LoyaltyService : ILoyaltyService
{
    private readonly VetCareDbContext _db;

    public LoyaltyService(VetCareDbContext db) => _db = db;

    public async Task<int> GetBalanceAsync(int ownerId) =>
        await _db.CrmRecords
            .Where(c => c.OwnerID == ownerId)
            .SumAsync(c => (int?)c.LoyaltyPoints) ?? 0;

    public Task AwardAsync(int ownerId, int points, string note) =>
        points > 0 ? AddLedgerRowAsync(ownerId, points, note) : Task.CompletedTask;

    public Task DeductAsync(int ownerId, int points, string note) =>
        points > 0 ? AddLedgerRowAsync(ownerId, -points, note) : Task.CompletedTask;

    public async Task RefundAsync(int redemptionId, string reason)
    {
        var redemption = await _db.RewardRedemptions
            .FirstOrDefaultAsync(r => r.RedemptionID == redemptionId);

        // Only a still-booked redemption can be refunded; completed ones already delivered
        // the free service, and refunded ones would double-credit the owner.
        if (redemption == null || redemption.Status != "Booked") return;

        redemption.Status = "Refunded";
        await _db.SaveChangesAsync();

        await AddLedgerRowAsync(redemption.OwnerID, redemption.PointsSpent,
            $"Redeemed points returned ({redemption.PointsSpent} pts for a free {redemption.RewardType}). {reason}");
    }

    private async Task AddLedgerRowAsync(int ownerId, int points, string note)
    {
        _db.CrmRecords.Add(new CrmRecord
        {
            OwnerID = ownerId,
            Interaction = note,
            LoyaltyPoints = points,
            InteractionDate = DateTime.Now
        });
        await _db.SaveChangesAsync();
    }
}
