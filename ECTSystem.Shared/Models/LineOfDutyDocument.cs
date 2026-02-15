namespace ECTSystem.Shared.Models;

/// <summary>
/// Class representing a document or form associated with the LOD case.
/// </summary>
public class LineOfDutyDocument
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }
    public string DocumentType { get; set; } = string.Empty; // e.g., AF Form 348, DD Form 261, Medical Records
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty; // MIME type, e.g. "application/pdf"
    public long FileSize { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>(); // File bytes stored as varbinary(max)
    public DateTime? UploadDate { get; set; }
    public string Description { get; set; } = string.Empty;
}
