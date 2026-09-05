namespace VetCare.Models;

public class Appointment
{
    public int AppointmentID { get; set; }
    public int PetID { get; set; }
    public int VetID { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string ServiceType { get; set; } = "General Checkup";
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public Pet? Pet { get; set; }
    public User? Vet { get; set; }
    public TreatmentRecord? TreatmentRecord { get; set; }
    public Billing? Billing { get; set; }
}
