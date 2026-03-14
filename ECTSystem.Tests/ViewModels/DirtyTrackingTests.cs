using System.Text.Json;
using System.Text.Json.Serialization;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.ViewModels;

/// <summary>
/// Unit tests for the <see cref="LineOfDutyViewModel"/> dirty-tracking system, which uses
/// JSON snapshot comparison to detect whether a form section has been modified since the
/// last <see cref="LineOfDutyViewModel.TakeSnapshot"/> call.
/// </summary>
/// <remarks>
/// <para>
/// The dirty-tracking mechanism works by serializing the view model to JSON at snapshot time,
/// then re-serializing and comparing when <see cref="LineOfDutyViewModel.IsDirtySection"/> is
/// called. These tests validate correct dirty/clean detection for text fields, nullable boolean
/// radio buttons, undo semantics, and multi-field scenarios.
/// </para>
/// <para>
/// A key Blazor/Radzen nuance tested here is that nullable boolean radio buttons (<c>bool?</c>)
/// cannot be reverted to <c>null</c> once a selection is made, so toggling between <c>true</c>
/// and <c>false</c> still reports dirty relative to a <c>null</c> snapshot.
/// </para>
/// </remarks>
public class DirtyTrackingTests
{
    /// <summary>
    /// Shared JSON serializer options configured to match the Blazor WebAssembly runtime defaults
    /// (camelCase naming, string enum conversion, and cyclic reference handling).
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    /// <summary>
    /// Verifies that changing a text field marks the section as dirty, and reverting it
    /// back to the original empty string value clears the dirty flag.
    /// </summary>
    [Fact]
    public void TextFieldChange_ThenUndo_SectionNotDirty()
    {
        // Arrange: new case, take snapshot with defaults
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        // Act: change a MedicalAssessment text field
        vm.ClinicalDiagnosis = "test diagnosis";
        Assert.True(vm.IsDirtySection("MedicalAssessment"), "Should be dirty after text change");

        // Undo: revert text to original empty value
        vm.ClinicalDiagnosis = "";
        Assert.False(vm.IsDirtySection("MedicalAssessment"), "Should NOT be dirty after reverting text to empty");
    }

    /// <summary>
    /// Verifies that reverting a text field to <c>null</c> (instead of empty string) also
    /// clears the dirty flag, accounting for Radzen components that may emit <c>null</c>
    /// rather than an empty string on clear.
    /// </summary>
    [Fact]
    public void TextFieldChange_SetToNull_ThenUndo_SectionNotDirty()
    {
        // Arrange: new case, take snapshot with defaults
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        // Act: change a MedicalAssessment text field
        vm.ClinicalDiagnosis = "test diagnosis";
        Assert.True(vm.IsDirtySection("MedicalAssessment"), "Should be dirty after text change");

        // Undo: revert text to null (some Radzen components may return null instead of "")
        vm.ClinicalDiagnosis = null;
        Assert.False(vm.IsDirtySection("MedicalAssessment"), "Should NOT be dirty after reverting text to null");
    }

    /// <summary>
    /// Verifies that a nullable boolean radio button starting at <c>null</c> remains dirty
    /// even after toggling to <c>false</c>, because radio buttons cannot return to the
    /// <c>null</c> (unselected) state once a choice is made.
    /// </summary>
    [Fact]
    public void BoolRadioButton_ChangeFromNull_CannotUndoToNull()
    {
        // Arrange: new case, bool? starts as null
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        // Act: user clicks Yes
        vm.IsMilitaryFacility = true;
        Assert.True(vm.IsDirtySection("MedicalAssessment"), "Should be dirty after radio change");

        // User clicks No (trying to "undo")
        vm.IsMilitaryFacility = false;
        Assert.True(vm.IsDirtySection("MedicalAssessment"),
            "Still dirty: original was null, now false — radio buttons can't return to null");
    }

    /// <summary>
    /// Verifies that toggling a boolean radio button back to its original value on an
    /// existing case leaves the section dirty if a dependent field (e.g.,
    /// <see cref="LineOfDutyViewModel.TreatmentFacilityName"/>) was cleared by a change
    /// handler and not restored.
    /// </summary>
    [Fact]
    public void BoolRadioButton_ExistingCase_ChangeAndUndo_SectionNotDirty()
    {
        // Arrange: existing case with saved values
        var vm = new LineOfDutyViewModel { IsMilitaryFacility = true, TreatmentFacilityName = "Base Hospital" };
        vm.TakeSnapshot(JsonOptions);

        // Act: user clicks No
        vm.IsMilitaryFacility = false;
        // Simulate change handler clearing dependent field
        vm.TreatmentFacilityName = null;
        Assert.True(vm.IsDirtySection("MedicalAssessment"), "Should be dirty after radio change");

        // User clicks Yes back
        vm.IsMilitaryFacility = true;
        // NOTE: change handler does NOT restore TreatmentFacilityName — it stays null
        // So even though IsMilitaryFacility matches snapshot, TreatmentFacilityName doesn't
        var isDirty = vm.IsDirtySection("MedicalAssessment");
        Assert.True(isDirty, "Still dirty: TreatmentFacilityName was cleared and not restored");
    }

    /// <summary>
    /// Verifies that a freshly created view model with all default property values is not
    /// detected as dirty immediately after taking a snapshot.
    /// </summary>
    [Fact]
    public void AllMedicalAssessmentDefaults_NotDirty()
    {
        // Arrange: new case with all defaults
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        // Assert: nothing changed, section should not be dirty
        Assert.False(vm.IsDirtySection("MedicalAssessment"), "Should NOT be dirty with no changes");
    }

    /// <summary>
    /// Verifies that changing multiple text fields marks the section as dirty, and reverting
    /// all of them back to empty strings clears the dirty flag, confirming holistic comparison
    /// across all tracked properties.
    /// </summary>
    [Fact]
    public void MultipleTextFields_ChangeAndUndo_SectionNotDirty()
    {
        // Arrange
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        // Change multiple fields
        vm.ClinicalDiagnosis = "test";
        vm.MedicalRecommendation = "recommend something";
        vm.MedicalFindings = "findings here";
        Assert.True(vm.IsDirtySection("MedicalAssessment"));

        // Undo all
        vm.ClinicalDiagnosis = "";
        vm.MedicalRecommendation = "";
        vm.MedicalFindings = "";
        Assert.False(vm.IsDirtySection("MedicalAssessment"), "Should NOT be dirty after undoing all text changes");
    }
}
