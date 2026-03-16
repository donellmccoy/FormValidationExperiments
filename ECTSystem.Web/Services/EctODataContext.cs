using Microsoft.OData.Client;
using ECTSystem.Shared.Models;

#nullable enable

namespace ECTSystem.Web.Services;

public class EctODataContext : DataServiceContext
{
    public EctODataContext(Uri serviceRoot) : base(serviceRoot, ODataProtocolVersion.V4)
    {
        MergeOption = MergeOption.OverwriteChanges;

        ResolveName = ResolveEntitySetName;
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
        => CreateQuery<WorkflowStateHistory>("WorkflowStateHistories");

    public DataServiceQuery<CaseBookmark> CaseBookmarks
        => CreateQuery<CaseBookmark>("CaseBookmarks");

    public DataServiceQuery<Notification> Notifications
        => CreateQuery<Notification>("Notifications");

    public DataServiceQuery<LineOfDutyAppeal> Appeals
        => CreateQuery<LineOfDutyAppeal>("Appeals");

    public DataServiceQuery<WitnessStatement> WitnessStatements
        => CreateQuery<WitnessStatement>("WitnessStatements");

    public DataServiceQuery<AuditComment> AuditComments
        => CreateQuery<AuditComment>("AuditComments");

    private static string ResolveEntitySetName(Type type)
    {
        return type.Name switch
        {
            nameof(LineOfDutyCase) => "Cases",
            nameof(Member) => "Members",
            nameof(LineOfDutyAuthority) => "Authorities",
            nameof(LineOfDutyDocument) => "Documents",
            nameof(WorkflowStateHistory) => "WorkflowStateHistories",
            nameof(CaseBookmark) => "CaseBookmarks",
            nameof(Notification) => "Notifications",
            nameof(LineOfDutyAppeal) => "Appeals",
            nameof(WitnessStatement) => "WitnessStatements",
            nameof(AuditComment) => "AuditComments",
            nameof(MEDCONDetail) => "MEDCONDetails",
            nameof(INCAPDetails) => "INCAPDetails",
            _ => type.Name
        };
    }

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
            nameof(CaseBookmark) => typeof(CaseBookmark),
            nameof(Notification) => typeof(Notification),
            nameof(LineOfDutyAppeal) => typeof(LineOfDutyAppeal),
            nameof(WitnessStatement) => typeof(WitnessStatement),
            nameof(AuditComment) => typeof(AuditComment),
            nameof(MEDCONDetail) => typeof(MEDCONDetail),
            nameof(INCAPDetails) => typeof(INCAPDetails),
            _ => typeof(object)
        };
    }
}
