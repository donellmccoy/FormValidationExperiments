namespace FormValidationExperiments.Web.Models;

/// <summary>
/// Class representing a document or form associated with the LOD case.
/// </summary>
public class LODDocument
{
    public string DocumentType { get; set; } // e.g., AF Form 348, DD Form 261, Medical Records
    public string FileName { get; set; }
    public DateTime? UploadDate { get; set; }
    public string Description { get; set; }
}
