namespace ECTSystem.Api.Logging;

public partial class ApiLogService(ILogger<ApiLogService> logger) : IApiLogService
{
    private readonly ILogger _logger = logger;

    // Cases (EventId 100–110)

    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Querying all LOD cases")]
    public partial void QueryingCases();

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "Retrieving LOD case {CaseId}")]
    public partial void RetrievingCase(int caseId);

    [LoggerMessage(EventId = 102, Level = LogLevel.Warning, Message = "LOD case {CaseId} not found")]
    public partial void CaseNotFound(int caseId);

    [LoggerMessage(EventId = 103, Level = LogLevel.Warning, Message = "Invalid model state in {Action} action")]
    public partial void InvalidModelState(string action);

    [LoggerMessage(EventId = 111, Level = LogLevel.Warning, Message = "ModelState error in {Action}: {Property} — {Error}")]
    public partial void ModelStatePropertyError(string action, string property, string error);

    [LoggerMessage(EventId = 112, Level = LogLevel.Warning, Message = "Deserialization error in {Action}: {Property} — {ExceptionMessage}")]
    public partial void ModelStateExceptionError(string action, string property, string exceptionMessage);

    [LoggerMessage(EventId = 104, Level = LogLevel.Information, Message = "LOD case {CaseId} created")]
    public partial void CaseCreated(int caseId);

    [LoggerMessage(EventId = 105, Level = LogLevel.Information, Message = "Updating LOD case {CaseId}")]
    public partial void UpdatingCase(int caseId);

    [LoggerMessage(EventId = 106, Level = LogLevel.Information, Message = "LOD case {CaseId} updated")]
    public partial void CaseUpdated(int caseId);

    [LoggerMessage(EventId = 107, Level = LogLevel.Information, Message = "Patching LOD case {CaseId}")]
    public partial void PatchingCase(int caseId);

    [LoggerMessage(EventId = 108, Level = LogLevel.Information, Message = "LOD case {CaseId} patched")]
    public partial void CasePatched(int caseId);

    [LoggerMessage(EventId = 109, Level = LogLevel.Information, Message = "Deleting LOD case {CaseId}")]
    public partial void DeletingCase(int caseId);

    [LoggerMessage(EventId = 110, Level = LogLevel.Information, Message = "LOD case {CaseId} deleted")]
    public partial void CaseDeleted(int caseId);

    // Members (EventId 200–210)

    [LoggerMessage(EventId = 200, Level = LogLevel.Information, Message = "Querying all members")]
    public partial void QueryingMembers();

    [LoggerMessage(EventId = 201, Level = LogLevel.Information, Message = "Retrieving member {MemberId}")]
    public partial void RetrievingMember(int memberId);

    [LoggerMessage(EventId = 202, Level = LogLevel.Warning, Message = "Member {MemberId} not found")]
    public partial void MemberNotFound(int memberId);

    [LoggerMessage(EventId = 203, Level = LogLevel.Warning, Message = "Invalid model state in member {Action} action")]
    public partial void MemberInvalidModelState(string action);

    [LoggerMessage(EventId = 204, Level = LogLevel.Information, Message = "Member {MemberId} created")]
    public partial void MemberCreated(int memberId);

    [LoggerMessage(EventId = 205, Level = LogLevel.Information, Message = "Updating member {MemberId}")]
    public partial void UpdatingMember(int memberId);

    [LoggerMessage(EventId = 206, Level = LogLevel.Information, Message = "Member {MemberId} updated")]
    public partial void MemberUpdated(int memberId);

    [LoggerMessage(EventId = 207, Level = LogLevel.Information, Message = "Patching member {MemberId}")]
    public partial void PatchingMember(int memberId);

    [LoggerMessage(EventId = 208, Level = LogLevel.Information, Message = "Member {MemberId} patched")]
    public partial void MemberPatched(int memberId);

    [LoggerMessage(EventId = 209, Level = LogLevel.Information, Message = "Deleting member {MemberId}")]
    public partial void DeletingMember(int memberId);

    [LoggerMessage(EventId = 210, Level = LogLevel.Information, Message = "Member {MemberId} deleted")]
    public partial void MemberDeleted(int memberId);

    // Documents (EventId 300–311)

    [LoggerMessage(EventId = 311, Level = LogLevel.Information, Message = "Querying documents")]
    public partial void QueryingDocuments();

    [LoggerMessage(EventId = 300, Level = LogLevel.Information, Message = "Querying documents for case {CaseId}")]
    public partial void QueryingDocuments(int caseId);

    [LoggerMessage(EventId = 301, Level = LogLevel.Information, Message = "Retrieving document {DocumentId} for case {CaseId}")]
    public partial void RetrievingDocument(int documentId, int caseId);

    [LoggerMessage(EventId = 302, Level = LogLevel.Warning, Message = "Document {DocumentId} not found for case {CaseId}")]
    public partial void DocumentNotFound(int documentId, int caseId);

    [LoggerMessage(EventId = 303, Level = LogLevel.Information, Message = "Downloading document {DocumentId} for case {CaseId}")]
    public partial void DownloadingDocument(int documentId, int caseId);

    [LoggerMessage(EventId = 304, Level = LogLevel.Warning, Message = "Document content not found for document {DocumentId}")]
    public partial void DocumentContentNotFound(int documentId);

    [LoggerMessage(EventId = 305, Level = LogLevel.Warning, Message = "Invalid upload attempt for case {CaseId}")]
    public partial void InvalidUpload(int caseId);

    [LoggerMessage(EventId = 306, Level = LogLevel.Information, Message = "Uploading document for case {CaseId}")]
    public partial void UploadingDocument(int caseId);

    [LoggerMessage(EventId = 307, Level = LogLevel.Information, Message = "Document {DocumentId} uploaded for case {CaseId}")]
    public partial void DocumentUploaded(int documentId, int caseId);

    [LoggerMessage(EventId = 308, Level = LogLevel.Warning, Message = "Upload failed for case {CaseId}")]
    public partial void UploadFailed(int caseId, Exception ex);

    [LoggerMessage(EventId = 309, Level = LogLevel.Information, Message = "Deleting document {DocumentId} for case {CaseId}")]
    public partial void DeletingDocument(int documentId, int caseId);

    [LoggerMessage(EventId = 310, Level = LogLevel.Information, Message = "Document {DocumentId} deleted for case {CaseId}")]
    public partial void DocumentDeleted(int documentId, int caseId);

    // Bookmarks (EventId 500–506)

    [LoggerMessage(EventId = 500, Level = LogLevel.Information, Message = "Querying bookmarks")]
    public partial void QueryingBookmarks();

    [LoggerMessage(EventId = 501, Level = LogLevel.Information, Message = "Bookmark created for case {CaseId}")]
    public partial void BookmarkCreated(int caseId);

    [LoggerMessage(EventId = 502, Level = LogLevel.Information, Message = "Bookmark already exists for case {CaseId}")]
    public partial void BookmarkAlreadyExists(int caseId);

    [LoggerMessage(EventId = 503, Level = LogLevel.Information, Message = "Deleting bookmark for case {CaseId}")]
    public partial void DeletingBookmark(int caseId);

    [LoggerMessage(EventId = 504, Level = LogLevel.Information, Message = "Bookmark deleted for case {CaseId}")]
    public partial void BookmarkDeleted(int caseId);

    [LoggerMessage(EventId = 505, Level = LogLevel.Warning, Message = "Bookmark not found for case {CaseId}")]
    public partial void BookmarkNotFound(int caseId);

    [LoggerMessage(EventId = 506, Level = LogLevel.Information, Message = "Checking bookmark status for case {CaseId}")]
    public partial void CheckingBookmark(int caseId);

    // Navigation Properties (EventId 400–401)

    [LoggerMessage(EventId = 400, Level = LogLevel.Information, Message = "Querying {NavigationProperty} for case {CaseId}")]
    public partial void QueryingCaseNavigation(int caseId, string navigationProperty);

    [LoggerMessage(EventId = 401, Level = LogLevel.Information, Message = "Querying {NavigationProperty} for member {MemberId}")]
    public partial void QueryingMemberNavigation(int memberId, string navigationProperty);
}
