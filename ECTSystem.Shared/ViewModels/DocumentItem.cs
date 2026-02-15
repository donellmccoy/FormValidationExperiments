namespace ECTSystem.Shared.ViewModels;

/// <summary>
/// Lightweight view model representing metadata for a supporting document
/// attached to an LOD case.
/// </summary>
public class DocumentItem
{
    /// <summary>
    /// Gets or sets the display name of the document.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the document type or category (e.g., "AF Form 348", "Medical Record").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date the document was uploaded or received.
    /// </summary>
    public string Date { get; set; } = string.Empty;
}
