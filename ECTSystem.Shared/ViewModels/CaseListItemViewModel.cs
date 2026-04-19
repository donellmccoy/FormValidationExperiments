using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.ViewModels;

public class CaseListItemViewModel
{
    public int Id { get; set; }
    public string CaseId { get; set; }
    public string ServiceNumber { get; set; }
    public string MemberName { get; set; }
    public string MemberRank { get; set; }
    public string Unit { get; set; }
    public IncidentType IncidentType { get; set; }
    public DateTime IncidentDate { get; set; }
    public ProcessType ProcessType { get; set; }
    public WorkflowState CurrentWorkflowState { get; set; }
    public bool IsCheckedOut { get; set; }
    public string CheckedOutBy { get; set; }
    public string CheckedOutByName { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public bool IsBookmarked { get; set; }
    public bool IsAnimating { get; set; }
    public int? BookmarkId { get; set; }
}
