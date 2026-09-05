namespace VetCare.Models;

public class Supplier
{
    public int SupplierID { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
    public string? ProductCatalog { get; set; }
    public string? ContractDetails { get; set; }

    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
}
