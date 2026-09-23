namespace VetCare.Models;

public class BillingItem
{
    public int BillingItemID { get; set; }
    public int InvoiceID { get; set; }
    public int InventoryItemID { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;

    public Billing? Billing { get; set; }
    public InventoryItem? InventoryItem { get; set; }
}