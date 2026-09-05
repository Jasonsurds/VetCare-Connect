namespace VetCare.Models;

public class InventoryItem
{
    public int ItemID { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int ReorderLevel { get; set; }
    public int? SupplierID { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    public Supplier? Supplier { get; set; }
}
