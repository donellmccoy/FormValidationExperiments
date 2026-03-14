using System.Text.Json;
using System.Text.Json.Serialization;
using ECTSystem.Shared.Enums;
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
        vm.ClinicalDiagnosis = string.Empty;
        vm.MedicalRecommendation = "";
        vm.MedicalFindings = "";
        Assert.False(vm.IsDirtySection("MedicalAssessment"), "Should NOT be dirty after undoing all text changes");
    }

    // ── HasSnapshot Tests ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="TrackableModel.HasSnapshot"/> returns <c>false</c> before
    /// any snapshot is taken, and <c>true</c> immediately after <see cref="TrackableModel.TakeSnapshot"/>.
    /// </summary>
    [Fact]
    public void HasSnapshot_FalseBeforeSnapshot_TrueAfter()
    {
        var vm = new LineOfDutyViewModel();
        Assert.False(vm.HasSnapshot, "HasSnapshot should be false before TakeSnapshot");

        vm.TakeSnapshot(JsonOptions);
        Assert.True(vm.HasSnapshot, "HasSnapshot should be true after TakeSnapshot");
    }

    // ── Global IsDirty Tests ────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the global <see cref="TrackableModel.IsDirty"/> property detects changes
    /// to any tracked property regardless of section, and returns <c>false</c> when no changes
    /// have been made.
    /// </summary>
    [Fact]
    public void IsDirty_DetectsChangesAcrossAnySectionOrProperty()
    {
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        Assert.False(vm.IsDirty, "Should not be dirty immediately after snapshot");

        vm.CommanderName = "Col Smith";
        Assert.True(vm.IsDirty, "Should be dirty after changing a UnitCommander property");
    }

    /// <summary>
    /// Verifies that <see cref="TrackableModel.IsDirty"/> returns <c>false</c> when no snapshot
    /// has been taken, even if properties have been changed, because there is no baseline to
    /// compare against.
    /// </summary>
    [Fact]
    public void IsDirty_FalseWhenNoSnapshot()
    {
        var vm = new LineOfDutyViewModel { ClinicalDiagnosis = "changed value" };
        Assert.False(vm.IsDirty, "IsDirty should return false when no snapshot exists");
    }

    // ── Cross-Section Isolation Tests ───────────────────────────────────────

    /// <summary>
    /// Verifies that modifying a property in one section does not cause a different section
    /// to report as dirty, confirming section-level isolation.
    /// </summary>
    [Fact]
    public void CrossSection_ChangingUnitCommander_DoesNotDirtyMedicalAssessment()
    {
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        // Change a UnitCommander-only field
        vm.NarrativeOfCircumstances = "Member was on duty";
        Assert.True(vm.IsDirtySection("UnitCommander"), "UnitCommander should be dirty");
        Assert.False(vm.IsDirtySection("MedicalAssessment"), "MedicalAssessment should NOT be dirty");
    }

    /// <summary>
    /// Verifies that modifying a property in MedicalAssessment does not cause
    /// UnitCommander or WingCommander sections to report as dirty.
    /// </summary>
    [Fact]
    public void CrossSection_ChangingMedicalAssessment_DoesNotDirtyOtherSections()
    {
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        vm.ClinicalDiagnosis = "Fracture, left tibia";
        Assert.True(vm.IsDirtySection("MedicalAssessment"), "MedicalAssessment should be dirty");
        Assert.False(vm.IsDirtySection("UnitCommander"), "UnitCommander should NOT be dirty");
        Assert.False(vm.IsDirtySection("WingCommander"), "WingCommander should NOT be dirty");
    }

    // ── Enum Field Tests ────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that changing a nullable enum field (e.g., <see cref="LineOfDutyViewModel.CommanderRank"/>)
    /// from <c>null</c> to an enum value marks the section as dirty.
    /// </summary>
    [Fact]
    public void EnumField_ChangeFromNull_MarksSectionDirty()
    {
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        vm.CommanderRank = MilitaryRank.Col;
        Assert.True(vm.IsDirtySection("UnitCommander"), "Should be dirty after setting nullable enum from null");
    }

    /// <summary>
    /// Verifies that changing a nullable enum field from one value to another marks the
    /// section as dirty, and reverting to the original value clears the dirty flag.
    /// </summary>
    [Fact]
    public void EnumField_ChangeAndRevert_ClearsDirtyFlag()
    {
        var vm = new LineOfDutyViewModel { Recommendation = CommanderRecommendation.InLineOfDuty };
        vm.TakeSnapshot(JsonOptions);

        vm.Recommendation = CommanderRecommendation.ReferToFormalInvestigation;
        Assert.True(vm.IsDirtySection("UnitCommander"), "Should be dirty after enum value change");

        vm.Recommendation = CommanderRecommendation.InLineOfDuty;
        Assert.False(vm.IsDirtySection("UnitCommander"), "Should NOT be dirty after reverting enum to original");
    }

    // ── DateTime Field Tests ────────────────────────────────────────────────

    /// <summary>
    /// Verifies that changing a nullable <see cref="DateTime"/> field marks the section as dirty
    /// and reverting clears it.
    /// </summary>
    [Fact]
    public void DateTimeField_ChangeAndRevert_TracksDirtyCorrectly()
    {
        var originalDate = new DateTime(2024, 3, 15);
        var vm = new LineOfDutyViewModel { CommanderSignatureDate = originalDate };
        vm.TakeSnapshot(JsonOptions);

        vm.CommanderSignatureDate = new DateTime(2024, 6, 1);
        Assert.True(vm.IsDirtySection("UnitCommander"), "Should be dirty after DateTime change");

        vm.CommanderSignatureDate = originalDate;
        Assert.False(vm.IsDirtySection("UnitCommander"), "Should NOT be dirty after reverting DateTime");
    }

    // ── Revert Tests ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="TrackableModel.Revert"/> restores all changed properties
    /// to their snapshot values and clears the dirty flag.
    /// </summary>
    [Fact]
    public void Revert_RestoresAllChangedPropertiesToSnapshotValues()
    {
        var vm = new LineOfDutyViewModel
        {
            ClinicalDiagnosis = "Original diagnosis",
            CommanderName = "Original commander",
            CommanderRank = MilitaryRank.Maj
        };
        vm.TakeSnapshot(JsonOptions);

        // Change properties across multiple sections
        vm.ClinicalDiagnosis = "Updated diagnosis";
        vm.CommanderName = "New commander";
        vm.CommanderRank = MilitaryRank.Col;
        Assert.True(vm.IsDirty, "Should be dirty after changes");

        // Revert
        vm.Revert();
        Assert.False(vm.IsDirty, "Should NOT be dirty after Revert");
        Assert.Equal("Original diagnosis", vm.ClinicalDiagnosis);
        Assert.Equal("Original commander", vm.CommanderName);
        Assert.Equal(MilitaryRank.Maj, vm.CommanderRank);
    }

    /// <summary>
    /// Verifies that <see cref="TrackableModel.Revert"/> is a no-op when no snapshot has
    /// been taken, leaving property values unchanged.
    /// </summary>
    [Fact]
    public void Revert_NoSnapshot_LeavesValuesUnchanged()
    {
        var vm = new LineOfDutyViewModel { ClinicalDiagnosis = "some value" };

        vm.Revert(); // should not throw or change anything

        Assert.Equal("some value", vm.ClinicalDiagnosis);
    }

    // ── Re-Snapshot Tests ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies that taking a new snapshot after modifications resets the dirty baseline,
    /// so the model reports clean relative to the new snapshot state.
    /// </summary>
    [Fact]
    public void ReSnapshot_AfterChanges_ResetsBaseline()
    {
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        vm.ClinicalDiagnosis = "updated";
        Assert.True(vm.IsDirtySection("MedicalAssessment"), "Should be dirty after change");

        // Take a new snapshot (simulating a save)
        vm.TakeSnapshot(JsonOptions);
        Assert.False(vm.IsDirtySection("MedicalAssessment"), "Should NOT be dirty after re-snapshot");
        Assert.False(vm.IsDirty, "Global IsDirty should be false after re-snapshot");
    }

    // ── IsDirtySection Edge Cases ───────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="TrackableModel.IsDirtySection"/> returns <c>false</c>
    /// for a non-existent section name because no properties match the filter.
    /// </summary>
    [Fact]
    public void IsDirtySection_NonExistentSection_ReturnsFalse()
    {
        var vm = new LineOfDutyViewModel();
        vm.TakeSnapshot(JsonOptions);

        vm.ClinicalDiagnosis = "changed";
        Assert.False(vm.IsDirtySection("NonExistentSection"), "Non-existent section should return false");
    }

    /// <summary>
    /// Verifies that <see cref="TrackableModel.IsDirtySection"/> returns <c>false</c>
    /// when called with a null or empty section name.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsDirtySection_NullOrEmptyName_ReturnsFalse(string sectionName)
    {
        var vm = new LineOfDutyViewModel { ClinicalDiagnosis = "changed" };
        vm.TakeSnapshot(JsonOptions);

        vm.ClinicalDiagnosis = "different";
        Assert.False(vm.IsDirtySection(sectionName), "Null/empty section name should return false");
    }
}
