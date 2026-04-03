namespace ECTSystem.Shared.AlternateModels;

/// <summary>
/// Reusable signature block for Parts II–VII of AF Form 348.
/// </summary>
public class SignatureBlockSection
{
    public DateTime? SignatureDate { get; set; }
    public string NameAndRank { get; set; }
    public string Signature { get; set; }
}
