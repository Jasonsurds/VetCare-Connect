namespace VetCare.Models;

public class Report
{
    public int ReportID { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public int GeneratedBy { get; set; }
    public DateTime DateGenerated { get; set; } = DateTime.Now;
    public string Content { get; set; } = string.Empty;

    public User? Generator { get; set; }
}
