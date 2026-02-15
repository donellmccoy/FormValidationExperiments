namespace ECTSystem.Api.Controllers;

public partial class CasesController
{
    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Querying all LOD cases")]
        public static partial void QueryingCases(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Retrieving LOD case {CaseId}")]
        public static partial void RetrievingCase(ILogger logger, int caseId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "LOD case {CaseId} not found")]
        public static partial void CaseNotFound(ILogger logger, int caseId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid model state in {Action} action")]
        public static partial void InvalidModelState(ILogger logger, string action);

        [LoggerMessage(Level = LogLevel.Information, Message = "LOD case {CaseId} created")]
        public static partial void CaseCreated(ILogger logger, int caseId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Updating LOD case {CaseId}")]
        public static partial void UpdatingCase(ILogger logger, int caseId);

        [LoggerMessage(Level = LogLevel.Information, Message = "LOD case {CaseId} updated")]
        public static partial void CaseUpdated(ILogger logger, int caseId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Patching LOD case {CaseId}")]
        public static partial void PatchingCase(ILogger logger, int caseId);

        [LoggerMessage(Level = LogLevel.Information, Message = "LOD case {CaseId} patched")]
        public static partial void CasePatched(ILogger logger, int caseId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Deleting LOD case {CaseId}")]
        public static partial void DeletingCase(ILogger logger, int caseId);

        [LoggerMessage(Level = LogLevel.Information, Message = "LOD case {CaseId} deleted")]
        public static partial void CaseDeleted(ILogger logger, int caseId);
    }
}
