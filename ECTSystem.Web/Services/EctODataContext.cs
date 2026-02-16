using Microsoft.OData.Client;
using ECTSystem.Shared.Models;

namespace ECTSystem.Web.Services;

/// <summary>
/// OData client context for the ECT System API.
/// In Blazor WASM, <see cref="HttpClientRequestMessage"/> uses
/// <c>BrowserHttpHandler</c> under the hood — no explicit HttpClient injection needed.
/// Entities are resolved against ECTSystem.Shared types matching the server's EDM.
/// </summary>
public class EctODataContext : DataServiceContext
{
    private static readonly System.Reflection.Assembly SharedAssembly = typeof(LineOfDutyCase).Assembly;

    public EctODataContext(Uri serviceRoot)
        : base(serviceRoot, ODataProtocolVersion.V4)
    {
        // JSON format — fetches $metadata on first use for model awareness
        Format.UseJson();

        // No automatic entity tracking — keeps context lightweight
        MergeOption = MergeOption.NoTracking;

        // CLR ↔ EDM type name resolution (must match server EDM namespace)
        ResolveName = type => type.FullName;
        ResolveType = typeName => SharedAssembly.GetType(typeName);
    }

    // Entity set query accessors (names match server EDM entity sets)
    public DataServiceQuery<LineOfDutyCase> Cases => CreateQuery<LineOfDutyCase>("Cases");
    public DataServiceQuery<Member> Members => CreateQuery<Member>("Members");
    public DataServiceQuery<Notification> Notifications => CreateQuery<Notification>("Notifications");
    public DataServiceQuery<LineOfDutyAuthority> Authorities => CreateQuery<LineOfDutyAuthority>("Authorities");
    public DataServiceQuery<LineOfDutyDocument> Documents => CreateQuery<LineOfDutyDocument>("Documents");
    public DataServiceQuery<TimelineStep> TimelineSteps => CreateQuery<TimelineStep>("TimelineSteps");
    public DataServiceQuery<LineOfDutyAppeal> Appeals => CreateQuery<LineOfDutyAppeal>("Appeals");
    public DataServiceQuery<MEDCONDetail> MEDCONDetails => CreateQuery<MEDCONDetail>("MEDCONDetails");
    public DataServiceQuery<INCAPDetails> INCAPDetails => CreateQuery<INCAPDetails>("INCAPDetails");
}
