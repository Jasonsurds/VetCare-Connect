namespace VetCare.Services;

/// <summary>
/// Services a pet owner can trade loyalty points for. The keys must match
/// <see cref="ServiceFees.Fees"/> and <c>AppointmentsController.ServiceTypes</c> so the
/// redeemed appointment prices correctly and appears in the normal booking dropdowns.
/// </summary>
public static class RewardCatalog
{
    public static readonly Dictionary<string, int> Costs = new()
    {
        ["Grooming"] = 15,
        ["Dental Cleaning"] = 20
    };

    public static List<string> Available => Costs.Keys.OrderBy(k => Costs[k]).ToList();

    public static bool IsEligible(string? serviceType) =>
        serviceType != null && Costs.ContainsKey(serviceType);

    public static int CostFor(string serviceType) =>
        Costs.TryGetValue(serviceType, out var cost) ? cost : 0;

    public static decimal ValueOf(string serviceType) => ServiceFees.GetFee(serviceType);

    public static int Cheapest() => Costs.Values.Min();
}
