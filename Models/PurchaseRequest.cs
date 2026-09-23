namespace VetCare.Models;

public class PurchaseRequest
{
    public int RequestID { get; set; }
    public int InventoryItemID { get; set; }
    public int RequestedBy { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.Now;
    public DateTime? ProcessedAt { get; set; }
    public int? ProcessedBy { get; set; }

    public InventoryItem? InventoryItem { get; set; }
    public User? Requester { get; set; }
    public User? Processor { get; set; }
}