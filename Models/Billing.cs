namespace VetCare.Models;

public class Billing
{
    public int InvoiceID { get; set; }
    public int AppointmentID { get; set; }
    public int OwnerID { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string PaymentStatus { get; set; } = "Pending";
    public DateTime DateIssued { get; set; } = DateTime.Now;

    public Appointment? Appointment { get; set; }
    public User? Owner { get; set; }
}
