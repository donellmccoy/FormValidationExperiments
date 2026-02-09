using AirForceLODSystem;

namespace FormValidationExperiments.Web.Pages;

public partial class Home
{
    public class LegalSJAReviewFormModel
    {
        // ── Item 24: Legal Sufficiency Review ──
        public bool? IsLegallySufficient { get; set; }
        public bool? ConcurWithRecommendation { get; set; }
        public string NonConcurrenceReason { get; set; } = string.Empty;

        // ── Item 25: Legal Remarks ──
        public string LegalRemarks { get; set; } = string.Empty;

        // ── SJA Signature Block ──
        public string SJAName { get; set; } = string.Empty;
        public MilitaryRank? SJARank { get; set; }
        public string SJAOrganization { get; set; } = string.Empty;
        public DateTime? SJASignatureDate { get; set; }
    }
}
