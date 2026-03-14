namespace ECTSystem.Api.Logging;

public partial class LoggingService(ILogger<LoggingService> logger) : ILoggingService
{
    private readonly ILogger _logger = logger;

    // Cases (EventId 100–110)

    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Querying all LOD cases")]
    public partial void QueryingCases();

    [LoggerMessage(EventId = 113, Level = LogLevel.Information, Message = "Querying bookmarked LOD cases")]
    public partial void QueryingBookmarkedCases();

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

    [LoggerMessage(EventId = 111, Level = LogLevel.Information, Message = "Transitioning LOD case {CaseId}")]
    public partial void TransitioningCase(int caseId);

    [LoggerMessage(EventId = 112, Level = LogLevel.Information, Message = "LOD case {CaseId} transitioned with {HistoryEntryCount} history entries")]
    public partial void CaseTransitioned(int caseId, int historyEntryCount);

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

    // Workflow State Histories (EventId 700–703)

    [LoggerMessage(EventId = 700, Level = LogLevel.Information, Message = "Creating workflow state history entry for case {CaseId}")]
    public partial void CreatingWorkflowStateHistory(int caseId);

    [LoggerMessage(EventId = 701, Level = LogLevel.Information, Message = "Workflow state history entry {EntryId} created for case {CaseId}")]
    public partial void WorkflowStateHistoryCreated(int entryId, int caseId);

    [LoggerMessage(EventId = 702, Level = LogLevel.Warning, Message = "Invalid model state for workflow state history creation")]
    public partial void WorkflowStateHistoryInvalidModelState();

    [LoggerMessage(EventId = 703, Level = LogLevel.Warning, Message = "Invalid case ID {CaseId} for workflow state history")]
    public partial void WorkflowStateHistoryInvalidCaseId(int caseId);

    [LoggerMessage(EventId = 704, Level = LogLevel.Information, Message = "Creating batch of {Count} workflow state history entries for case {CaseId}")]
    public partial void CreatingWorkflowStateHistoryBatch(int count, int caseId);

    [LoggerMessage(EventId = 705, Level = LogLevel.Information, Message = "Batch of {Count} workflow state history entries created for case {CaseId}")]
    public partial void WorkflowStateHistoryBatchCreated(int count, int caseId);

    [LoggerMessage(EventId = 706, Level = LogLevel.Warning, Message = "Empty batch submitted for workflow state history creation")]
    public partial void WorkflowStateHistoryBatchEmpty();

    // Authorities (EventId 900–901)

    [LoggerMessage(EventId = 900, Level = LogLevel.Information, Message = "Saving {Count} authorities for LOD case {CaseId}")]
    public partial void SavingAuthorities(int caseId, int count);

    [LoggerMessage(EventId = 901, Level = LogLevel.Information, Message = "Saved {Count} authorities for LOD case {CaseId}")]
    public partial void AuthoritiesSaved(int caseId, int count);

    // Case Checkout (EventId 800–805)

    [LoggerMessage(EventId = 800, Level = LogLevel.Information, Message = "Checking out LOD case {CaseId}")]
    public partial void CheckingOutCase(int caseId);

    [LoggerMessage(EventId = 801, Level = LogLevel.Information, Message = "LOD case {CaseId} checked out by {UserName}")]
    public partial void CaseCheckedOut(int caseId, string userName);

    [LoggerMessage(EventId = 802, Level = LogLevel.Warning, Message = "LOD case {CaseId} is already checked out by {UserName}")]
    public partial void CaseAlreadyCheckedOut(int caseId, string userName);

    [LoggerMessage(EventId = 803, Level = LogLevel.Information, Message = "Checking in LOD case {CaseId}")]
    public partial void CheckingInCase(int caseId);

    [LoggerMessage(EventId = 804, Level = LogLevel.Information, Message = "LOD case {CaseId} checked in")]
    public partial void CaseCheckedIn(int caseId);

    [LoggerMessage(EventId = 805, Level = LogLevel.Warning, Message = "LOD case {CaseId} is checked out by {UserName}, cannot check in")]
    public partial void CaseCheckedOutByAnother(int caseId, string userName);
}
