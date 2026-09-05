namespace VetCare.Models;

public class Pet
{
    public int PetID { get; set; }
    public int OwnerID { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public int Age { get; set; }
    public string? MedicalHistory { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public User? Owner { get; set; }
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<VaccinationReminder> VaccinationReminders { get; set; } = new List<VaccinationReminder>();
}
