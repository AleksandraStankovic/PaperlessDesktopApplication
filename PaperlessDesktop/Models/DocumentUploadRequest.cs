namespace PaperlessDesktop.Models;

public record DocumentUploadRequest(
    string FilePath,
    string Title,
    int? CorrespondentId,
    int? DocumentTypeId,
    IReadOnlyList<int> TagIds);