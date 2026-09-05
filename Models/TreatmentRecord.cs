namespace VetCare.Models;

public class TreatmentRecord
{
    public int TreatmentID { get; set; }
    public int AppointmentID { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string? Prescription { get; set; }
    public string? TreatmentNotes { get; set; }
    public DateTime TreatmentDate { get; set; } = DateTime.Now;

    public Appointment? Appointment { get; set; }
}
