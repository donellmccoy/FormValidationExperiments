using AirForceLODSystem;

namespace FormValidationExperiments.Web.Pages;

public partial class Workflow
{
    private bool isLoading = true;
    private int selectedStepIndex;
    private int step1TabIndex;
    private int step2TabIndex;
    private int step3TabIndex;
    private int step4TabIndex;
    private int step5TabIndex;

    private LODCase lodCase = new()
    {
        MEDCON = new MEDCONDetails(),
        INCAP = new INCAPDetails(),
        TimelineSteps = new List<TimelineStep>(),
        Authorities = new List<LODAuthority>(),
        Documents = new List<LODDocument>(),
        Appeals = new List<LODAppeal>(),
        WitnessStatements = new List<string>(),
        AuditComments = new List<string>()
    };

    // Form entry models for add-item tabs
    private LODDocument currentDocument = new();
    private CommentModel currentComment = new();
    private LODAppeal currentAppeal = new();
    private TimelineStep currentTimelineStep = new();

    // Dropdown data sources
    private List<DropdownItem<LODProcessType>> processTypes;
    private List<DropdownItem<ServiceComponent>> serviceComponents;
    private List<DropdownItem<IncidentType>> incidentTypes;
    private List<DropdownItem<DutyStatus>> dutyStatuses;
    private List<DropdownItem<LODFinding>> lodFindings;

    protected override async Task OnInitializedAsync()
    {
        InitDropdowns();
        await Task.Delay(800);
        isLoading = false;
    }

    private void InitDropdowns()
    {
        processTypes = Enum.GetValues<LODProcessType>()
            .Select(v => new DropdownItem<LODProcessType> { Value = v, Text = FormatEnum(v) })
            .ToList();

        serviceComponents = Enum.GetValues<ServiceComponent>()
            .Select(v => new DropdownItem<ServiceComponent> { Value = v, Text = FormatEnum(v) })
            .ToList();

        incidentTypes = Enum.GetValues<IncidentType>()
            .Select(v => new DropdownItem<IncidentType> { Value = v, Text = FormatEnum(v) })
            .ToList();

        dutyStatuses = Enum.GetValues<DutyStatus>()
            .Select(v => new DropdownItem<DutyStatus> { Value = v, Text = FormatEnum(v) })
            .ToList();

        lodFindings = Enum.GetValues<LODFinding>()
            .Select(v => new DropdownItem<LODFinding> { Value = v, Text = FormatEnum(v) })
            .ToList();
    }

    private void OnSubmit(LODCase model)
    {
        // Handle main form submission
    }

    private void OnStepChange(int index)
    {
        selectedStepIndex = index;
    }

    private void OnDocumentSubmit(LODDocument model)
    {
        lodCase.Documents.Add(new LODDocument
        {
            DocumentType = model.DocumentType,
            FileName = model.FileName,
            UploadDate = model.UploadDate,
            Description = model.Description
        });
        currentDocument = new LODDocument();
    }

    private void OnCommentSubmit(CommentModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.Text))
        {
            lodCase.AuditComments.Add(model.Text);
            currentComment = new CommentModel();
        }
    }

    private void OnAppealSubmit(LODAppeal model)
    {
        lodCase.Appeals.Add(new LODAppeal
        {
            AppealDate = model.AppealDate,
            Appellant = model.Appellant,
            OriginalFinding = model.OriginalFinding,
            AppealOutcome = model.AppealOutcome,
            ResolutionDate = model.ResolutionDate
        });
        currentAppeal = new LODAppeal();
    }

    private void OnTimelineStepSubmit(TimelineStep model)
    {
        lodCase.TimelineSteps.Add(new TimelineStep
        {
            StepDescription = model.StepDescription,
            TimelineDays = model.TimelineDays,
            StartDate = model.StartDate,
            CompletionDate = model.CompletionDate,
            IsOptional = model.IsOptional
        });
        currentTimelineStep = new TimelineStep();
    }

    private void OnMedconSubmit(MEDCONDetails model)
    {
        // MEDCON is already bound to lodCase.MEDCON
    }

    private void OnIncapSubmit(INCAPDetails model)
    {
        // INCAP is already bound to lodCase.INCAP
    }

    private void OnReset()
    {
        lodCase = new LODCase
        {
            MEDCON = new MEDCONDetails(),
            INCAP = new INCAPDetails(),
            TimelineSteps = new List<TimelineStep>(),
            Authorities = new List<LODAuthority>(),
            Documents = new List<LODDocument>(),
            Appeals = new List<LODAppeal>(),
            WitnessStatements = new List<string>(),
            AuditComments = new List<string>()
        };
    }

    private static string FormatEnum<T>(T value) where T : Enum
    {
        var name = value.ToString();
        return System.Text.RegularExpressions.Regex.Replace(name, "(?<!^)([A-Z])", " $1");
    }

    // Helper class for comment form binding
    public class CommentModel
    {
        public string Text { get; set; }
    }

    // Helper class for dropdown binding
    public class DropdownItem<T>
    {
        public T Value { get; set; }
        public string Text { get; set; }
    }
}
