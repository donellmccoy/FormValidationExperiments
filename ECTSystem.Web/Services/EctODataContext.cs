using Microsoft.OData.Client;
using ECTSystem.Shared.Models;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Typed OData v4 client context for the ECT API. Exposes a <see cref="DataServiceQuery{T}"/>
/// per entity set and resolves CLR type ↔ wire-format type names so server payloads materialize
/// into the shared model types in <c>ECTSystem.Shared.Models</c>.
/// </summary>
/// <remarks>
/// <para><b>Adding a new entity set requires updates in three coordinated places — keep them in sync:</b></para>
/// <list type="number">
///   <item>
///     <description>
///       Add an <see cref="EntitySetAttribute"/>-backed <see cref="DataServiceQuery{T}"/> property here.
///     </description>
///   </item>
///   <item>
///     <description>
///       Add a <c>case nameof(T) =&gt; typeof(T)</c> arm to <see cref="ResolveEntityType"/> so
///       responses materialize correctly. The default <c>typeof(object)</c> fallback is silent —
///       missing entries surface as <see cref="NullReferenceException"/> during property
///       materialization rather than as a clear "unknown type" error.
///     </description>
///   </item>
///   <item>
///     <description>
///       Add a matching <c>EdmEntityType</c> + <c>container.AddEntitySet(...)</c> entry in
///       <c>ServiceCollectionExtensions.BuildClientEdmModel()</c>; otherwise the OData reader
///       cannot deserialize the entity (especially enum properties — see
///       <c>BuildClientEdmModel</c>'s enum-type registrations).
///     </description>
///   </item>
/// </list>
/// <para>
/// <see cref="ResolveName"/> intentionally returns <c>type.FullName</c> so the server-side
/// EDM (which uses CLR full names) and the client-side EDM stay aligned without a manual map.
/// </para>
/// </remarks>
public class EctODataContext : DataServiceContext
{
    public EctODataContext(Uri serviceRoot) : base(serviceRoot, ODataProtocolVersion.V4)
    {
        MergeOption = MergeOption.OverwriteChanges;

        ResolveName = type => type.FullName!;
        ResolveType = ResolveEntityType;
    }

    public DataServiceQuery<LineOfDutyCase> Cases
        => CreateQuery<LineOfDutyCase>("Cases");

    public DataServiceQuery<Member> Members
        => CreateQuery<Member>("Members");

    public DataServiceQuery<LineOfDutyAuthority> Authorities
        => CreateQuery<LineOfDutyAuthority>("Authorities");

    public DataServiceQuery<LineOfDutyDocument> Documents
        => CreateQuery<LineOfDutyDocument>("Documents");

    public DataServiceQuery<WorkflowStateHistory> WorkflowStateHistories
        => CreateQuery<WorkflowStateHistory>("WorkflowStateHistory");

    public DataServiceQuery<Bookmark> Bookmarks
        => CreateQuery<Bookmark>("Bookmarks");

    public DataServiceQuery<LineOfDutyAppeal> Appeals
        => CreateQuery<LineOfDutyAppeal>("Appeals");

    public DataServiceQuery<WitnessStatement> WitnessStatements
        => CreateQuery<WitnessStatement>("WitnessStatements");

    public DataServiceQuery<AuditComment> AuditComments
        => CreateQuery<AuditComment>("AuditComments");

    public DataServiceQuery<CaseDialogueComment> CaseDialogueComments
        => CreateQuery<CaseDialogueComment>("CaseDialogueComments");

    private static Type ResolveEntityType(string typeName)
    {
        var name = typeName.Split('.').Last();
        return name switch
        {
            nameof(LineOfDutyCase) => typeof(LineOfDutyCase),
            nameof(Member) => typeof(Member),
            nameof(LineOfDutyAuthority) => typeof(LineOfDutyAuthority),
            nameof(LineOfDutyDocument) => typeof(LineOfDutyDocument),
            nameof(WorkflowStateHistory) => typeof(WorkflowStateHistory),
            nameof(Bookmark) => typeof(Bookmark),
            nameof(Notification) => typeof(Notification),
            nameof(LineOfDutyAppeal) => typeof(LineOfDutyAppeal),
            nameof(WitnessStatement) => typeof(WitnessStatement),
            nameof(AuditComment) => typeof(AuditComment),
            nameof(CaseDialogueComment) => typeof(CaseDialogueComment),
            nameof(MEDCONDetail) => typeof(MEDCONDetail),
            nameof(INCAPDetails) => typeof(INCAPDetails),
            _ => typeof(object)
        };
    }
}
