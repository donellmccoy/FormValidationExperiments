using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ECTSystem.Api.Data.Configurations;

/// <summary>
/// Shared value converter and comparer for List&lt;string&gt; properties stored as JSON.
/// </summary>
internal static class StringListConversion
{
    public static readonly ValueConverter<List<string>, string> Converter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)default!),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)default!) ?? new List<string>());

    public static readonly ValueComparer<List<string>> Comparer = new(
        (c1, c2) => (c1 ?? new()).SequenceEqual(c2 ?? new()),
        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
        c => c.ToList());
}
