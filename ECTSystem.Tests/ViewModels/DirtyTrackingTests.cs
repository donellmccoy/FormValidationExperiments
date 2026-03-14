using System.Text.Json;
using System.Text.Json.Serialization;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.ViewModels;

public class DirtyTrackingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

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

    [Fact]
    public void AllMedicalAssessmentDefaults_NotDirty()
    {
        // Arrange: new case with all defaults
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        // Assert: nothing changed, section should not be dirty
        Assert.False(vm.IsDirtySection("MedicalAssessment"), "Should NOT be dirty with no changes");
    }

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
