using System.Text.Json;
using System.Text.Json.Serialization;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="TrackableModel"/>, the abstract base class providing
/// snapshot-based dirty tracking via JSON serialization. Tests verify snapshot capture,
/// dirty detection across full model and named sections, null/empty normalization,
/// revert semantics, and edge cases like missing snapshots.
/// </summary>
public class TrackableModelTests
{
    /// <summary>
    /// Shared JSON serializer options matching the Blazor WebAssembly runtime defaults.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    // --- Test model ---

    private class TestModel : TrackableModel
    {
        [FormSection("SectionA")]
        public string FieldA { get; set; } = string.Empty;

        [FormSection("SectionA")]
        public int NumberA { get; set; }

        [FormSection("SectionB")]
        public string FieldB { get; set; } = string.Empty;

        [FormSection("SectionB")]
        public bool? NullableBool { get; set; }

        public string UnsectionedField { get; set; } = string.Empty;
    }

    // --- HasSnapshot ---

    /// <summary>
    /// Verifies that <see cref="TrackableModel.HasSnapshot"/> is false before any snapshot is taken.
    /// </summary>
    [Fact]
    public void HasSnapshot_BeforeSnapshot_ReturnsFalse()
    {
        var model = new TestModel();

        Assert.False(model.HasSnapshot);
    }

    /// <summary>
    /// Verifies that <see cref="TrackableModel.HasSnapshot"/> is true after a snapshot is taken.
    /// </summary>
    [Fact]
    public void HasSnapshot_AfterSnapshot_ReturnsTrue()
    {
        var model = new TestModel();
        model.TakeSnapshot(JsonOptions);

        Assert.True(model.HasSnapshot);
    }

    // --- IsDirty (full model) ---

    /// <summary>
    /// Verifies that <see cref="TrackableModel.IsDirty"/> returns false when no snapshot exists.
    /// </summary>
    [Fact]
    public void IsDirty_NoSnapshot_ReturnsFalse()
    {
        var model = new TestModel { FieldA = "changed" };

        Assert.False(model.IsDirty);
    }

    /// <summary>
    /// Verifies that a model is not dirty immediately after taking a snapshot.
    /// </summary>
    [Fact]
    public void IsDirty_ImmediatelyAfterSnapshot_ReturnsFalse()
    {
        var model = new TestModel { FieldA = "initial" };
        model.TakeSnapshot(JsonOptions);

        Assert.False(model.IsDirty);
    }

    /// <summary>
    /// Verifies that changing a string property marks the model as dirty.
    /// </summary>
    [Fact]
    public void IsDirty_AfterStringChange_ReturnsTrue()
    {
        var model = new TestModel();
        model.TakeSnapshot(JsonOptions);

        model.FieldA = "modified";

        Assert.True(model.IsDirty);
    }

    /// <summary>
    /// Verifies that changing an int property marks the model as dirty.
    /// </summary>
    [Fact]
    public void IsDirty_AfterIntChange_ReturnsTrue()
    {
        var model = new TestModel();
        model.TakeSnapshot(JsonOptions);

        model.NumberA = 42;

        Assert.True(model.IsDirty);
    }

    /// <summary>
    /// Verifies that changing a nullable bool from null to a value marks the model as dirty.
    /// </summary>
    [Fact]
    public void IsDirty_NullableBoolFromNullToTrue_ReturnsTrue()
    {
        var model = new TestModel();
        model.TakeSnapshot(JsonOptions);

        model.NullableBool = true;

        Assert.True(model.IsDirty);
    }

    // --- Null/empty string normalization ---

    /// <summary>
    /// Verifies that setting a field from empty string to null does not trigger dirty,
    /// because the NormalizedEquals treats null and empty string as equivalent.
    /// </summary>
    [Fact]
    public void IsDirty_EmptyToNull_ReturnsFalse()
    {
        var model = new TestModel { FieldA = "" };
        model.TakeSnapshot(JsonOptions);

        model.FieldA = null;

        Assert.False(model.IsDirty);
    }

    /// <summary>
    /// Verifies that setting a field from null to empty string does not trigger dirty.
    /// </summary>
    [Fact]
    public void IsDirty_NullToEmpty_ReturnsFalse()
    {
        var model = new TestModel { FieldA = null };
        model.TakeSnapshot(JsonOptions);

        model.FieldA = "";

        Assert.False(model.IsDirty);
    }

    // --- IsDirtySection ---

    /// <summary>
    /// Verifies that changing only SectionA field marks SectionA as dirty but not SectionB.
    /// </summary>
    [Fact]
    public void IsDirtySection_ChangeInSectionA_OnlySectionADirty()
    {
        var model = new TestModel();
        model.TakeSnapshot(JsonOptions);

        model.FieldA = "changed";

        Assert.True(model.IsDirtySection("SectionA"));
        Assert.False(model.IsDirtySection("SectionB"));
    }

    /// <summary>
    /// Verifies that changing only SectionB field marks SectionB as dirty but not SectionA.
    /// </summary>
    [Fact]
    public void IsDirtySection_ChangeInSectionB_OnlySectionBDirty()
    {
        var model = new TestModel();
        model.TakeSnapshot(JsonOptions);

        model.FieldB = "changed";

        Assert.False(model.IsDirtySection("SectionA"));
        Assert.True(model.IsDirtySection("SectionB"));
    }

    /// <summary>
    /// Verifies that changing an unsectioned field does not affect any named section.
    /// </summary>
    [Fact]
    public void IsDirtySection_ChangeUnsectionedField_SectionsNotDirty()
    {
        var model = new TestModel();
        model.TakeSnapshot(JsonOptions);

        model.UnsectionedField = "changed";

        Assert.False(model.IsDirtySection("SectionA"));
        Assert.False(model.IsDirtySection("SectionB"));
    }

    /// <summary>
    /// Verifies that passing null or empty section name returns false.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void IsDirtySection_NullOrEmptyName_ReturnsFalse(string sectionName)
    {
        var model = new TestModel { FieldA = "value" };
        model.TakeSnapshot(JsonOptions);

        model.FieldA = "changed";

        Assert.False(model.IsDirtySection(sectionName));
    }

    /// <summary>
    /// Verifies that a non-existent section name returns false (no properties match).
    /// </summary>
    [Fact]
    public void IsDirtySection_NonExistentSection_ReturnsFalse()
    {
        var model = new TestModel();
        model.TakeSnapshot(JsonOptions);

        model.FieldA = "changed";

        Assert.False(model.IsDirtySection("NonExistentSection"));
    }

    // --- Revert ---

    /// <summary>
    /// Verifies that Revert restores all public read/write properties to snapshot values.
    /// </summary>
    [Fact]
    public void Revert_AfterModification_RestoresAllProperties()
    {
        var model = new TestModel
        {
            FieldA = "original",
            NumberA = 10,
            FieldB = "original-b",
            NullableBool = true,
            UnsectionedField = "original-unsectioned"
        };
        model.TakeSnapshot(JsonOptions);

        // Modify everything
        model.FieldA = "modified";
        model.NumberA = 99;
        model.FieldB = "modified-b";
        model.NullableBool = false;
        model.UnsectionedField = "modified-unsectioned";

        // Act
        model.Revert();

        // Assert
        Assert.Equal("original", model.FieldA);
        Assert.Equal(10, model.NumberA);
        Assert.Equal("original-b", model.FieldB);
        Assert.True(model.NullableBool);
        Assert.Equal("original-unsectioned", model.UnsectionedField);
        Assert.False(model.IsDirty);
    }

    /// <summary>
    /// Verifies that Revert is a no-op when no snapshot has been taken.
    /// </summary>
    [Fact]
    public void Revert_WithoutSnapshot_DoesNothing()
    {
        var model = new TestModel { FieldA = "current" };

        model.Revert();

        Assert.Equal("current", model.FieldA);
    }

    // --- Re-snapshot after save ---

    /// <summary>
    /// Verifies that taking a new snapshot after modifications resets dirty tracking
    /// to the new values as the clean baseline.
    /// </summary>
    [Fact]
    public void TakeSnapshot_AfterModification_ResetsBaseline()
    {
        var model = new TestModel { FieldA = "v1" };
        model.TakeSnapshot(JsonOptions);

        model.FieldA = "v2";
        Assert.True(model.IsDirty);

        // Re-snapshot with new values
        model.TakeSnapshot(JsonOptions);

        Assert.False(model.IsDirty);

        // Further change from new baseline should be dirty
        model.FieldA = "v3";
        Assert.True(model.IsDirty);
    }
}
