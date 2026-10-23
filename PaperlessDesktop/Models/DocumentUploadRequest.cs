namespace PaperlessDesktop.Models;

/// <summary>Everything needed to upload one document.</summary>
public record DocumentUploadRequest(
    string FilePath,
    string Title,
    int? CorrespondentId,
    int? DocumentTypeId,
    IReadOnlyList<int> TagIds);