namespace VetCare.Services;

/// <summary>Standard consultation fees used to auto-generate bills when a consultation is completed.</summary>
public static class ServiceFees
{
    public static readonly Dictionary<string, decimal> Fees = new()
    {
        ["General Checkup"] = 500m,
        ["Vaccination"] = 450m,
        ["Dental Cleaning"] = 1500m,
        ["Grooming"] = 400m,
        ["Follow-up"] = 300m,
        ["Surgery Consult"] = 800m
    };

    public static decimal GetFee(string serviceType) =>
        Fees.TryGetValue(serviceType, out var fee) ? fee : 500m;
}
