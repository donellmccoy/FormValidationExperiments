namespace ECTSystem.Web.Pages;

public partial class ValidatorDemo
{
    // ── Style 1: Pulse-Glow ──
    private string _demo1Value = string.Empty;

    // ── Style 2: Status Chips ──
    private string _demo2Diagnosis = string.Empty;

    // ── Style 3: Progress Rings ──
    private string _sectionALast = string.Empty;
    private string _sectionAFirst = "John";
    private string _sectionARank = string.Empty;
    private string _sectionADob = string.Empty;
    private double _sectionAPercent = 25;

    // ── Style 4: Validation Summary ──
    private bool _showSummary = true;

    // ── Style 5: Sidebar Badges ──
    private readonly List<SidebarStep> _sidebarSteps =
    [
        new("Member Information",  false, true,  3),
        new("Medical Technician",  false, true,  0),
        new("Medical Officer",     false, true,  0),
        new("Unit Commander",      true,  false, 2),
        new("Wing JA Review",      false, false, 0),
        new("Wing Commander",      false, false, 1),
        new("Appointing Authority", false, false, 0),
        new("Board Review",        false, false, 0),
    ];

    private void RecalcSectionA()
    {
        var filled = 0;
        if (!string.IsNullOrWhiteSpace(_sectionALast))  filled++;
        if (!string.IsNullOrWhiteSpace(_sectionAFirst)) filled++;
        if (!string.IsNullOrWhiteSpace(_sectionARank))  filled++;
        if (!string.IsNullOrWhiteSpace(_sectionADob))   filled++;
        _sectionAPercent = filled / 4.0 * 100;
    }

    private sealed record SidebarStep(
        string Label,
        bool Active,
        bool Complete,
        int Errors);
}
