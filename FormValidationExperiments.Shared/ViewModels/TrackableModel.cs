using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormValidationExperiments.Shared.ViewModels;

/// <summary>
/// Base class that provides snapshot-based dirty tracking for form models.
/// Call <see cref="TakeSnapshot"/> after loading or saving to establish a clean baseline.
/// The <see cref="IsDirty"/> property returns <see langword="true"/> when the current
/// property values differ from the last snapshot.
/// Call <see cref="Revert"/> to restore all properties to the snapshot state.
/// </summary>
public abstract class TrackableModel
{
    [JsonIgnore]
    private string? _snapshot;

    [JsonIgnore]
    private JsonSerializerOptions? _jsonOptions;

    /// <summary>
    /// Gets whether any tracked property has changed since the last snapshot.
    /// </summary>
    [JsonIgnore]
    public bool IsDirty =>
        _snapshot is not null
        && _jsonOptions is not null
        && JsonSerializer.Serialize(this, GetType(), _jsonOptions) != _snapshot;

    /// <summary>
    /// Captures the current state as the clean baseline.
    /// </summary>
    public void TakeSnapshot(JsonSerializerOptions options)
    {
        _jsonOptions = options;
        _snapshot = JsonSerializer.Serialize(this, GetType(), options);
    }

    /// <summary>
    /// Restores all public read/write properties to the values captured in the last snapshot.
    /// </summary>
    public void Revert()
    {
        if (_snapshot is null || _jsonOptions is null)
            return;

        var clean = JsonSerializer.Deserialize(_snapshot, GetType(), _jsonOptions);

        foreach (var prop in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanRead && prop.CanWrite && prop.GetCustomAttribute<JsonIgnoreAttribute>() is null)
                prop.SetValue(this, prop.GetValue(clean));
        }
    }
}
