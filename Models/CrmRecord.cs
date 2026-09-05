namespace VetCare.Models;

public class CrmRecord
{
    public int CRMID { get; set; }
    public int OwnerID { get; set; }
    public string? Interaction { get; set; }
    public string? Feedback { get; set; }
    public int LoyaltyPoints { get; set; }
    public DateTime InteractionDate { get; set; } = DateTime.Now;

    public User? Owner { get; set; }
}
