namespace VetCare.Models;

public class User
{
    public int UserID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ContactNumber { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<Pet> Pets { get; set; } = new List<Pet>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<Billing> Invoices { get; set; } = new List<Billing>();
    public ICollection<CrmRecord> CrmRecords { get; set; } = new List<CrmRecord>();
}
