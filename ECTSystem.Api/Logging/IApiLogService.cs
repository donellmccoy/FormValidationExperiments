namespace ECTSystem.Api.Logging;

public interface IApiLogService
{
    // Cases
    void QueryingCases();
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
}
