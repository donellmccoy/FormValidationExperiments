namespace ECTSystem.Shared.Models;

public class WitnessStatement
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }
    public string Text { get; set; } = string.Empty;
}
