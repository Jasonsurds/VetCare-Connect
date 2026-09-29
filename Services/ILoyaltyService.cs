namespace VetCare.Services;

/// <summary>
/// Single entry point for the loyalty ledger. The balance of a pet owner is always the
/// sum of their <c>CrmRecord.LoyaltyPoints</c> rows, so earning and spending are both
/// written as new ledger rows here.
/// </summary>
public interface ILoyaltyService
{
    Task<int> GetBalanceAsync(int ownerId);
    Task AwardAsync(int ownerId, int points, string note);
    Task DeductAsync(int ownerId, int points, string note);
    Task RefundAsync(int redemptionId, string reason);
}
