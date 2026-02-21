using System.Text.Json;
using System.Text.RegularExpressions;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using ECTSystem.Web.Services;
using ECTSystem.Shared.ViewModels;
using ECTSystem.Web.Shared;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;

namespace ECTSystem.Web.Pages;

public partial class EditCase : ComponentBase, IDisposable
{
    private static class TabNames
    {
        public const string MemberInformation = "Member Information";
        public const string MedicalTechnician = "Medical Technician";
        public const string MedicalOfficer = "Medical Officer";
        public const string UnitCommander = "Unit CC Review";
        public const string WingJudgeAdvocate = "Wing JA Review";
        public const string WingCommander = "Wing CC Review";
        public const string AppointingAuthority = "Appointing Authority";
        public const string BoardTechnicianReview = "Board Technician Review";
        public const string BoardMedicalReview = "Board Medical Review";
        public const string BoardLegalReview = "Board Legal Review";
        public const string BoardAdminReview = "Board Admin Review";
        public const string Draft = "Draft";
    }

    [Inject]
    private IDataService CaseService { get; set; }

    [Inject]
    private BookmarkCountService BookmarkCountService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    [Inject]
    private DialogService DialogService { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; }

    [Inject]
    private JsonSerializerOptions JsonOptions { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; }

    [Parameter]
    public string CaseId { get; set; }

    [SupplyParameterFromQuery(Name = "from")]
    public string FromPage { get; set; }

    private bool IsNewCase => string.IsNullOrEmpty(CaseId);
    private bool IsFromBookmarks => string.Equals(FromPage, "bookmarks", StringComparison.OrdinalIgnoreCase);

    private string _memberSearchText = string.Empty;

    private List<Member> _memberSearchResults = [];

    private bool _isMemberSearching;

    private CancellationTokenSource _searchCts = new();

    private RadzenTextBox _memberSearchTextBox;

    private Popup _memberSearchPopup;

    private RadzenDataGrid<Member> _memberSearchGrid;

    private int _memberSearchSelectedIndex;

    private System.Timers.Timer _debounceTimer;

    private readonly CancellationTokenSource _cts = new();

    private LineOfDutyCase _lodCase;

    private bool _isLoading = true;

    private bool _isSaving;

    private bool _isBusy;

    private string _busyMessage = string.Empty;

    private int _selectedTabIndex;

    private int _currentStepIndex;

    private int _selectedMemberId;

    private MemberInfoFormModel _memberFormModel = new();

    private MedicalAssessmentFormModel _medicalFormModel = new();

    private RadzenTemplateForm<MedicalAssessmentFormModel> _medicalForm;

    private UnitCommanderFormModel _commanderFormModel = new();

    private WingJudgeAdvocateFormModel _wingJAFormModel = new();

    private WingCommanderFormModel _wingCommanderFormModel = new();

    private MedicalTechnicianFormModel _medTechFormModel = new();

    private AppointingAuthorityFormModel _appointingAuthorityFormModel = new();

    private LineOfDutyBoardFormModel _boardFormModel = new();

    private CaseInfoModel _caseInfo = new()
    {
        CaseNumber = "Pending...",
        MemberName = "Pending...",
        Component = "Pending...",
        Rank = "Pending...",
        Grade = "Pending...",
        Unit = "Pending...",
        DateOfInjury = "Pending...",
        Status = "New"
    };

    private IReadOnlyList<TrackableModel> AllFormModels => [_memberFormModel, _medicalFormModel, _commanderFormModel, _wingJAFormModel, _wingCommanderFormModel, _medTechFormModel, _appointingAuthorityFormModel, _boardFormModel];

    private bool HasAnyChanges => AllFormModels.Any(m => m.IsDirty);

    private List<WorkflowStep> _workflowSteps = [];

    private WorkflowStep CurrentStep => _workflowSteps.Count > 0 ? _workflowSteps[_currentStepIndex] : null;

    protected override async Task OnInitializedAsync()
    {
        if (IsNewCase)
        {
            InitializeWorkflowSteps();
            TakeSnapshots();
        }
        else
        {
            await LoadCaseAsync();
        }

        _isLoading = false;
    }

    private async Task LoadCaseAsync()
    {
        await SetBusyAsync("Loading case...", true);

        try
        {
            _lodCase = await CaseService.GetCaseAsync(CaseId, _cts.Token);

            if (_lodCase is null)
            {
                InitializeWorkflowSteps();

                return;
            }

            var dto = LineOfDutyCaseMapper.ToCaseViewModelsDto(_lodCase);

            _caseInfo = dto.CaseInfo;
            _memberFormModel = dto.MemberInfo;
            _medicalFormModel = dto.MedicalAssessment;
            _commanderFormModel = dto.UnitCommander;
            _medTechFormModel = dto.MedicalTechnician;
            _wingJAFormModel = dto.WingJudgeAdvocate;
            _wingCommanderFormModel = dto.WingCommander;
            _appointingAuthorityFormModel = dto.AppointingAuthority;
            _boardFormModel = dto.BoardReview;

            _memberFormModel.Grade = _lodCase.MemberRank ?? string.Empty;

            InitializeWorkflowSteps();

            TakeSnapshots();

            // Check if this case is bookmarked
            if (_lodCase.Id > 0)
            {
                try
                {
                    _isBookmarked = await CaseService.IsBookmarkedAsync(_lodCase.Id, _cts.Token);
                }
                catch
                {
                    // Non-critical — default to not bookmarked
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Component disposed during load — silently ignore
        }
        catch (Exception ex)
        {
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Load Failed",
                Detail = $"Failed to load case: {ex.Message}",
                Duration = 5000
            });

            InitializeWorkflowSteps();
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private void TakeSnapshots()
    {
        foreach (var model in AllFormModels)
        {
            model.TakeSnapshot(JsonOptions);
        }
    }

    private void InitializeWorkflowSteps()
    {
        _workflowSteps =
        [
            new() { Number = 1,  Name = "Enter Member Information",  Icon = "flag",                 Status = WorkflowStepStatus.InProgress, Description = "Enter member identification and incident details to initiate the LOD case." },
            new() { Number = 2,  Name = "Medical Technician Review", Icon = "person",               Status = WorkflowStepStatus.Pending,    Description = "Medical technician reviews the injury/illness and documents clinical findings." },
            new() { Number = 3,  Name = "Medical Officer Review",    Icon = "medical_services",     Status = WorkflowStepStatus.Pending,    Description = "Medical officer reviews the technician's findings and provides a clinical assessment." },
            new() { Number = 4,  Name = "Unit CC Review",            Icon = "edit_document",        Status = WorkflowStepStatus.Pending,    Description = "Unit commander reviews the case and submits a recommendation for the LOD determination." },
            new() { Number = 5,  Name = "Wing JA Review",            Icon = "gavel",                Status = WorkflowStepStatus.Pending,    Description = "Wing Judge Advocate reviews the case for legal sufficiency and compliance." },
            new() { Number = 6,  Name = "Appointing Authority",       Icon = "verified_user",        Status = WorkflowStepStatus.Pending,    Description = "Appointing authority reviews the case and issues a formal LOD determination." },
            new() { Number = 7,  Name = "Wing CC Review",             Icon = "stars",                Status = WorkflowStepStatus.Pending,    Description = "Wing commander reviews the case and renders a preliminary LOD determination." },
            new() { Number = 8,  Name = "Board Technician Review",    Icon = "rate_review",          Status = WorkflowStepStatus.Pending,    Description = "Board medical technician reviews the case file for completeness and accuracy." },
            new() { Number = 9,  Name = "Board Medical Review",       Icon = "medical_services",     Status = WorkflowStepStatus.Pending,    Description = "Board medical officer reviews all medical evidence and provides a formal assessment." },
            new() { Number = 10, Name = "Board Legal Review",         Icon = "gavel",                Status = WorkflowStepStatus.Pending,    Description = "Board legal counsel reviews the case for legal sufficiency before final decision." },
            new() { Number = 11, Name = "Board Admin Review",         Icon = "admin_panel_settings", Status = WorkflowStepStatus.Pending,    Description = "Board administrative officer finalizes the case package and prepares the formal determination." },
            new() { Number = 12, Name = "Completed",                  Icon = "check_circle",         Status = WorkflowStepStatus.Pending,    Description = "LOD determination has been finalized and the case is closed." }
        ];
    }

    private async Task OnFormSubmit(MedicalAssessmentFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.MedicalTechnician);
    }

    private async Task OnMemberFormSubmit(MemberInfoFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.MemberInformation);
    }

    private async Task OnCommanderFormSubmit(UnitCommanderFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.UnitCommander);
    }

    private async Task OnWingJAFormSubmit(WingJudgeAdvocateFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.WingJudgeAdvocate);
    }

    private async Task OnWingCommanderFormSubmit(WingCommanderFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.WingCommander);
    }

    private async Task OnMedTechFormSubmit(MedicalTechnicianFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.MedicalTechnician);
    }

    private async Task OnAppointingAuthorityFormSubmit(AppointingAuthorityFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.AppointingAuthority);
    }

    private async Task OnBoardFormSubmit(LineOfDutyBoardFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.BoardTechnicianReview);
    }

    private bool _isBookmarked;
    private bool _bookmarkAnimating;

    private string BookmarkIcon => _bookmarkAnimating ? "bookmark_added" : _isBookmarked ? "bookmark_remove" : "bookmark_add";

    private async Task OnBookmarkClick()
    {
        if (_lodCase?.Id is null or 0)
            return;

        _isBookmarked = !_isBookmarked;

        if (_isBookmarked)
        {
            _bookmarkAnimating = true;
            StateHasChanged();

            try
            {
                await CaseService.AddBookmarkAsync(_lodCase.Id, _cts.Token);
                await BookmarkCountService.RefreshAsync(_cts.Token);
            }
            catch
            {
                _isBookmarked = false; // Revert on failure
            }

            await Task.Delay(800);
            _bookmarkAnimating = false;
        }
        else
        {
            try
            {
                await CaseService.RemoveBookmarkAsync(_lodCase.Id, _cts.Token);
                await BookmarkCountService.RefreshAsync(_cts.Token);
                NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {_caseInfo?.CaseNumber} removed from bookmarks.", closeOnClick: true);
            }
            catch
            {
                _isBookmarked = true; // Revert on failure
            }
        }
    }

    private async Task OnAttachFileClick()
    {
        // TODO: Implement file attachment dialog/upload
        await Task.CompletedTask;
    }

    private void OnStepSelected(WorkflowStep step)
    {
        _currentStepIndex = step.Number - 1;

        foreach (var s in _workflowSteps)
        {
            if (s.Number < step.Number)
            {
                s.Status = WorkflowStepStatus.Completed;
                if (string.IsNullOrEmpty(s.StatusText))
                {
                    s.StatusText = "Completed";
                }

                if (string.IsNullOrEmpty(s.CompletionDate))
                {
                    s.CompletionDate = DateTime.Now.ToString("MM/dd/yyyy h:mm tt");
                }
            }
            else if (s.Number == step.Number)
            {
                s.Status = WorkflowStepStatus.InProgress;
            }
            else
            {
                s.Status = WorkflowStepStatus.Pending;
                s.StatusText = string.Empty;
                s.CompletionDate = string.Empty;
            }
        }

        _selectedTabIndex = 0;
    }

    private async Task OnStartLod()
    {
        var confirmed = await DialogService.Confirm(
            "Are you sure you want to start the LOD process?",
            "Start LOD",
            new ConfirmOptions { OkButtonText = "Start", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Creating LOD case...");

        try
        {
            var newCase = new LineOfDutyCase
            {
                CaseId = $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                MemberId = _selectedMemberId,
                InitiationDate = DateTime.UtcNow,
                IncidentDate = DateTime.UtcNow
            };

            LineOfDutyCaseMapper.ApplyMemberInfo(_memberFormModel, newCase);

            var saved = await CaseService.SaveCaseAsync(newCase, _cts.Token);

            _lodCase = saved;
            CaseId = saved.CaseId;
            _caseInfo = LineOfDutyCaseMapper.ToCaseInfoModel(saved);

            TakeSnapshots();

            NotificationService.Notify(NotificationSeverity.Success, "LOD Started",$"Case {saved.CaseId} created for {saved.MemberName}.");

            Navigation.NavigateTo($"/case/{saved.CaseId}", replace: true);
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Create Failed", ex.Message);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task OnSplitButtonClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "revert")
        {
            await OnRevertChanges();

            return;
        }

        // Validate the medical tab before saving
        if (_selectedTabIndex == 1 && _medicalForm?.EditContext?.Validate() == false)
        {
            return;
        }

        // Determine which tab to save based on the currently selected tab index
        var source = _selectedTabIndex switch
        {
            0 => TabNames.MemberInformation,
            1 => TabNames.MedicalTechnician,
            2 => TabNames.MedicalOfficer,
            3 => TabNames.UnitCommander,
            4 => TabNames.WingJudgeAdvocate,
            5 => TabNames.WingCommander,
            6 => TabNames.AppointingAuthority,
            7 => TabNames.BoardTechnicianReview,
            _ => TabNames.Draft
        };

        var confirmed = await DialogService.Confirm(
            "Are you sure you want to save?",
            "Confirm Save",
            new ConfirmOptions { OkButtonText = "Save", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        await SaveCurrentTabAsync(source);
    }

    private async Task OnMedTechForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "cancel")
        {
            await CancelInvestigationAsync();
        }
        else
        {
            await ConfirmAndNotifyAsync(
                "Are you sure you want to forward this case to the Medical Officer?",
                "Confirm Forward", "Forward",
                "Forwarding to Medical Officer...",
                NotificationSeverity.Success, "Forwarded to Medical Officer",
                "Case has been forwarded to the Medical Officer for review.");
        }
    }

    private async Task OnMedicalForwardClick(RadzenSplitButtonItem item)
    {
        switch (item?.Value)
        {
            case "return":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Medical Technician?",
                    "Confirm Return", "Return",
                    "Returning to Med Tech...",
                    NotificationSeverity.Info, "Returned to Med Tech",
                    "Case has been returned to the Medical Technician for review.");
                break;
            case "cancel":
                await CancelInvestigationAsync();
                break;
            default:
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to forward this case to the Unit Commander?",
                    "Confirm Forward", "Forward",
                    "Forwarding to Unit CC...",
                    NotificationSeverity.Success, "Forwarded to Unit CC",
                    "Case has been forwarded to the Unit Commander.");
                break;
        }
    }

    private async Task OnCommanderForwardClick(RadzenSplitButtonItem item)
    {
        switch (item?.Value)
        {
            case "return-med-officer":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Medical Officer?",
                    "Confirm Return", "Return",
                    "Returning to Medical Officer...",
                    NotificationSeverity.Info, "Returned to Medical Officer",
                    "Case has been returned to the Medical Officer for review.");
                break;
            case "return-med-tech":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Medical Technician?",
                    "Confirm Return", "Return",
                    "Returning to Medical Technician...",
                    NotificationSeverity.Info, "Returned to Medical Technician",
                    "Case has been returned to the Medical Technician for review.");
                break;
            case "cancel":
                await CancelInvestigationAsync();
                break;
            default:
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to forward this case to the Wing Judge Advocate?",
                    "Confirm Forward", "Forward",
                    "Forwarding to Wing JA...",
                    NotificationSeverity.Success, "Forwarded to Wing JA",
                    "Case has been forwarded to the Wing Judge Advocate.");
                break;
        }
    }

    private async Task OnWingJAForwardClick(RadzenSplitButtonItem item)
    {
        switch (item?.Value)
        {
            case "return-unit-cc":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Unit Commander?",
                    "Confirm Return", "Return",
                    "Returning to Unit CC...",
                    NotificationSeverity.Info, "Returned to Unit CC",
                    "Case has been returned to the Unit Commander for review.");
                break;
            case "return-med-officer":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Medical Officer?",
                    "Confirm Return", "Return",
                    "Returning to Medical Officer...",
                    NotificationSeverity.Info, "Returned to Medical Officer",
                    "Case has been returned to the Medical Officer for review.");
                break;
            case "return-med-tech":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Medical Technician?",
                    "Confirm Return", "Return",
                    "Returning to Medical Technician...",
                    NotificationSeverity.Info, "Returned to Medical Technician",
                    "Case has been returned to the Medical Technician for review.");
                break;
            case "cancel":
                await CancelInvestigationAsync();
                break;
            default:
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to forward this case to the Wing Commander?",
                    "Confirm Forward", "Forward",
                    "Forwarding to Wing CC...",
                    NotificationSeverity.Success, "Forwarded to Wing CC",
                    "Case has been forwarded to the Wing Commander.");
                break;
        }
    }

    private async Task OnLegalForwardClick(RadzenSplitButtonItem item)
    {
        switch (item?.Value)
        {
            case "return-wing-ja":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Wing Judge Advocate?",
                    "Confirm Return", "Return",
                    "Returning to Wing JA...",
                    NotificationSeverity.Info, "Returned to Wing JA",
                    "Case has been returned to the Wing Judge Advocate for review.");
                break;
            case "return":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Unit Commander?",
                    "Confirm Return", "Return",
                    "Returning to Unit CC...",
                    NotificationSeverity.Info, "Returned to Unit CC",
                    "Case has been returned to the Unit Commander for review.");
                break;
            case "return-med-officer":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Medical Officer?",
                    "Confirm Return", "Return",
                    "Returning to Medical Officer...",
                    NotificationSeverity.Info, "Returned to Medical Officer",
                    "Case has been returned to the Medical Officer for review.");
                break;
            case "return-med-tech":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Medical Technician?",
                    "Confirm Return", "Return",
                    "Returning to Medical Technician...",
                    NotificationSeverity.Info, "Returned to Medical Technician",
                    "Case has been returned to the Medical Technician for review.");
                break;
            case "cancel":
                await CancelInvestigationAsync();
                break;
            default:
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to forward this case to the Wing Commander?",
                    "Confirm Forward", "Forward",
                    "Forwarding to Wing CC...",
                    NotificationSeverity.Success, "Forwarded to Wing CC",
                    "Case has been forwarded to the Wing Commander.");
                break;
        }
    }

    private async Task OnWingForwardClick(RadzenSplitButtonItem item)
    {
        switch (item?.Value)
        {
            case "return-wing-cc":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Wing Commander?",
                    "Confirm Return", "Return",
                    "Returning to Wing CC...",
                    NotificationSeverity.Info, "Returned to Wing CC",
                    "Case has been returned to the Wing Commander for review.");
                break;
            case "return-wing-ja":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Wing Judge Advocate?",
                    "Confirm Return", "Return",
                    "Returning to Wing JA...",
                    NotificationSeverity.Info, "Returned to Wing JA",
                    "Case has been returned to the Wing Judge Advocate for review.");
                break;
            case "return-unit-cc":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Unit Commander?",
                    "Confirm Return", "Return",
                    "Returning to Unit CC...",
                    NotificationSeverity.Info, "Returned to Unit CC",
                    "Case has been returned to the Unit Commander for review.");
                break;
            case "return-med-officer":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Medical Officer?",
                    "Confirm Return", "Return",
                    "Returning to Medical Officer...",
                    NotificationSeverity.Info, "Returned to Medical Officer",
                    "Case has been returned to the Medical Officer for review.");
                break;
            case "return-med-tech":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Medical Technician?",
                    "Confirm Return", "Return",
                    "Returning to Medical Technician...",
                    NotificationSeverity.Info, "Returned to Medical Technician",
                    "Case has been returned to the Medical Technician for review.");
                break;
            case "cancel":
                await CancelInvestigationAsync();
                break;
            default:
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to forward this case to the Board for review?",
                    "Confirm Forward", "Forward",
                    "Forwarding to Board Review...",
                    NotificationSeverity.Success, "Forwarded to Board Review",
                    "Case has been forwarded to the Board for review.");
                break;
        }
    }

    private async Task OnBoardCompleteClick(RadzenSplitButtonItem item)
    {
        switch (item?.Value)
        {
            case "return-appointing-authority":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Appointing Authority?",
                    "Confirm Return", 
                    "Return",
                    "Returning to Appointing Authority...",
                    NotificationSeverity.Info, 
                    "Returned to Appointing Authority",
                    "Case has been returned to the Appointing Authority for review.");
                break;
            case "return-wing-cc":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Wing Commander?",
                    "Confirm Return", 
                    "Return",
                    "Returning to Wing CC...",
                    NotificationSeverity.Info, 
                    "Returned to Wing CC",
                    "Case has been returned to the Wing Commander for review.");
                break;
            case "return-wing-ja":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Wing Judge Advocate?",
                    "Confirm Return", 
                    "Return",
                    "Returning to Wing JA...",
                    NotificationSeverity.Info, 
                    "Returned to Wing JA",
                    "Case has been returned to the Wing Judge Advocate for review.");
                break;
            case "return-unit-cc":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Unit Commander?",
                    "Confirm Return", 
                    "Return",
                    "Returning to Unit CC...",
                    NotificationSeverity.Info, 
                    "Returned to Unit CC",
                    "Case has been returned to the Unit Commander for review.");
                break;
            case "return-med-officer":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Medical Officer?",
                    "Confirm Return", 
                    "Return",
                    "Returning to Medical Officer...",
                    NotificationSeverity.Info, 
                    "Returned to Medical Officer",
                    "Case has been returned to the Medical Officer for review.");
                break;
            case "return-med-tech":
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to return this case to the Medical Technician?",
                    "Confirm Return", 
                    "Return",
                    "Returning to Medical Technician...",
                    NotificationSeverity.Info, 
                    "Returned to Medical Technician",
                    "Case has been returned to the Medical Technician for review.");
                break;
            case "cancel":
                await CancelInvestigationAsync();
                break;
            default:
                await ConfirmAndNotifyAsync(
                    "Are you sure you want to complete the Board review?",
                    "Confirm Complete", 
                    "Complete",
                    "Completing Board review...",
                    NotificationSeverity.Success, 
                    "Review Completed",
                    "The Board review has been completed.");
                break;
        }
    }

    private async Task ConfirmAndNotifyAsync(
        string confirmMessage, 
        string confirmTitle, 
        string okButtonText,
        string busyMessage, 
        NotificationSeverity severity,
        string notifySummary, 
        string notifyDetail,
        string cancelButtonText = "Cancel")
    {
        var confirmed = await DialogService.Confirm(confirmMessage, confirmTitle,
            new ConfirmOptions { OkButtonText = okButtonText, CancelButtonText = cancelButtonText });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync(busyMessage);

        try
        {
            NotificationService.Notify(severity, notifySummary, notifyDetail);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private Task CancelInvestigationAsync()
    {
        return ConfirmAndNotifyAsync(
            "Are you sure you want to cancel this investigation?",
            "Confirm Cancellation", 
            "Yes, Cancel",
            "Cancelling investigation...",
            NotificationSeverity.Warning, 
            "Investigation Cancelled",
            "The LOD investigation has been cancelled.", 
            "No");
    }

    private async Task OnDigitallySign()
    {
        var confirmed = await DialogService.Confirm(
            "Are you sure you want to digitally sign this section?",
            "Confirm Digital Signature",
            new ConfirmOptions { OkButtonText = "Sign", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Applying digital signature...");

        try
        {
            // TODO: persist digital signature to database

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Digitally Signed",
                Detail = "Section has been digitally signed.",
                Duration = 3000
            });
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task SaveCurrentTabAsync(string source)
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        await SetBusyAsync("Saving...");

        try
        {
            // Apply only the specific tab's view model to the entity
            switch (source)
            {
                case TabNames.MemberInformation:
                    LineOfDutyCaseMapper.ApplyMemberInfo(_memberFormModel, _lodCase);
                    break;
                case TabNames.MedicalTechnician:
                    LineOfDutyCaseMapper.ApplyMedicalAssessment(_medicalFormModel, _lodCase);
                    break;
                case TabNames.UnitCommander:
                    LineOfDutyCaseMapper.ApplyUnitCommander(_commanderFormModel, _lodCase);
                    break;
                case TabNames.WingJudgeAdvocate:
                    LineOfDutyCaseMapper.ApplyWingJudgeAdvocate(_wingJAFormModel, _lodCase);
                    break;
                case TabNames.WingCommander:
                    LineOfDutyCaseMapper.ApplyWingCommander(_wingCommanderFormModel, _lodCase);
                    break;
                case TabNames.AppointingAuthority:
                    LineOfDutyCaseMapper.ApplyAppointingAuthority(_appointingAuthorityFormModel, _lodCase);
                    break;
                case TabNames.BoardTechnicianReview:
                    LineOfDutyCaseMapper.ApplyBoardReview(_boardFormModel, _lodCase);
                    break;
                default:
                    LineOfDutyCaseMapper.ApplyAll(
                        new CaseViewModelsDto
                        {
                            CaseInfo = _caseInfo,
                            MemberInfo = _memberFormModel,
                            MedicalAssessment = _medicalFormModel,
                            UnitCommander = _commanderFormModel,
                            MedicalTechnician = _medTechFormModel,
                            WingJudgeAdvocate = _wingJAFormModel,
                            WingCommander = _wingCommanderFormModel,
                            AppointingAuthority = _appointingAuthorityFormModel,
                            BoardReview = _boardFormModel
                        },
                        _lodCase);
                    break;
            }

            // Save the entity
            _lodCase = await CaseService.SaveCaseAsync(_lodCase, _cts.Token);

            // Refresh the read-only case info from the saved entity
            _caseInfo = LineOfDutyCaseMapper.ToCaseInfoModel(_lodCase);

            // Re-snapshot only the saved model so other tabs retain their dirty state
            switch (source)
            {
                case TabNames.MemberInformation:
                    _memberFormModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.MedicalTechnician:
                    _medicalFormModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.UnitCommander:
                    _commanderFormModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.WingJudgeAdvocate:
                    _wingJAFormModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.WingCommander:
                    _wingCommanderFormModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.AppointingAuthority:
                    _appointingAuthorityFormModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.BoardTechnicianReview:
                    _boardFormModel.TakeSnapshot(JsonOptions);
                    break;
                default:
                    TakeSnapshots();
                    break;
            }

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Saved",
                Detail = $"{source} data saved successfully.",
                Duration = 3000
            });
        }
        catch (OperationCanceledException)
        {
            // Component disposed during save — silently ignore
        }
        catch (Exception ex)
        {
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Save Failed",
                Detail = ex.Message,
                Duration = 5000
            });
        }
        finally
        {
            _isSaving = false;
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task SetBusyAsync(string message = "Working...", bool? isBusy = true)
    {
        _busyMessage = message;
        _isBusy = isBusy.GetValueOrDefault(true);
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnRevertChanges()
    {
        var confirmed = await DialogService.Confirm(
            "Revert all unsaved changes? This cannot be undone.",
            "Confirm Revert",
            new ConfirmOptions { OkButtonText = "Revert", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        foreach (var model in AllFormModels)
        {
            model.Revert();
        }

        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Info,
            Summary = "Reverted",
            Detail = "All unsaved changes have been reverted.",
            Duration = 3000
        });

        StateHasChanged();
    }

    private void OnIsMilitaryFacilityChanged()
    {
        if (_medicalFormModel.IsMilitaryFacility != true)
        {
            _medicalFormModel.TreatmentFacilityName = null;
        }
    }

    private void OnWasUnderInfluenceChanged()
    {
        if (_medicalFormModel.WasUnderInfluence != true)
        {
            _medicalFormModel.SubstanceType = null;
        }
    }

    private void OnToxicologyTestDoneChanged()
    {
        if (_medicalFormModel.ToxicologyTestDone != true)
        {
            _medicalFormModel.ToxicologyTestResults = null;
        }
    }

    private void OnPsychiatricEvalCompletedChanged()
    {
        if (_medicalFormModel.PsychiatricEvalCompleted != true)
        {
            _medicalFormModel.PsychiatricEvalDate = null;
            _medicalFormModel.PsychiatricEvalResults = null;
        }
    }

    private void OnOtherTestsDoneChanged()
    {
        if (_medicalFormModel.OtherTestsDone != true)
        {
            _medicalFormModel.OtherTestDate = null;
            _medicalFormModel.OtherTestResults = null;
        }
    }

    private void OnIsEptsNsaChanged()
    {
        if (_medicalFormModel.IsEptsNsa != true)
        {
            _medicalFormModel.IsServiceAggravated = null;
        }
    }

    private void OnIsAtDeployedLocationChanged()
    {
        if (_medicalFormModel.IsAtDeployedLocation != false)
        {
            _medicalFormModel.RequiresArcBoard = null;
        }
    }

    private async Task OnMemberSearchKeyDown(KeyboardEventArgs args)
    {
        var items = _memberSearchResults;
        var popupOpened = await JSRuntime.InvokeAsync<bool>("Radzen.popupOpened", "member-search-popup");
        var key = args.Code ?? args.Key;

        if (!args.AltKey && (key == "ArrowDown" || key == "ArrowUp"))
        {
            var result = await JSRuntime.InvokeAsync<int[]>("Radzen.focusTableRow", "member-search-grid", key, _memberSearchSelectedIndex, null, false);
            _memberSearchSelectedIndex = result.First();
        }
        else if (args.AltKey && key == "ArrowDown" || key == "Enter" || key == "NumpadEnter")
        {
            if (popupOpened && (key == "Enter" || key == "NumpadEnter"))
            {
                var selected = items.ElementAtOrDefault(_memberSearchSelectedIndex);
                if (selected != null)
                {
                    await OnMemberSelected(selected);
                    return;
                }
            }

            await _memberSearchPopup.ToggleAsync(_memberSearchTextBox.Element);
        }
        else if (key == "Escape" || key == "Tab")
        {
            await _memberSearchPopup.CloseAsync();
        }
    }

    private async Task OnMemberSearchInput(ChangeEventArgs args)
    {
        _memberSearchSelectedIndex = 0;
        _memberSearchText = args.Value?.ToString() ?? string.Empty;

        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();

        if (string.IsNullOrWhiteSpace(_memberSearchText))
        {
            _memberSearchResults = [];

            StateHasChanged();

            return;
        }

        _debounceTimer = new System.Timers.Timer(300)
        {
            AutoReset = false
        };

        _debounceTimer.Elapsed += async (_, _) =>
        {
            await InvokeAsync(async () =>
            {
                await _searchCts.CancelAsync();

                _searchCts.Dispose();
                _searchCts = new CancellationTokenSource();

                _isMemberSearching = true;
                StateHasChanged();

                try
                {
                    _memberSearchResults = await CaseService.SearchMembersAsync(_memberSearchText, _searchCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Search was superseded — ignore
                }
                catch (Exception ex)
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Search Failed", ex.Message);
                    _memberSearchResults = [];
                }
                finally
                {
                    _isMemberSearching = false;

                    StateHasChanged();
                }
            });
        };

        _debounceTimer.Start();
    }

    private async Task OnMemberSelected(Member member)
    {
        _memberSearchText = string.Empty;
        await _memberSearchPopup.CloseAsync();

        _selectedMemberId = member.Id;

        _memberFormModel.FirstName = member.FirstName;
        _memberFormModel.LastName = member.LastName;
        _memberFormModel.MiddleInitial = member.MiddleInitial;
        _memberFormModel.OrganizationUnit = member.Unit;
        _memberFormModel.SSN = member.ServiceNumber;
        _memberFormModel.DateOfBirth = member.DateOfBirth;
        _memberFormModel.Component = Regex.Replace(member.Component.ToString(), "(\\B[A-Z])", " $1");

        var parsedRank = LineOfDutyCaseMapper.ParseMilitaryRank(member.Rank);
        _memberFormModel.Rank = parsedRank.HasValue ? LineOfDutyCaseMapper.FormatRankToFullName(parsedRank.Value) : member.Rank;
        _memberFormModel.Grade = parsedRank.HasValue ? LineOfDutyCaseMapper.FormatRankToPayGrade(parsedRank.Value): member.Rank;

        _caseInfo.MemberName = $"{_memberFormModel.LastName}, {_memberFormModel.FirstName}";
        _caseInfo.Component = _memberFormModel.Component;
        _caseInfo.Rank = _memberFormModel.Rank;
        _caseInfo.Grade = _memberFormModel.Grade;
        _caseInfo.Unit = _memberFormModel.OrganizationUnit;

        _selectedTabIndex = 0;
        StateHasChanged();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();
        _searchCts.Cancel();
        _searchCts.Dispose();
    }
}