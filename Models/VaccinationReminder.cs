namespace VetCare.Models;

public class VaccinationReminder
{
    public int ReminderID { get; set; }
    public int PetID { get; set; }
    public string VaccineName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "Pending";

    public Pet? Pet { get; set; }
}
