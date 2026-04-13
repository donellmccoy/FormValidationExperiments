namespace ECTSystem.Api.Logging;

public interface ILoggingService
{
    // Cases
    void QueryingCases();
    void QueryingBookmarkedCases();
    void RetrievingCase(int caseId);
    void CaseNotFound(int caseId);
    void InvalidModelState(string action);
    void ModelStatePropertyError(string action, string property, string error);
    void ModelStateExceptionError(string action, string property, string exceptionMessage);
    void CaseCreated(int caseId);
    void UpdatingCase(int caseId);
    void CaseUpdated(int caseId);
    void PatchingCase(int caseId);
    void CasePatched(int caseId);
    void TransitioningCase(int caseId);
    void CaseTransitioned(int caseId, int historyEntryCount);
    void DeletingCase(int caseId);
    void CaseDeleted(int caseId);

    // Members
    void QueryingMembers();
    void RetrievingMember(int memberId);
    void MemberNotFound(int memberId);
    void MemberInvalidModelState(string action);
    void MemberCreated(int memberId);
    void UpdatingMember(int memberId);
    void MemberUpdated(int memberId);
    void PatchingMember(int memberId);
    void MemberPatched(int memberId);
    void DeletingMember(int memberId);
    void MemberDeleted(int memberId);

    // Documents
    void QueryingDocuments();
    void QueryingDocuments(int caseId);
    void RetrievingDocument(int documentId, int caseId);
    void DocumentNotFound(int documentId, int caseId);
    void DownloadingDocument(int documentId, int caseId);
    void DocumentContentNotFound(int documentId);
    void InvalidUpload(int caseId);
    void UploadingDocument(int caseId);
    void DocumentUploaded(int documentId, int caseId);
    void UploadFailed(int caseId, Exception ex);
    void DeletingDocument(int documentId, int caseId);
    void DocumentDeleted(int documentId, int caseId);
    void PatchingDocument(int documentId, int caseId);
    void DocumentPatched(int documentId, int caseId);
    void UpdatingDocument(int documentId, int caseId);
    void DocumentUpdated(int documentId, int caseId);
    void BlobDeleteFailed(string blobPath, int documentId, string error);

    // Bookmarks
    void QueryingBookmarks();
    void BookmarkCreated(int caseId);
    void BookmarkAlreadyExists(int caseId);
    void DeletingBookmark(int caseId);
    void BookmarkDeleted(int caseId);
    void BookmarkNotFound(int caseId);
    void CheckingBookmark(int caseId);

    // Navigation Properties
    void QueryingCaseNavigation(int caseId, string navigationProperty);
    void QueryingMemberNavigation(int memberId, string navigationProperty);

    // Workflow State Histories
    void CreatingWorkflowStateHistory(int caseId);
    void WorkflowStateHistoryCreated(int entryId, int caseId);
    void WorkflowStateHistoryInvalidModelState();
    void WorkflowStateHistoryInvalidCaseId(int caseId);
    // Authorities
    void SavingAuthorities(int caseId, int count);
    void AuthoritiesSaved(int caseId, int count);
    void QueryingAuthorities();
    void RetrievingAuthority(int authorityId);
    void AuthorityNotFound(int authorityId);
    void CreatingAuthority();
    void AuthorityCreated(int authorityId);
    void PatchingAuthority(int authorityId);
    void AuthorityPatched(int authorityId);
    void DeletingAuthority(int authorityId);
    void AuthorityDeleted(int authorityId);

    // Workflow State Histories
    void QueryingWorkflowStateHistories();
    void RetrievingWorkflowStateHistory(int entryId);

    // Case Checkout
    void CheckingOutCase(int caseId);
    void CaseCheckedOut(int caseId, string userName);
    void CaseAlreadyCheckedOut(int caseId, string userName);
    void CheckingInCase(int caseId);
    void CaseCheckedIn(int caseId);
    void CaseCheckedOutByAnother(int caseId, string userName);
}
