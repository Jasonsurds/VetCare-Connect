namespace VetCare.Models;

/// <summary>
/// A pet owner spending loyalty points for a free service. The linked appointment is
/// billed at zero once the assigned veterinarian completes it. Cancelling or deleting
/// that appointment refunds the points and flips the status to "Refunded".
/// </summary>
public class RewardRedemption
{
    public int RedemptionID { get; set; }
    public int OwnerID { get; set; }
    public string RewardType { get; set; } = string.Empty;
    public int PointsSpent { get; set; }
    public decimal ServiceValue { get; set; }
    public string Status { get; set; } = "Booked"; // Booked | Completed | Refunded
    public int? AppointmentID { get; set; }
    public DateTime RedeemedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }

    public User? Owner { get; set; }
    public Appointment? Appointment { get; set; }
}
