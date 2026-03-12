using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECTSystem.Shared.ViewModels;

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
    private string _snapshot = string.Empty;

    [JsonIgnore]
    private JsonSerializerOptions _jsonOptions = new JsonSerializerOptions();

    /// <summary>
    /// Gets whether any tracked property has changed since the last snapshot.
    /// </summary>
    [JsonIgnore]
    public bool IsDirty => CheckDirty(sectionFilter: null);

    [JsonIgnore]
    public bool HasSnapshot => !string.IsNullOrEmpty(_snapshot);

    /// <summary>
    /// Gets whether any property in the named section has changed since the last snapshot.
    /// </summary>
    public bool IsDirtySection(string sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return false;
        }

        return CheckDirty(sectionFilter: sectionName);
    }

    private bool CheckDirty(string sectionFilter)
    {
        if (string.IsNullOrWhiteSpace(_snapshot) || _jsonOptions is null)
        {
            return false;
        }

        var snapshotObj = JsonSerializer.Deserialize(_snapshot, GetType(), _jsonOptions);
        if (snapshotObj is null)
        {
            return false;
        }

        IEnumerable<PropertyInfo> properties = GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        if (sectionFilter is not null)
        {
            properties = properties.Where(p => p.GetCustomAttributes<FormSectionAttribute>()
                .Any(a => a.SectionName == sectionFilter));
        }
        else
        {
            properties = properties.Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null);
        }

        foreach (var property in properties)
        {
            var currentValue = property.GetValue(this);
            var snapshotValue = property.GetValue(snapshotObj);
            if (!NormalizedEquals(currentValue, snapshotValue))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NormalizedEquals(object current, object snapshot)
    {
        // Treat null and empty string as equivalent so that change handlers
        // setting a string to null won't conflict with a snapshot that has "".
        if (current is string sc && sc.Length == 0) current = null;
        if (snapshot is string ss && ss.Length == 0) snapshot = null;

        return Equals(current, snapshot);
    }

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
        if (string.IsNullOrWhiteSpace(_snapshot) || _jsonOptions is null)
        {
            return;
        }

        var clean = JsonSerializer.Deserialize(_snapshot, GetType(), _jsonOptions);

        foreach (var prop in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanRead && prop.CanWrite && prop.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            {
                prop.SetValue(this, prop.GetValue(clean));
            }
        }
    }
}
